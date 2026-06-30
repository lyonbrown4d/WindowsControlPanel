using Prism.Commands;
using Prism.Mvvm;
using System.Windows;
using Serilog;
using WindowsControlPanel.Service;

namespace WindowsControlPanel.ViewModels;

public class OptimizeOptionPageViewModel : BindableBase, INavigationAware
{
    private readonly ISystemControlService _systemControlService;
    private readonly IRegionManager _regionManager;
    private readonly ILogger _logger;
    private string _globalStatus = string.Empty;
    private bool _isSecuritySelected;
    private bool _isDevelopmentSelected;
    private bool _isGameSelected;
    private bool _isStartupSelected;
    private bool _isCleanupSelected;
    private bool _isNetworkSelected;
    private string _lastFeature = "security";

    public OptimizeOptionPageViewModel(
        ISystemControlService systemControlService,
        IRegionManager regionManager,
        ILogger logger)
    {
        _systemControlService = systemControlService;
        _regionManager = regionManager;
        _logger = logger;

        OpenSecurityCommand = new DelegateCommand(() => NavigateFeature("security"));
        OpenDevelopmentCommand = new DelegateCommand(() => NavigateFeature("development"));
        OpenGameCommand = new DelegateCommand(() => NavigateFeature("gaming"));
        OpenStartupCommand = new DelegateCommand(() => NavigateFeature("startup"));
        OpenCleanupCommand = new DelegateCommand(() => NavigateFeature("cleanup"));
        OpenNetworkCommand = new DelegateCommand(() => NavigateFeature("network"));
        BackToHomeCommand = new DelegateCommand(() =>
            _regionManager.RequestNavigate("ContentRegion", "Dashboard"));

        _logger.Information("OptimizeOptionPageViewModel initialized.");
        _ = RefreshStatusAsync();
    }

    public string GlobalStatus
    {
        get => _globalStatus;
        private set => SetProperty(ref _globalStatus, value);
    }

    public bool IsSecuritySelected
    {
        get => _isSecuritySelected;
        private set => SetProperty(ref _isSecuritySelected, value);
    }

    public bool IsDevelopmentSelected
    {
        get => _isDevelopmentSelected;
        private set => SetProperty(ref _isDevelopmentSelected, value);
    }

    public bool IsGameSelected
    {
        get => _isGameSelected;
        private set => SetProperty(ref _isGameSelected, value);
    }

    public bool IsStartupSelected
    {
        get => _isStartupSelected;
        private set => SetProperty(ref _isStartupSelected, value);
    }

    public bool IsCleanupSelected
    {
        get => _isCleanupSelected;
        private set => SetProperty(ref _isCleanupSelected, value);
    }

    public bool IsNetworkSelected
    {
        get => _isNetworkSelected;
        private set => SetProperty(ref _isNetworkSelected, value);
    }

    public DelegateCommand OpenSecurityCommand { get; }
    public DelegateCommand OpenDevelopmentCommand { get; }
    public DelegateCommand OpenGameCommand { get; }
    public DelegateCommand OpenStartupCommand { get; }
    public DelegateCommand OpenCleanupCommand { get; }
    public DelegateCommand OpenNetworkCommand { get; }
    public DelegateCommand BackToHomeCommand { get; }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        var feature = navigationContext.Parameters.GetValue<string>("feature");
        var targetFeature = string.IsNullOrWhiteSpace(feature) ? "security" : feature;
        _logger.Information(
            "OnNavigatedTo OptimizeOption with feature={Feature}. RawParameters={Parameters}",
            targetFeature,
            navigationContext.Parameters?.ToString()
        );
        NavigateFeature(targetFeature);
    }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
    }

    private async Task RefreshStatusAsync()
    {
        try
        {
            var snapshot = await _systemControlService.GetStatusSnapshotAsync();
            var modeSummary = snapshot.IsVbsEnabled
                ? "安全优先（VBS 已开启）"
                : "性能优先（VBS 已关闭）";

            var adminSummary = snapshot.IsAdministrator ? "管理员权限已获取" : "当前未提权";
            GlobalStatus = $"当前系统状态：{modeSummary} | {adminSummary}";
            _logger.Debug(
                "RefreshStatusAsync completed. Mode={ModeSummary}, Admin={Admin}, HyperV={HyperV}, WSL={WSL}, VMP={VMP}, Sandbox={Sandbox}",
                modeSummary,
                snapshot.IsAdministrator,
                snapshot.HyperVState,
                snapshot.WslState,
                snapshot.VmPlatformState,
                snapshot.SandboxState
            );
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "RefreshStatusAsync failed.");
            GlobalStatus = "状态刷新失败，请查看日志。";
        }
    }

    private void NavigateFeature(string feature)
    {
        _logger.Information("NavigateFeature requested. Feature={Feature}", feature);
        _ = RefreshStatusAsync();
        ApplySelection(feature);
        _lastFeature = feature;

        if (feature == "security")
        {
            RequestNavigateFeatureRegion("SecurityVirtualization");
            return;
        }

        if (feature == "development")
        {
            RequestNavigateFeatureRegion("DevelopmentAdvanced");
            return;
        }

        if (feature == "gaming")
        {
            RequestNavigateFeatureRegion("GamingAdvanced");
            return;
        }

        if (feature == "startup")
        {
            RequestNavigateFeatureRegion("StartupPrograms");
            return;
        }

        if (feature == "network")
        {
            RequestNavigateFeatureRegion("NetworkDns");
            return;
        }

        if (feature == "cleanup")
        {
            var toolkitParameters = new NavigationParameters
            {
                { "feature", feature }
            };
            RequestNavigateFeatureRegion("SystemToolkit", toolkitParameters);
            return;
        }

        var parameters = new NavigationParameters
        {
            { "feature", feature }
        };

        RequestNavigateFeatureRegion("FeaturePlaceholder", parameters);
    }

    private void ApplySelection(string feature)
    {
        IsSecuritySelected = feature == "security";
        IsDevelopmentSelected = feature == "development";
        IsGameSelected = feature == "gaming";
        IsStartupSelected = feature == "startup";
        IsCleanupSelected = feature == "cleanup";
        IsNetworkSelected = feature == "network";
    }

    public void EnsureFeatureRegionReady()
    {
        _logger.Information("EnsureFeatureRegionReady invoked. LastFeature={Feature}", _lastFeature);
        NavigateFeature(string.IsNullOrWhiteSpace(_lastFeature) ? "security" : _lastFeature);
    }

    private void RequestNavigateFeatureRegion(string target, NavigationParameters? parameters = null)
    {
        _logger.Information(
            "RequestNavigateFeatureRegion scheduled. Target={Target}, HasParameters={HasParameters}",
            target,
            parameters is not null
        );

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            RequestNavigateInternal(target, parameters);
            return;
        }

        dispatcher.BeginInvoke(() =>
        {
            RequestNavigateInternal(target, parameters);
        });
    }

    private void RequestNavigateInternal(string target, NavigationParameters? parameters)
    {
        try
        {
            if (parameters is null)
            {
                _regionManager.RequestNavigate("FeatureRegion", target, result =>
                {
                    if (result.Success)
                    {
                        _logger.Information("FeatureRegion navigation succeeded. Target={Target}", target);
                        return;
                    }

                    _logger.Error(
                        result.Exception,
                        "FeatureRegion navigation failed. Target={Target}, Success={Success}",
                        target,
                        result.Success
                    );
                });
                return;
            }

            _regionManager.RequestNavigate("FeatureRegion", target, result =>
            {
                if (result.Success)
                {
                    _logger.Information(
                        "FeatureRegion navigation succeeded. Target={Target}, Parameters={Parameters}",
                        target,
                        parameters.ToString()
                    );
                    return;
                }

                _logger.Error(
                    result.Exception,
                    "FeatureRegion navigation failed. Target={Target}, Parameters={Parameters}, Success={Success}",
                    target,
                    parameters.ToString(),
                    result.Success
                );
            }, parameters);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "FeatureRegion navigation threw exception. Target={Target}", target);
        }
    }
}
