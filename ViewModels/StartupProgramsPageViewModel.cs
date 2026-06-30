using System.Collections.ObjectModel;
using Prism.Commands;
using Prism.Mvvm;
using Serilog;
using WindowsControlPanel.Models;
using WindowsControlPanel.Service;

namespace WindowsControlPanel.ViewModels;

public sealed class StartupProgramsPageViewModel : BindableBase, INavigationAware
{
    private readonly IStartupProgramService _startupProgramService;
    private readonly IRegionManager _regionManager;
    private readonly ILogger _logger;
    private string _statusSummary = "尚未读取启动项。";
    private string _executionLog = "此页面仅展示启动项，不会禁用、删除或修改系统配置。";
    private bool _isBusy;

    public StartupProgramsPageViewModel(
        IStartupProgramService startupProgramService,
        IRegionManager regionManager,
        ILogger logger
    )
    {
        _startupProgramService = startupProgramService;
        _regionManager = regionManager;
        _logger = logger;

        RefreshCommand = new DelegateCommand(async () => await RefreshProgramsAsync(), CanRunOperations);
        BackToHomeCommand = new DelegateCommand(
            () => _regionManager.RequestNavigate("ContentRegion", "Dashboard")
        );
    }

    public ObservableCollection<StartupProgramEntry> StartupPrograms { get; } = new();

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
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DelegateCommand RefreshCommand { get; }
    public DelegateCommand BackToHomeCommand { get; }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        _logger.Information("StartupProgramsPage navigated to.");
        _ = RefreshProgramsAsync();
    }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
    }

    private async Task RefreshProgramsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        AppendLog("开始读取启动项。");
        StatusSummary = "正在读取 WMI 与注册表启动项...";

        try
        {
            var entries = await _startupProgramService.GetStartupProgramsAsync(AppendLog);
            StartupPrograms.Clear();
            foreach (var entry in entries)
            {
                StartupPrograms.Add(entry);
            }

            var registryCount = entries.Count(entry => entry.Source.Contains("注册表", StringComparison.OrdinalIgnoreCase));
            var wmiCount = entries.Count(entry => entry.Source.Contains("WMI", StringComparison.OrdinalIgnoreCase));
            StatusSummary = $"共发现 {entries.Count} 个启动项：注册表 {registryCount} 个，WMI {wmiCount} 个。";
            AppendLog("启动项列表刷新完成。");
            _logger.Information(
                "Startup programs refreshed. Count={Count}, RegistryCount={RegistryCount}, WmiCount={WmiCount}",
                entries.Count,
                registryCount,
                wmiCount
            );
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Refresh startup programs failed.");
            StatusSummary = "启动项读取失败，请查看执行日志。";
            AppendLog($"启动项读取失败: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        ExecutionLog = string.IsNullOrWhiteSpace(ExecutionLog)
            ? line
            : $"{ExecutionLog}{Environment.NewLine}{line}";
    }

    private bool CanRunOperations()
    {
        return !IsBusy;
    }
}
