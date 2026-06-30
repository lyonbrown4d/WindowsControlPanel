using Prism.Commands;
using Prism.Mvvm;
using WindowsControlPanel.Service;

namespace WindowsControlPanel.ViewModels;

public class SystemToolkitPageViewModel : BindableBase, INavigationAware
{
    private readonly ISystemControlService _systemControlService;
    private readonly IOperationSafetyService _operationSafetyService;
    private readonly IRegionManager _regionManager;
    private string _title = "系统维护工具箱";
    private string _description = "集中访问可选功能、网络与维护入口。";
    private string _featureStateSummary = string.Empty;
    private string _executionLog = string.Empty;
    private bool _isBusy;

    public SystemToolkitPageViewModel(
        ISystemControlService systemControlService,
        IOperationSafetyService operationSafetyService,
        IRegionManager regionManager
    )
    {
        _systemControlService = systemControlService;
        _operationSafetyService = operationSafetyService;
        _regionManager = regionManager;

        EnableHyperVCommand = new DelegateCommand(
            async () => await RunFeatureSwitchAsync("Microsoft-Hyper-V-All", true),
            CanRunOperations
        );
        DisableHyperVCommand = new DelegateCommand(
            async () => await RunFeatureSwitchAsync("Microsoft-Hyper-V-All", false),
            CanRunOperations
        );
        EnableWslCommand = new DelegateCommand(
            async () => await RunFeatureSwitchAsync("Microsoft-Windows-Subsystem-Linux", true),
            CanRunOperations
        );
        DisableWslCommand = new DelegateCommand(
            async () => await RunFeatureSwitchAsync("Microsoft-Windows-Subsystem-Linux", false),
            CanRunOperations
        );
        EnableSandboxCommand = new DelegateCommand(
            async () => await RunFeatureSwitchAsync("Containers-DisposableClientVM", true),
            CanRunOperations
        );
        DisableSandboxCommand = new DelegateCommand(
            async () => await RunFeatureSwitchAsync("Containers-DisposableClientVM", false),
            CanRunOperations
        );
        OpenOptionalFeaturesCommand = new DelegateCommand(
            async () => await RunMaintenanceActionAsync(MaintenanceAction.OpenOptionalFeatures),
            CanRunOperations
        );
        OpenDnsSettingsCommand = new DelegateCommand(
            async () => await RunMaintenanceActionAsync(MaintenanceAction.OpenDnsSettings),
            CanRunOperations
        );
        OpenDeliveryOptimizationCommand = new DelegateCommand(
            async () => await RunMaintenanceActionAsync(MaintenanceAction.OpenDeliveryOptimization),
            CanRunOperations
        );
        OpenStorageSenseCommand = new DelegateCommand(
            async () => await RunMaintenanceActionAsync(MaintenanceAction.OpenStorageSense),
            CanRunOperations
        );
        GenerateEnergyReportCommand = new DelegateCommand(
            async () => await RunMaintenanceActionAsync(MaintenanceAction.GenerateEnergyReport),
            CanRunOperations
        );
        GenerateBatteryReportCommand = new DelegateCommand(
            async () => await RunMaintenanceActionAsync(MaintenanceAction.GenerateBatteryReport),
            CanRunOperations
        );
        QueryStartupProgramsCommand = new DelegateCommand(
            async () => await RunMaintenanceActionAsync(MaintenanceAction.QueryStartupPrograms),
            CanRunOperations
        );
        RefreshStatusCommand = new DelegateCommand(async () => await RefreshStatusAsync(), () => !_isBusy);
        BackToHomeCommand = new DelegateCommand(
            () => _regionManager.RequestNavigate("ContentRegion", "Dashboard")
        );
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Description
    {
        get => _description;
        private set => SetProperty(ref _description, value);
    }

    public string FeatureStateSummary
    {
        get => _featureStateSummary;
        private set => SetProperty(ref _featureStateSummary, value);
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

    public DelegateCommand EnableHyperVCommand { get; }
    public DelegateCommand DisableHyperVCommand { get; }
    public DelegateCommand EnableWslCommand { get; }
    public DelegateCommand DisableWslCommand { get; }
    public DelegateCommand EnableSandboxCommand { get; }
    public DelegateCommand DisableSandboxCommand { get; }
    public DelegateCommand OpenOptionalFeaturesCommand { get; }
    public DelegateCommand OpenDnsSettingsCommand { get; }
    public DelegateCommand OpenDeliveryOptimizationCommand { get; }
    public DelegateCommand OpenStorageSenseCommand { get; }
    public DelegateCommand GenerateEnergyReportCommand { get; }
    public DelegateCommand GenerateBatteryReportCommand { get; }
    public DelegateCommand QueryStartupProgramsCommand { get; }
    public DelegateCommand RefreshStatusCommand { get; }
    public DelegateCommand BackToHomeCommand { get; }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        var feature = navigationContext.Parameters.GetValue<string>("feature");
        ApplySection(feature);
        _ = RefreshStatusAsync();
    }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
    }

    private async Task RunFeatureSwitchAsync(string featureName, bool enable)
    {
        if (IsBusy)
        {
            return;
        }

        var profile = _operationSafetyService.CreateOptionalFeatureProfile(featureName, enable);
        if (!OperationConfirmation.Confirm(profile))
        {
            AppendLog($"已取消操作: {profile.Title}");
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _systemControlService.SetOptionalFeatureAsync(featureName, enable, AppendLog);
            AppendLog(result.Message);
            await RefreshStatusAsync(writeLog: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunMaintenanceActionAsync(MaintenanceAction action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _systemControlService.RunMaintenanceActionAsync(action, AppendLog);
            AppendLog(result.Message);
            if (action is MaintenanceAction.QueryStartupPrograms)
            {
                AppendLog("已输出启动项列表，可在日志中滚动查看。");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshStatusAsync(bool writeLog = true)
    {
        var snapshot = await _systemControlService.GetStatusSnapshotAsync();
        FeatureStateSummary =
            $"Hyper-V: {FormatFeature(snapshot.HyperVState)} | WSL: {FormatFeature(snapshot.WslState)} | VMP: {FormatFeature(snapshot.VmPlatformState)} | Sandbox: {FormatFeature(snapshot.SandboxState)} | Admin: {(snapshot.IsAdministrator ? "Yes" : "No")}";
        if (writeLog)
        {
            AppendLog("工具箱状态已刷新。");
        }
    }

    private void ApplySection(string? section)
    {
        switch (section)
        {
            case "startup":
                Title = "启动与服务工具箱";
                Description = "聚焦启动项与后台服务相关动作。";
                break;
            case "cleanup":
                Title = "磁盘与清理工具箱";
                Description = "集中提供存储感知、能耗分析和清理入口。";
                break;
            case "network":
                Title = "网络与 DNS 工具箱";
                Description = "快速进入 DNS、高级网络与下载优化设置。";
                break;
            default:
                Title = "系统维护工具箱";
                Description = "集中访问可选功能、网络与维护入口。";
                break;
        }
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        ExecutionLog = string.IsNullOrEmpty(ExecutionLog)
            ? line
            : $"{ExecutionLog}{Environment.NewLine}{line}";
    }

    private bool CanRunOperations()
    {
        return !IsBusy;
    }

    private void RaiseCommandCanExecuteChanged()
    {
        EnableHyperVCommand.RaiseCanExecuteChanged();
        DisableHyperVCommand.RaiseCanExecuteChanged();
        EnableWslCommand.RaiseCanExecuteChanged();
        DisableWslCommand.RaiseCanExecuteChanged();
        EnableSandboxCommand.RaiseCanExecuteChanged();
        DisableSandboxCommand.RaiseCanExecuteChanged();
        OpenOptionalFeaturesCommand.RaiseCanExecuteChanged();
        OpenDnsSettingsCommand.RaiseCanExecuteChanged();
        OpenDeliveryOptimizationCommand.RaiseCanExecuteChanged();
        OpenStorageSenseCommand.RaiseCanExecuteChanged();
        GenerateEnergyReportCommand.RaiseCanExecuteChanged();
        GenerateBatteryReportCommand.RaiseCanExecuteChanged();
        QueryStartupProgramsCommand.RaiseCanExecuteChanged();
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
