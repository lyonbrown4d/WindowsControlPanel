using System.IO;
using System.Text;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Unity;
using Serilog;
using WindowsControlPanel.Context;
using WindowsControlPanel.Service;
using WindowsControlPanel.Views;

namespace WindowsControlPanel;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : PrismApplication
{
    protected override void OnInitialized()
    {
        base.OnInitialized();
        var logger = Container.Resolve<Serilog.ILogger>();
        var themeSettingsService = Container.Resolve<IThemeSettingsService>();
        var themePreference = themeSettingsService.GetThemePreferenceAsync().GetAwaiter().GetResult();
        themeSettingsService.ApplyTheme(themePreference, Current.MainWindow);
        var regionManager = Container.Resolve<IRegionManager>();
        logger.Information("Application initialized. Navigating ContentRegion -> Dashboard.");
        regionManager.RequestNavigate("ContentRegion", "Dashboard", result =>
        {
            if (result.Success)
            {
                logger.Information("Navigation succeeded: ContentRegion -> Dashboard.");
                return;
            }

            logger.Error(
                result.Exception,
                "Navigation failed: ContentRegion -> Dashboard. Success={Success}",
                result.Success
            );
        });
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<DashboardPage>("Dashboard");
        containerRegistry.RegisterForNavigation<OptimizeOptionPage>("OptimizeOption");
        containerRegistry.RegisterForNavigation<SecurityVirtualizationPage>("SecurityVirtualization");
        containerRegistry.RegisterForNavigation<SystemToolkitPage>("SystemToolkit");
        containerRegistry.RegisterForNavigation<StartupProgramsPage>("StartupPrograms");
        containerRegistry.RegisterForNavigation<NetworkDnsPage>("NetworkDns");
        containerRegistry.RegisterForNavigation<DevelopmentAdvancedPage>("DevelopmentAdvanced");
        containerRegistry.RegisterForNavigation<GamingAdvancedPage>("GamingAdvanced");
        containerRegistry.RegisterForNavigation<FeaturePlaceholderPage>("FeaturePlaceholder");

        containerRegistry.RegisterSingleton<ISystemInfoService, SystemInfoService>();
        containerRegistry.RegisterSingleton<ISystemControlService, SystemControlService>();
        containerRegistry.RegisterSingleton<IStartupProgramService, StartupProgramService>();
        containerRegistry.RegisterSingleton<INetworkStatusService, NetworkStatusService>();
        containerRegistry.RegisterSingleton<IOperationSafetyService, OperationSafetyService>();
        containerRegistry.RegisterSingleton<IThemeSettingsService, ThemeSettingsService>();

        containerRegistry.RegisterSingleton<AppDbContext>(() =>
        {
            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WindowsOptimizer", "appdata.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            var db = new AppDbContext(options);
            db.Database.EnsureCreated(); // 自动创建数据库和表
            return db;
        });

        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsControlPanel",
            "logs"
        );
        Directory.CreateDirectory(logDirectory);
        var logPath = Path.Combine(logDirectory, "app.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug() // 最低日志级别
            .Enrich.FromLogContext()
            .WriteTo.Console() // 控制台输出
            .WriteTo.File(logPath,
                rollingInterval: RollingInterval.Day,
                encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
            ) // 文件输出
            .CreateLogger();

        Log.Logger.Information("Serilog configured. Log file path: {LogPath}", logPath);
        containerRegistry.RegisterInstance(Log.Logger);
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        // moduleCatalog.AddModule<CoreModule>();
        // moduleCatalog.AddModule<HotkeyModule>();
        // moduleCatalog.AddModule<WslModule>();
    }

    protected override Window CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }
}
