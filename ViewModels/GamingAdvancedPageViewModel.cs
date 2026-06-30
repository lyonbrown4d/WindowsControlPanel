using Prism.Commands;
using Prism.Mvvm;
using WindowsControlPanel.Service;

namespace WindowsControlPanel.ViewModels;

public class GamingAdvancedPageViewModel : BindableBase
{
    private readonly ISystemControlService _systemControlService;
    private readonly IOperationSafetyService _operationSafetyService;
    private readonly IRegionManager _regionManager;
    private string _statusSummary = string.Empty;
    private string _executionLog = string.Empty;
    private bool _isBusy;

    public GamingAdvancedPageViewModel(
        ISystemControlService systemControlService,
        IOperationSafetyService operationSafetyService,
        IRegionManager regionManager
    )
    {
        _systemControlService = systemControlService;
        _operationSafetyService = operationSafetyService;
        _regionManager = regionManager;

        ApplyGamingModeCommand = new DelegateCommand(async () => await ApplyGamingModeAsync(), CanRunActions);
        OpenGraphicsSettingsCommand = new DelegateCommand(
            async () => await RunActionAsync(AdvancedAction.OpenGraphicsSettings),
            CanRunActions
        );
        OpenGameModeSettingsCommand = new DelegateCommand(
            async () => await RunActionAsync(AdvancedAction.OpenGameModeSettings),
            CanRunActions
        );
        OpenGameBarSettingsCommand = new DelegateCommand(
            async () => await RunActionAsync(AdvancedAction.OpenGameBarSettings),
            CanRunActions
        );
        RefreshStatusCommand = new DelegateCommand(async () => await RefreshStatusAsync(), () => !_isBusy);
        BackToHomeCommand = new DelegateCommand(
            () => _regionManager.RequestNavigate("ContentRegion", "Dashboard")
        );

        _ = RefreshStatusAsync();
    }

    public string StatusSummary
    {
        get => _statusSummary;
        private set => SetProperty(ref _statusSummary, value);
    }

    public string ExecutionLog
    {
        get => _executionLog;
        private set => SetProperty(ref _executionLog, value);
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

    public DelegateCommand ApplyGamingModeCommand { get; }
    public DelegateCommand OpenGraphicsSettingsCommand { get; }
    public DelegateCommand OpenGameModeSettingsCommand { get; }
    public DelegateCommand OpenGameBarSettingsCommand { get; }
    public DelegateCommand RefreshStatusCommand { get; }
    public DelegateCommand BackToHomeCommand { get; }

    private async Task ApplyGamingModeAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var profile = _operationSafetyService.CreateModeProfile(OptimizationMode.Gaming, autoReboot: false);
        if (!OperationConfirmation.Confirm(profile))
        {
            AppendLog($"已取消操作: {profile.Title}");
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _systemControlService.ApplyModeAsync(
                OptimizationMode.Gaming,
                autoReboot: false,
                AppendLog
            );
            AppendLog(result.Message);
            await RefreshStatusAsync(writeLog: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunActionAsync(AdvancedAction action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _systemControlService.RunAdvancedActionAsync(action, AppendLog);
            AppendLog(result.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshStatusAsync(bool writeLog = true)
    {
        var snapshot = await _systemControlService.GetStatusSnapshotAsync();
        StatusSummary =
            $"Admin: {(snapshot.IsAdministrator ? "Yes" : "No")} | Hypervisor: {snapshot.HypervisorLaunchType} | VBS: {(snapshot.IsVbsEnabled ? "On" : "Off")} | Hyper-V: {FormatFeature(snapshot.HyperVState)}";
        if (writeLog)
        {
            AppendLog("游戏高级页状态已刷新。");
        }
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        ExecutionLog = string.IsNullOrEmpty(ExecutionLog)
            ? line
            : $"{ExecutionLog}{Environment.NewLine}{line}";
    }

    private bool CanRunActions()
    {
        return !IsBusy;
    }

    private void RaiseCommandCanExecuteChanged()
    {
        ApplyGamingModeCommand.RaiseCanExecuteChanged();
        OpenGraphicsSettingsCommand.RaiseCanExecuteChanged();
        OpenGameModeSettingsCommand.RaiseCanExecuteChanged();
        OpenGameBarSettingsCommand.RaiseCanExecuteChanged();
        RefreshStatusCommand.RaiseCanExecuteChanged();
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
