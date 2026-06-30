using System.Collections.ObjectModel;
using Prism.Commands;
using Prism.Mvvm;
using Serilog;
using WindowsControlPanel.Service;

namespace WindowsControlPanel.ViewModels;

public class NetworkDnsPageViewModel : BindableBase, INavigationAware
{
    private readonly INetworkStatusService _networkStatusService;
    private readonly ISystemControlService _systemControlService;
    private readonly IRegionManager _regionManager;
    private readonly ILogger _logger;
    private NetworkAdapterSnapshot? _selectedAdapter;
    private string _statusSummary = "等待刷新网络状态。";
    private string _hintText = "当前页面仅读取网络状态，不修改 DNS、DHCP 或网关配置。";
    private string _executionLog = string.Empty;
    private bool _isBusy;

    public NetworkDnsPageViewModel(
        INetworkStatusService networkStatusService,
        ISystemControlService systemControlService,
        IRegionManager regionManager,
        ILogger logger
    )
    {
        _networkStatusService = networkStatusService;
        _systemControlService = systemControlService;
        _regionManager = regionManager;
        _logger = logger;

        RefreshCommand = new DelegateCommand(async () => await RefreshAsync(), CanRunActions);
        OpenDnsSettingsCommand = new DelegateCommand(
            async () => await OpenDnsSettingsAsync(),
            CanRunActions
        );
        BackToHomeCommand = new DelegateCommand(
            () => _regionManager.RequestNavigate("ContentRegion", "Dashboard")
        );
    }

    public ObservableCollection<NetworkAdapterSnapshot> Adapters { get; } = new();

    public NetworkAdapterSnapshot? SelectedAdapter
    {
        get => _selectedAdapter;
        set => SetProperty(ref _selectedAdapter, value);
    }

    public string StatusSummary
    {
        get => _statusSummary;
        private set => SetProperty(ref _statusSummary, value);
    }

    public string HintText
    {
        get => _hintText;
        private set => SetProperty(ref _hintText, value);
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

    public DelegateCommand RefreshCommand { get; }
    public DelegateCommand OpenDnsSettingsCommand { get; }
    public DelegateCommand BackToHomeCommand { get; }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        _ = RefreshAsync(writeLog: false);
    }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
    }

    private async Task RefreshAsync(bool writeLog = true)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var previouslySelectedId = SelectedAdapter?.Id;
            var snapshot = await _networkStatusService.GetNetworkStatusAsync();

            Adapters.Clear();
            foreach (var adapter in snapshot.Adapters)
            {
                Adapters.Add(adapter);
            }

            SelectedAdapter = Adapters.FirstOrDefault(adapter => adapter.Id == previouslySelectedId)
                ?? Adapters.FirstOrDefault(adapter => adapter.IsUp)
                ?? Adapters.FirstOrDefault();

            StatusSummary = $"{snapshot.Summary} 更新时间：{snapshot.Timestamp:HH:mm:ss}";
            HintText = "提示：这里展示的是只读快照。修改 DNS 请打开 Windows 设置，后续模块可在此基础上扩展安全的变更预览。";

            if (writeLog)
            {
                AppendLog("网络状态已刷新。");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Refresh network status failed.");
            StatusSummary = "网络状态刷新失败，请查看应用日志。";
            AppendLog($"网络状态刷新失败：{ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OpenDnsSettingsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _systemControlService.RunMaintenanceActionAsync(
                MaintenanceAction.OpenDnsSettings,
                AppendLog
            );
            AppendLog(result.Message);
        }
        finally
        {
            IsBusy = false;
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
        RefreshCommand.RaiseCanExecuteChanged();
        OpenDnsSettingsCommand.RaiseCanExecuteChanged();
    }
}
