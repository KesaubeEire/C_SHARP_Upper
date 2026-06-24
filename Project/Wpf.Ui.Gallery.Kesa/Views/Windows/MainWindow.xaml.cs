// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.Controls.Sidebar;
using Wpf.Ui.Gallery.Models.Navigation;
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

        // 创建 PLC 连接面板并注入到 "PLC 连接" 的 CustomContent
        var plcSection = serviceProvider.GetRequiredService<PpeConnectionSection>();
        foreach (var entry in ViewModel.MenuItems)
        {
            if (entry is SidebarEntry { Label: "PLC 连接" } plcEntry)
            {
                plcEntry.CustomContent = plcSection;
                break;
            }
        }

        snackbarService.SetSnackbarPresenter(SnackbarPresenter);
        navigationService.SetNavigationControl(NavigationView);
        contentDialogService.SetDialogHost(RootContentDialog);

        // NavigationView 完全加载后强制将 Gallery 设为收起
        NavigationView.Loaded += (_, _) =>
        {
            foreach (var entry in ViewModel.MenuItems)
            {
                if (entry is SidebarEntry { Label: "Gallery" } galleryEntry)
                {
                    galleryEntry.IsExpanded = false;
                    break;
                }
            }
        };

        SetupTrayMenuEvents();
    }

    public MainWindowViewModel ViewModel { get; }

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
            return;

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
                break;
        }
    }

    private void HandleTrayHomeClick()
    {
        ShowAndActivateWindow();
        NavigateToPage(typeof(DashboardPage));
    }

    private void HandleTraySettingsClick()
    {
        ShowAndActivateWindow();
        NavigateToPage(typeof(SettingsPage));
    }

    private static void HandleTrayCloseClick()
    {
        Application.Current.Shutdown();
    }

    private void ShowAndActivateWindow()
    {
        if (WindowState == WindowState.Minimized)
            SetCurrentValue(WindowStateProperty, WindowState.Normal);
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
            Debug.WriteLine($"NavigateToPage {pageType.Name} Error: {ex.Message}");
        }
    }

    /// <summary>
    /// TreeView 选择变更 → 导航到对应页面
    /// </summary>
    private void OnSidebarTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not SidebarEntry entry)
            return;
        if (entry.TargetPageType is null)
            return;

        UpdateActiveState(entry);
        NavigateToPage(entry.TargetPageType);
    }

    /// <summary>
    /// 递归清除所有条目的 IsActive，然后将指定的 entry 设为活跃
    /// </summary>
    private void UpdateActiveState(SidebarEntry activeEntry)
    {
        foreach (var entry in ViewModel.MenuItems)
            SetInactiveRecursive(entry);
        foreach (var entry in ViewModel.FooterMenuItems)
            SetInactiveRecursive(entry);
        activeEntry.IsActive = true;
    }

    private static void SetInactiveRecursive(SidebarEntry entry)
    {
        entry.IsActive = false;
        foreach (var child in entry.Children)
            SetInactiveRecursive(child);
    }

    private void OnNavigationSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not NavigationView navigationView)
            return;

        NavigationView.SetCurrentValue(
            NavigationView.HeaderVisibilityProperty,
            navigationView.SelectedItem?.TargetPageType != typeof(DashboardPage)
                ? Visibility.Visible
                : Visibility.Collapsed
        );
    }

    /// <summary>
    /// 窗口宽度 >1200 时展开侧栏，否则折叠
    /// </summary>
    private void MainWindow_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        SidebarColumn.Width = e.NewSize.Width > 1200
            ? new GridLength(310)
            : new GridLength(0);
    }

    private void OnThemeToggle(object sender, RoutedEventArgs e)
    {
        var currentTheme = Appearance.ApplicationThemeManager.GetAppTheme();
        var newTheme = currentTheme == Appearance.ApplicationTheme.Light
            ? Appearance.ApplicationTheme.Dark
            : Appearance.ApplicationTheme.Light;

        Appearance.ApplicationThemeManager.Apply(newTheme);

        ThemeToggleButton.Icon = new SymbolIcon
        {
            Symbol = newTheme == Appearance.ApplicationTheme.Dark
                ? SymbolRegular.WeatherMoon24
                : SymbolRegular.WeatherSunny24
        };

        var config = App.GetRequiredService<Services.Plc.AppConfigService>();
        config.ThemeMode = newTheme == Appearance.ApplicationTheme.Dark ? "Dark" : "Light";
        config.Save();
    }
}
