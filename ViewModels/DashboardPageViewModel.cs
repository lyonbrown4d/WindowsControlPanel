using System.Collections.ObjectModel;
using System.Windows;
using Prism.Commands;
using Prism.Mvvm;
using WindowsControlPanel.Service;

namespace WindowsControlPanel.ViewModels;

public sealed class ThemePreferenceOption
{
    public ThemePreferenceOption(AppThemePreference value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }

    public AppThemePreference Value { get; }
    public string DisplayName { get; }
}

public class DashboardPageViewModel : BindableBase
{
    private readonly ISystemControlService _systemControlService;
    private readonly IThemeSettingsService _themeSettingsService;
    private readonly IRegionManager _regionManager;
    private string _systemInfoSummary = "正在读取系统信息...";
    private string _administratorStatus = "权限状态：检测中";
    private string _memoryStatus = "内存状态：检测中";
    private string _securitySummary = "安全状态：检测中";
    private string _virtualizationSummary = "虚拟化状态：检测中";
    private string _performanceSummary = "性能状态：检测中";
    private string _securityRecommendation = "正在生成安全建议。";
    private string _performanceRecommendation = "正在生成性能建议。";
    private string _refreshState = "等待首次刷新";
    private string _themeSummary = "主题：读取中";
    private AppThemePreference _selectedThemePreference = AppThemePreference.FollowSystem;
    private bool _isLoadingThemePreference;

    public DashboardPageViewModel(
        ISystemControlService systemControlService,
        IThemeSettingsService themeSettingsService,
        IRegionManager regionManager)
    {
        _systemControlService = systemControlService;
        _themeSettingsService = themeSettingsService;
        _regionManager = regionManager;

        ThemeOptions = new ObservableCollection<ThemePreferenceOption>
        {
            new(AppThemePreference.FollowSystem, "跟随系统"),
            new(AppThemePreference.Dark, "深色"),
            new(AppThemePreference.Light, "浅色")
        };

        OpenSecurityCommand = new DelegateCommand(() => NavigateToHub("security"));
        OpenDevelopmentCommand = new DelegateCommand(() => NavigateToHub("development"));
        OpenGameCommand = new DelegateCommand(() => NavigateToHub("gaming"));
        OpenStartupCommand = new DelegateCommand(() => NavigateToHub("startup"));
        OpenCleanupCommand = new DelegateCommand(() => NavigateToHub("cleanup"));
        OpenNetworkCommand = new DelegateCommand(() => NavigateToHub("network"));
        RefreshCommand = new DelegateCommand(async () => await RefreshDataAsync());

        _ = LoadThemePreferenceAsync();
        _ = RefreshDataAsync();
    }

    public ObservableCollection<ThemePreferenceOption> ThemeOptions { get; }

    public string SystemInfoSummary
    {
        get => _systemInfoSummary;
        private set => SetProperty(ref _systemInfoSummary, value);
    }

    public string LastRefreshTime
    {
        get => _refreshState;
        private set => SetProperty(ref _refreshState, value);
    }

    public string AdministratorStatus
    {
        get => _administratorStatus;
        private set => SetProperty(ref _administratorStatus, value);
    }

    public string MemoryStatus
    {
        get => _memoryStatus;
        private set => SetProperty(ref _memoryStatus, value);
    }

    public string SecuritySummary
    {
        get => _securitySummary;
        private set => SetProperty(ref _securitySummary, value);
    }

    public string VirtualizationSummary
    {
        get => _virtualizationSummary;
        private set => SetProperty(ref _virtualizationSummary, value);
    }

    public string PerformanceSummary
    {
        get => _performanceSummary;
        private set => SetProperty(ref _performanceSummary, value);
    }

    public string SecurityRecommendation
    {
        get => _securityRecommendation;
        private set => SetProperty(ref _securityRecommendation, value);
    }

    public string PerformanceRecommendation
    {
        get => _performanceRecommendation;
        private set => SetProperty(ref _performanceRecommendation, value);
    }

    public string ThemeSummary
    {
        get => _themeSummary;
        private set => SetProperty(ref _themeSummary, value);
    }

    public AppThemePreference SelectedThemePreference
    {
        get => _selectedThemePreference;
        set
        {
            if (!SetProperty(ref _selectedThemePreference, value))
            {
                return;
            }

            ThemeSummary = $"主题：{_themeSettingsService.FormatThemePreference(value)}";
            if (!_isLoadingThemePreference)
            {
                _ = SaveThemePreferenceAsync(value);
            }
        }
    }

    public DelegateCommand OpenSecurityCommand { get; }
    public DelegateCommand OpenDevelopmentCommand { get; }
    public DelegateCommand OpenGameCommand { get; }
    public DelegateCommand OpenStartupCommand { get; }
    public DelegateCommand OpenCleanupCommand { get; }
    public DelegateCommand OpenNetworkCommand { get; }
    public DelegateCommand RefreshCommand { get; }

    private async Task LoadThemePreferenceAsync()
    {
        _isLoadingThemePreference = true;
        try
        {
            SelectedThemePreference = await _themeSettingsService.GetThemePreferenceAsync();
            ThemeSummary = $"主题：{_themeSettingsService.FormatThemePreference(SelectedThemePreference)}";
        }
        finally
        {
            _isLoadingThemePreference = false;
        }
    }

    private async Task SaveThemePreferenceAsync(AppThemePreference preference)
    {
        await _themeSettingsService.SetThemePreferenceAsync(preference);
        _themeSettingsService.ApplyTheme(preference, Application.Current.MainWindow);
        ThemeSummary = $"主题：{_themeSettingsService.FormatThemePreference(preference)}";
    }

    private async Task RefreshDataAsync()
    {
        LastRefreshTime = "正在刷新状态...";
        var snapshot = await _systemControlService.GetStatusSnapshotAsync();
        var osVersion = string.IsNullOrWhiteSpace(snapshot.OSVersion) ? "未知系统" : snapshot.OSVersion;
        var machineName = string.IsNullOrWhiteSpace(snapshot.MachineName) ? "未知设备" : snapshot.MachineName;
        var cpuInfo = string.IsNullOrWhiteSpace(snapshot.CPUInfo) ? "未知 CPU" : snapshot.CPUInfo;
        var virtualizationFirmware = snapshot.IsVirtualizationFirmwareEnabled switch
        {
            true => "固件虚拟化已启用",
            false => "固件虚拟化未启用",
            _ => "固件虚拟化未知"
        };

        SystemInfoSummary = $"{machineName} | {osVersion} | {cpuInfo}";
        AdministratorStatus = snapshot.IsAdministrator
            ? "管理员权限：已获取"
            : "管理员权限：未获取，部分切换操作需提权";
        MemoryStatus = BuildMemoryStatus(snapshot.TotalMemory, snapshot.FreeMemory);
        SecuritySummary = snapshot.IsVbsEnabled
            ? $"安全模式偏稳健：VBS 已开启，HVCI {FormatToggle(snapshot.IsHvciEnabled)}，安全启动 {FormatToggle(snapshot.IsSecureBootEnabled)}。"
            : $"性能模式更轻量：VBS 未开启，HVCI {FormatToggle(snapshot.IsHvciEnabled)}，安全启动 {FormatToggle(snapshot.IsSecureBootEnabled)}。";
        VirtualizationSummary =
            $"Hyper-V {FormatFeature(snapshot.HyperVState)} | WSL {FormatFeature(snapshot.WslState)} | VMP {FormatFeature(snapshot.VmPlatformState)} | {virtualizationFirmware}";
        PerformanceSummary =
            $"Hypervisor {FormatRawState(snapshot.HypervisorLaunchType)} | 沙盒 {FormatFeature(snapshot.SandboxState)} | {MemoryStatus}";
        SecurityRecommendation = BuildSecurityRecommendation(snapshot);
        PerformanceRecommendation = BuildPerformanceRecommendation(snapshot);
        LastRefreshTime = $"最近刷新：{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
    }

    private void NavigateToHub(string feature)
    {
        var parameters = new NavigationParameters
        {
            { "feature", feature }
        };

        _regionManager.RequestNavigate("ContentRegion", "OptimizeOption", parameters);
    }

    private static string FormatFeature(OptionalFeatureState state)
    {
        return state switch
        {
            OptionalFeatureState.Enabled => "已启用",
            OptionalFeatureState.Disabled => "已禁用",
            _ => "未知"
        };
    }

    private static string FormatToggle(bool enabled)
    {
        return enabled ? "已开启" : "未开启";
    }

    private static string FormatRawState(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "未知" : value;
    }

    private static string BuildMemoryStatus(string totalMemory, string freeMemory)
    {
        var totalMb = ParseMemoryMb(totalMemory);
        var freeMb = ParseMemoryMb(freeMemory);

        if (totalMb is null || freeMb is null || totalMb <= 0)
        {
            return $"内存：可用 {NormalizeMemory(freeMemory)} / 总计 {NormalizeMemory(totalMemory)}";
        }

        var usedPercent = Math.Clamp((int)Math.Round((1 - freeMb.Value / totalMb.Value) * 100), 0, 100);
        return $"内存：已用 {usedPercent}%（可用 {FormatMemoryGb(freeMb.Value)} / 总计 {FormatMemoryGb(totalMb.Value)}）";
    }

    private static string BuildSecurityRecommendation(SystemStatusSnapshot snapshot)
    {
        if (!snapshot.IsAdministrator)
        {
            return "建议：以管理员身份运行后再执行安全/虚拟化切换。";
        }

        if (!snapshot.IsSecureBootEnabled)
        {
            return "建议：如设备支持，可在固件中开启安全启动以增强启动链路保护。";
        }

        if (!snapshot.IsVbsEnabled)
        {
            return "建议：办公或开发场景可进入安全与虚拟化模块开启 VBS。";
        }

        return "建议：安全基线良好，切换前先确认是否会影响游戏或模拟器性能。";
    }

    private static string BuildPerformanceRecommendation(SystemStatusSnapshot snapshot)
    {
        if (snapshot.IsVbsEnabled || IsEnabled(snapshot.HyperVState) || IsEnabled(snapshot.VmPlatformState))
        {
            return "建议：追求游戏性能时，可使用游戏优化预设关闭虚拟化链路。";
        }

        if (!snapshot.IsAdministrator)
        {
            return "建议：当前适合查看状态；应用性能预设前需要管理员权限。";
        }

        return "建议：当前偏性能优先，可继续检查启动项、服务和磁盘清理。";
    }

    private static bool IsEnabled(OptionalFeatureState state)
    {
        return state == OptionalFeatureState.Enabled;
    }

    private static double? ParseMemoryMb(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return double.TryParse(parts.FirstOrDefault(), out var mb) ? mb : null;
    }

    private static string FormatMemoryGb(double mb)
    {
        return $"{mb / 1024:0.0} GB";
    }

    private static string NormalizeMemory(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "未知" : value;
    }
}
