// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Media;
using Lepo.i18n.DependencyInjection;
using Wpf.Ui.Appearance;
using Wpf.Ui.DependencyInjection;
using WpfScada.Controls.Sidebar;
using WpfScada.DependencyModel;
using WpfScada.Resources;
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
                _ = services.AddSingleton<PollingStore>();
                _ = services.AddSingleton<PollingScheduler>();
                _ = services.AddSingleton<AppConfigService>(_ => AppConfigService.Load());
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
        var config = Services.Plc.AppConfigService.Load();
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
    }

    /// <summary>
    /// Occurs when an exception is thrown by an application but not handled.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
    }
}
