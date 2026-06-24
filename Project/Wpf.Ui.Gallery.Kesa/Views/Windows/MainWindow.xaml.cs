// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.Controls.Sidebar;
using Wpf.Ui.Gallery.Services.Contracts;
using Wpf.Ui.Gallery.ViewModels.Windows;
using Wpf.Ui.Gallery.Views.Pages;

namespace Wpf.Ui.Gallery.Views.Windows;

public partial class MainWindow : IWindow
{
    public MainWindow(
        MainWindowViewModel viewModel,
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        ISnackbarService snackbarService,
        IContentDialogService contentDialogService,
        Services.Plc.AppConfigService config
    )
    {
        Appearance.SystemThemeWatcher.Watch(this);

        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();

        // 恢复窗口位置/大小
        if (config.WindowLeft >= 0 && config.WindowTop >= 0)
        {
            Left = config.WindowLeft;
            Top = config.WindowTop;
        }
        Width = config.WindowWidth;
        Height = config.WindowHeight;
        if (Enum.TryParse<WindowState>(config.WindowState, out var ws))
            WindowState = ws;

        // 关闭时保存配置
        Closed += (_, _) =>
        {
            config.WindowLeft = Left;
            config.WindowTop = Top;
            config.WindowWidth = Width;
            config.WindowHeight = Height;
            config.WindowState = WindowState.ToString();
            config.Save();
        };

        // 创建 PLC 连接综合面板并注入到 "PLC 连接" 的 MenuItems 中
        var plcSection = serviceProvider.GetRequiredService<PpeConnectionSection>();
        foreach (var item in ViewModel.MenuItems)
        {
            if (item is NavigationViewItem navItem && navItem.Content is string s && s == "PLC 连接")
            {
                navItem.MenuItemsSource = new object[] { plcSection };
                break;
            }
        }

        snackbarService.SetSnackbarPresenter(SnackbarPresenter);
        navigationService.SetNavigationControl(NavigationView);
        contentDialogService.SetDialogHost(RootContentDialog);

        // NavigationView 完全加载后强制将 Gallery 设为收起
        NavigationView.Loaded += (_, _) =>
        {
            foreach (var item in ViewModel.MenuItems)
            {
                if (item is NavigationViewItem navItem && navItem.Content is string s && s == "Gallery")
                {
                    navItem.SetCurrentValue(NavigationViewItem.IsExpandedProperty, false);
                    break;
                }
            }
        };

        SetupTrayMenuEvents();
    }

    public MainWindowViewModel ViewModel { get; }

    private bool _isUserClosedPane;

    private bool _isPaneOpenedOrClosedFromCode;

    private void SetupTrayMenuEvents()
    {
        foreach (var menuItem in ViewModel.TrayMenuItems)
        {
            if (menuItem is MenuItem item)
            {
                item.Click += OnTrayMenuItemClick;
            }
        }
    }

    private void OnTrayMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Wpf.Ui.Controls.MenuItem menuItem)
        {
            return;
        }

        var tag = menuItem.Tag?.ToString() ?? string.Empty;

        Debug.WriteLine($"System Tray Click: {menuItem.Header}, Tag: {tag}");

        switch (tag)
        {
            case "tray_home":
                HandleTrayHomeClick();
                break;
            case "tray_settings":
                HandleTraySettingsClick();
                break;
            case "tray_close":
                HandleTrayCloseClick();
                break;
            default:
                if (!string.IsNullOrEmpty(tag))
                {
                    System.Diagnostics.Debug.WriteLine($"unknown Tag: {tag}");
                }

                break;
        }
    }

    private void HandleTrayHomeClick()
    {
        System.Diagnostics.Debug.WriteLine("Tray menu - Home Click");

        ShowAndActivateWindow();

        NavigateToPage(typeof(DashboardPage));
    }

    private void HandleTraySettingsClick()
    {
        System.Diagnostics.Debug.WriteLine("Tray menu - Settings Click");

        ShowAndActivateWindow();

        NavigateToPage(typeof(SettingsPage));
    }

    private static void HandleTrayCloseClick()
    {
        System.Diagnostics.Debug.WriteLine("Tray menu - Close Click");

        Application.Current.Shutdown();
    }

    private void ShowAndActivateWindow()
    {
        if (WindowState == WindowState.Minimized)
        {
            SetCurrentValue(WindowStateProperty, WindowState.Normal);
        }

        Show();
        _ = Activate();
        _ = Focus();
    }

    private void NavigateToPage(Type pageType)
    {
        try
        {
            NavigationView.Navigate(pageType);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NavigateToPage {pageType.Name} Error: {ex.Message}");
        }
    }

    private void OnNavigationSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not Wpf.Ui.Controls.NavigationView navigationView)
        {
            return;
        }

        NavigationView.SetCurrentValue(
            NavigationView.HeaderVisibilityProperty,
            navigationView.SelectedItem?.TargetPageType != typeof(DashboardPage)
                ? Visibility.Visible
                : Visibility.Collapsed
        );
    }

    private void MainWindow_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isUserClosedPane)
        {
            return;
        }

        _isPaneOpenedOrClosedFromCode = true;
        NavigationView.SetCurrentValue(NavigationView.IsPaneOpenProperty, e.NewSize.Width > 1200);
        _isPaneOpenedOrClosedFromCode = false;
    }

    private void NavigationView_OnPaneOpened(NavigationView sender, RoutedEventArgs args)
    {
        if (_isPaneOpenedOrClosedFromCode)
        {
            return;
        }

        _isUserClosedPane = false;
    }

    private void NavigationView_OnPaneClosed(NavigationView sender, RoutedEventArgs args)
    {
        if (_isPaneOpenedOrClosedFromCode)
        {
            return;
        }

        _isUserClosedPane = true;
    }

    private void OnThemeToggle(object sender, RoutedEventArgs e)
    {
        var currentTheme = Appearance.ApplicationThemeManager.GetAppTheme();
        var newTheme = currentTheme == Appearance.ApplicationTheme.Light
            ? Appearance.ApplicationTheme.Dark
            : Appearance.ApplicationTheme.Light;

        Appearance.ApplicationThemeManager.Apply(newTheme);

        // 更新图标以匹配目标主题
        ThemeToggleButton.Icon = new SymbolIcon
        {
            Symbol = newTheme == Appearance.ApplicationTheme.Dark
                ? SymbolRegular.WeatherMoon24
                : SymbolRegular.WeatherSunny24
        };

        // 持久化主题选择
        var config = App.GetRequiredService<Services.Plc.AppConfigService>();
        config.ThemeMode = newTheme == Appearance.ApplicationTheme.Dark ? "Dark" : "Light";
        config.Save();
    }
}
