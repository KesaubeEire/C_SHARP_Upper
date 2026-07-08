// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Media;
using Lepo.i18n.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Wpf.Ui.Appearance;
using Wpf.Ui.DependencyInjection;
using WpfScada.Controls.Sidebar;
using WpfScada.DependencyModel;
using WpfScada.Resources;
using WpfScada.Services.Motion;
using WpfScada.Services;
using WpfScada.Services.Contracts;
using WpfScada.Services.Plc;
using WpfScada.ViewModels.Pages;
using WpfScada.ViewModels.Pages.Plc;
using WpfScada.ViewModels.Plc;
using WpfScada.ViewModels.Windows;
using WpfScada.Views.Pages;
using WpfScada.Views.Pages.Plc;
using WpfScada.Views.Windows;

namespace WpfScada;

public partial class App
{
    // The .NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
    // https://docs.microsoft.com/dotnet/core/extensions/configuration
    // https://docs.microsoft.com/dotnet/core/extensions/logging
    private static readonly IHost _host = Host.CreateDefaultBuilder()
        .ConfigureAppConfiguration(c =>
        {
            _ = c.SetBasePath(AppContext.BaseDirectory);
        })
        .ConfigureLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddDebug();
            logging.AddSerilog(new LoggerConfiguration()
                .WriteTo.File(
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "WpfScada", "logs", "wpfscada-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30)
                .CreateLogger(),
                dispose: true);
        })
        .ConfigureServices(
            (_1, services) =>
            {
                _ = services.AddNavigationViewPageProvider();

                // App Host
                _ = services.AddHostedService<ApplicationHostService>();

                // Main window container with navigation
                _ = services.AddSingleton<IWindow, MainWindow>();
                _ = services.AddSingleton<MainWindowViewModel>();
                _ = services.AddSingleton<INavigationService, NavigationService>();
                _ = services.AddSingleton<ISnackbarService, SnackbarService>();
                _ = services.AddSingleton<IContentDialogService, ContentDialogService>();
                _ = services.AddSingleton<WindowsProviderService>();

                // PLC Services
                _ = services.AddSingleton<PpeConnectionSectionViewModel>();
                _ = services.AddSingleton<S7Service>();
                _ = services.AddSingleton<VplcHttpService>();
                _ = services.AddSingleton<InputHistoryService>();
                _ = services.AddSingleton<PollingStore>();
                _ = services.AddSingleton<PollingScheduler>();
                _ = services.AddSingleton<AppConfigService>(sp => AppConfigService.Load(
                    logger: sp.GetService<ILogger<AppConfigService>>()));
                _ = services.AddSingleton<AlarmService>();
                _ = services.AddSingleton<RecipeService>();

                // Top-level pages
                _ = services.AddSingleton<DashboardPage>();
                _ = services.AddSingleton<DashboardViewModel>();
                _ = services.AddSingleton<AllControlsPage>();
                _ = services.AddSingleton<AllControlsViewModel>();
                _ = services.AddSingleton<SettingsPage>();
                _ = services.AddSingleton<SettingsViewModel>();

                // PLC Pages
                _ = services.AddSingleton<IoMonitorPage>();
                _ = services.AddSingleton<TrendChartPage>();
                _ = services.AddSingleton<GaugeDashboardPage>();
                _ = services.AddSingleton<DbMonitorPage>();
                _ = services.AddSingleton<AlarmPage>();
                _ = services.AddSingleton<AlarmViewModel>();
                _ = services.AddSingleton<RecipePage>();
                _ = services.AddSingleton<RecipeViewModel>();
                _ = services.AddSingleton<ModbusPage>();
                _ = services.AddSingleton<ModbusViewModel>();

                // Motion control card
                _ = services.AddSingleton<IMotionController, MockMotionController>();
                _ = services.AddSingleton<MotionPage>();
                _ = services.AddSingleton<MotionViewModel>();

                // Sidebar panel
                _ = services.AddTransient<PpeConnectionSection>();

                // All other pages and view models
                _ = services.AddTransientFromNamespace("WpfScada.Views", GalleryAssembly.Asssembly);
                _ = services.AddTransientFromNamespace(
                    "WpfScada.ViewModels",
                    GalleryAssembly.Asssembly
                );

                _ = services.AddStringLocalizer(b =>
                {
                    b.FromResource<Translations>(new("pl-PL"));
                });
            }
        )
        .Build();

    /// <summary>
    /// Gets registered service.
    /// </summary>
    /// <typeparam name="T">Type of the service to get.</typeparam>
    /// <returns>Instance of the service or <see langword="null"/>.</returns>
    public static T GetRequiredService<T>()
        where T : class
    {
        return _host.Services.GetRequiredService<T>();
    }

    /// <summary>
    /// Occurs when the application is loading.
    /// </summary>
    private void OnStartup(object sender, StartupEventArgs e)
    {
        // 加载已保存的主题配置
        var config = Services.Plc.AppConfigService.Load(logger: null);
        var theme = config.ThemeMode == "Light" ? ApplicationTheme.Light : ApplicationTheme.Dark;
        ApplicationThemeManager.Apply(theme);

        // 自适应当前主题，覆盖 CardBackground 以在浅色模式下加深
        OverrideCardBackground(theme);
        ApplicationThemeManager.Changed += (_, _) =>
        {
            OverrideCardBackground(ApplicationThemeManager.GetAppTheme());
        };

        _host.Start();
    }

    /// <summary>浅色模式下加深 CardBackground，深色还原为库默认值。</summary>
    private static void OverrideCardBackground(ApplicationTheme theme)
    {
        if (theme == ApplicationTheme.Light)
            Current.Resources["CardBackground"] = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA));
        else
            Current.Resources.Remove("CardBackground");
    }

    /// <summary>
    /// Occurs when the application is closing.
    /// </summary>
    private void OnExit(object sender, ExitEventArgs e)
    {
        _host.StopAsync().Wait();

        _host.Dispose();

        // 确保所有 Serilog 缓冲日志写入磁盘
        Log.CloseAndFlush();
    }

    /// <summary>
    /// Occurs when an exception is thrown by an application but not handled.
    /// 可恢复的 UI 异常（如绑定错误、资源未找到）记日志后吞掉；
    /// 致命异常（访问违例、栈溢出、文件损坏等）不设置 Handled，让进程自然崩溃。
    /// 上位机在内部状态不明时继续运行比崩溃更危险。
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var logger = _host.Services.GetRequiredService<ILogger<App>>();

        // 已知偏可恢复的 UI 异常 → 记日志、吞掉。
        // 空引用、XAML 加载失败、类型转换失败通常代表程序状态或资源已经不可信，交给默认崩溃处理。
        if (e.Exception is InvalidOperationException
            or ArgumentException
            or KeyNotFoundException
            or TaskCanceledException)
        {
            logger.LogWarning(e.Exception, "可恢复的 UI 异常（已拦截）: {Msg}", e.Exception.Message);
            e.Handled = true;
            return;
        }

        // 致命异常 → 记录后让程序崩溃（不设 Handled）
#pragma warning disable CA1873 // 进程即将终止，日志开销无关紧要
        logger.LogCritical(e.Exception, "未处理的致命 UI 异常，进程即将终止: {Msg}", e.Exception.Message);
#pragma warning restore CA1873
        // 不设置 e.Handled = true，让 WPF 继续默认的崩溃处理
    }
}
