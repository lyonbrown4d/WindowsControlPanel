using Prism.Commands;
using Prism.Mvvm;
using WindowsControlPanel.Service;
using Application = System.Windows.Application;

namespace WindowsControlPanel.ViewModels;

public class SecurityVirtualizationPageViewModel : BindableBase
{
    private readonly ISystemControlService _systemControlService;
    private readonly IOperationSafetyService _operationSafetyService;
    private readonly IRegionManager _regionManager;
    private string _vbsStatus = string.Empty;
    private string _virtualizationStatus = string.Empty;
    private string _adminStatus = string.Empty;
    private string _restartHint = string.Empty;
    private string _executionLog = string.Empty;
    private string _auditSummary = string.Empty;
    private bool _autoRebootAfterApply;
    private bool _isBusy;
    private bool _isAdministrator;

    public SecurityVirtualizationPageViewModel(
        ISystemControlService systemControlService,
        IOperationSafetyService operationSafetyService,
        IRegionManager regionManager)
    {
        _systemControlService = systemControlService;
        _operationSafetyService = operationSafetyService;
        _regionManager = regionManager;

        EnableDevModeCommand = new DelegateCommand(
            async () => await ApplyModeAsync(OptimizationMode.Development),
            CanRunModeSwitch
        );
        EnableGameModeCommand = new DelegateCommand(
            async () => await ApplyModeAsync(OptimizationMode.Gaming),
            CanRunModeSwitch
        );
        RelaunchAsAdminCommand = new DelegateCommand(
            RelaunchAsAdmin,
            () => !_isBusy && !_isAdministrator
        );
        RefreshCommand = new DelegateCommand(
            async () => await RefreshStatusAsync(),
            () => !_isBusy
        );
        BackToHomeCommand = new DelegateCommand(() =>
            _regionManager.RequestNavigate("ContentRegion", "Dashboard"));

        _ = RefreshStatusAsync(writeLog: false);
    }

    public string VbsStatus
    {
        get => _vbsStatus;
        private set => SetProperty(ref _vbsStatus, value);
    }

    public string AdminStatus
    {
        get => _adminStatus;
        private set => SetProperty(ref _adminStatus, value);
    }

    public string VirtualizationStatus
    {
        get => _virtualizationStatus;
        private set => SetProperty(ref _virtualizationStatus, value);
    }

    public string RestartHint
    {
        get => _restartHint;
        private set => SetProperty(ref _restartHint, value);
    }

    public string ExecutionLog
    {
        get => _executionLog;
        private set => SetProperty(ref _executionLog, value);
    }

    public string AuditSummary
    {
        get => _auditSummary;
        private set => SetProperty(ref _auditSummary, value);
    }

    public bool AutoRebootAfterApply
    {
        get => _autoRebootAfterApply;
        set => SetProperty(ref _autoRebootAfterApply, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandCanExecuteChanged();
            }
        }
    }

    public DelegateCommand EnableDevModeCommand { get; }
    public DelegateCommand EnableGameModeCommand { get; }
    public DelegateCommand RelaunchAsAdminCommand { get; }
    public DelegateCommand RefreshCommand { get; }
    public DelegateCommand BackToHomeCommand { get; }

    private async Task ApplyModeAsync(OptimizationMode mode)
    {
        if (IsBusy)
        {
            return;
        }

        var profile = _operationSafetyService.CreateModeProfile(mode, AutoRebootAfterApply);
        if (!OperationConfirmation.Confirm(profile))
        {
            AppendLog($"已取消操作: {profile.Title}");
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _systemControlService.ApplyModeAsync(mode, AutoRebootAfterApply, AppendLog);
            AppendLog(result.Message);
            if (!result.Success && result.RequiresElevation)
            {
                AppendLog("当前进程未提权。请点击“以管理员重新启动”。");
            }

            await RefreshStatusAsync(writeLog: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshStatusAsync(bool writeLog = true)
    {
        var snapshot = await _systemControlService.GetStatusSnapshotAsync();
        _isAdministrator = snapshot.IsAdministrator;

        VbsStatus = snapshot.IsVbsEnabled
            ? "VBS 当前状态: 已开启"
            : "VBS 当前状态: 已关闭";
        VirtualizationStatus =
            $"Hyper-V: {FormatFeature(snapshot.HyperVState)} | WSL: {FormatFeature(snapshot.WslState)} | VMP: {FormatFeature(snapshot.VmPlatformState)} | Sandbox: {FormatFeature(snapshot.SandboxState)} | Hypervisor: {snapshot.HypervisorLaunchType}";
        AdminStatus = snapshot.IsAdministrator
            ? "管理员权限: 已获取"
            : "管理员权限: 未获取（敏感操作前需要提权）";
        RestartHint = "提示: 修改虚拟化能力后通常需要重启系统生效。";

        await RefreshAuditSummaryAsync();
        if (writeLog)
        {
            AppendLog("状态已刷新。");
        }

        RaiseCommandCanExecuteChanged();
    }

    private async Task RefreshAuditSummaryAsync()
    {
        var recentAudits = await _systemControlService.GetRecentAuditsAsync(6);
        if (recentAudits.Count == 0)
        {
            AuditSummary = "暂无审计记录。";
            return;
        }

        AuditSummary = string.Join(
            Environment.NewLine,
            recentAudits.Select(x => $"[{x.Timestamp:MM-dd HH:mm:ss}] {x.Message}")
        );
    }

    private void RelaunchAsAdmin()
    {
        if (_systemControlService.IsRunningAsAdministrator())
        {
            AppendLog("当前已是管理员权限。");
            return;
        }

        var started = _systemControlService.TryRestartAsAdministrator();
        if (!started)
        {
            AppendLog("提权启动失败，可能是你取消了 UAC。");
            return;
        }

        AppendLog("已拉起管理员实例，当前实例即将退出。");
        Application.Current.Shutdown();
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        ExecutionLog = string.IsNullOrEmpty(ExecutionLog)
            ? line
            : $"{ExecutionLog}{Environment.NewLine}{line}";
    }

    private bool CanRunModeSwitch()
    {
        return !IsBusy;
    }

    private void RaiseCommandCanExecuteChanged()
    {
        EnableDevModeCommand.RaiseCanExecuteChanged();
        EnableGameModeCommand.RaiseCanExecuteChanged();
        RelaunchAsAdminCommand.RaiseCanExecuteChanged();
        RefreshCommand.RaiseCanExecuteChanged();
    }

    private static string FormatFeature(OptionalFeatureState state)
    {
        return state switch
        {
            OptionalFeatureState.Enabled => "On",
            OptionalFeatureState.Disabled => "Off",
            _ => "Unknown"
        };
    }
}
