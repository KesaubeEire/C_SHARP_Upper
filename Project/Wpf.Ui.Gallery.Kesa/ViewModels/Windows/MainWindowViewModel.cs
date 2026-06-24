// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Extensions.Localization;
using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.Models.Navigation;
using Wpf.Ui.Gallery.Resources;
using Wpf.Ui.Gallery.Views.Pages;
using Wpf.Ui.Gallery.Views.Pages.BasicInput;
using Wpf.Ui.Gallery.Views.Pages.Collections;
using Wpf.Ui.Gallery.Views.Pages.DateAndTime;
using Wpf.Ui.Gallery.Views.Pages.DesignGuidance;
using Wpf.Ui.Gallery.Views.Pages.DialogsAndFlyouts;
using Wpf.Ui.Gallery.Views.Pages.Layout;
using Wpf.Ui.Gallery.Views.Pages.Media;
using Wpf.Ui.Gallery.Views.Pages.Navigation;
using Wpf.Ui.Gallery.Views.Pages.OpSystem;
using Wpf.Ui.Gallery.Views.Pages.StatusAndInfo;
using Wpf.Ui.Gallery.Views.Pages.Text;
using Wpf.Ui.Gallery.Views.Pages.Windows;
using Wpf.Ui.Gallery.Views.Pages.Plc;

namespace Wpf.Ui.Gallery.ViewModels.Windows;

public partial class MainWindowViewModel(IStringLocalizer<Translations> localizer) : ViewModel
{
    [ObservableProperty]
    private string _applicationTitle = localizer["Kesa_PCL"];

    [ObservableProperty]
    private ObservableCollection<SidebarEntry> _menuItems =
    [
        new SidebarEntry
        {
            Label = "Gallery",
            Icon = new SymbolIcon { Symbol = SymbolRegular.Apps24 },
            IsExpanded = false,
            Children =
            {
                new SidebarEntry { Label = "Home", Icon = new SymbolIcon { Symbol = SymbolRegular.Home24 }, TargetPageType = typeof(DashboardPage) },
                new SidebarEntry
                {
                    Label = "Design guidance",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.DesignIdeas24 },
                    Children =
                    {
                        new SidebarEntry { Label = "Typography", Icon = new SymbolIcon { Symbol = SymbolRegular.TextFont24 }, TargetPageType = typeof(TypographyPage) },
                        new SidebarEntry { Label = "Icons", Icon = new SymbolIcon { Symbol = SymbolRegular.Diversity24 }, TargetPageType = typeof(IconsPage) },
                        new SidebarEntry { Label = "Colors", Icon = new SymbolIcon { Symbol = SymbolRegular.Color24 }, TargetPageType = typeof(ColorsPage) },
                    },
                },
                new SidebarEntry { Label = "All samples", Icon = new SymbolIcon { Symbol = SymbolRegular.List24 }, TargetPageType = typeof(AllControlsPage) },
                new SidebarEntry { IsSeparator = true },
                new SidebarEntry
                {
                    Label = "Basic Input",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.CheckboxChecked24 },
                    TargetPageType = typeof(BasicInputPage),
                    Children =
                    {
                        new SidebarEntry { Label = nameof(Anchor), TargetPageType = typeof(AnchorPage) },
                        new SidebarEntry { Label = nameof(Wpf.Ui.Controls.Button), TargetPageType = typeof(ButtonPage) },
                        new SidebarEntry { Label = nameof(DropDownButton), TargetPageType = typeof(DropDownButtonPage) },
                        new SidebarEntry { Label = nameof(HyperlinkButton), TargetPageType = typeof(HyperlinkButtonPage) },
                        new SidebarEntry { Label = nameof(ToggleButton), TargetPageType = typeof(ToggleButtonPage) },
                        new SidebarEntry { Label = nameof(ToggleSwitch), TargetPageType = typeof(ToggleSwitchPage) },
                        new SidebarEntry { Label = nameof(CheckBox), TargetPageType = typeof(CheckBoxPage) },
                        new SidebarEntry { Label = nameof(ComboBox), TargetPageType = typeof(ComboBoxPage) },
                        new SidebarEntry { Label = nameof(RadioButton), TargetPageType = typeof(RadioButtonPage) },
                        new SidebarEntry { Label = nameof(RatingControl), TargetPageType = typeof(RatingPage) },
                        new SidebarEntry { Label = nameof(ThumbRate), TargetPageType = typeof(ThumbRatePage) },
                        new SidebarEntry { Label = nameof(SplitButton), TargetPageType = typeof(SplitButtonPage) },
                        new SidebarEntry { Label = nameof(Slider), TargetPageType = typeof(SliderPage) },
                    },
                },
                new SidebarEntry
                {
                    Label = "Collections",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Table24 },
                    TargetPageType = typeof(CollectionsPage),
                    Children =
                    {
                        new SidebarEntry { Label = nameof(System.Windows.Controls.DataGrid), TargetPageType = typeof(DataGridPage) },
                        new SidebarEntry { Label = nameof(ListBox), TargetPageType = typeof(ListBoxPage) },
                        new SidebarEntry { Label = nameof(Ui.Controls.ListView), TargetPageType = typeof(ListViewPage) },
                        new SidebarEntry { Label = "TreeView", TargetPageType = typeof(TreeViewPage) },
#if DEBUG
                        new SidebarEntry { Label = "TreeList", TargetPageType = typeof(TreeListPage) },
#endif
                    },
                },
                new SidebarEntry
                {
                    Label = "Date & time",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.CalendarClock24 },
                    TargetPageType = typeof(DateAndTimePage),
                    Children =
                    {
                        new SidebarEntry { Label = nameof(CalendarDatePicker), TargetPageType = typeof(CalendarDatePickerPage) },
                        new SidebarEntry { Label = nameof(System.Windows.Controls.Calendar), TargetPageType = typeof(CalendarPage) },
                        new SidebarEntry { Label = nameof(DatePicker), TargetPageType = typeof(DatePickerPage) },
                        new SidebarEntry { Label = nameof(TimePicker), TargetPageType = typeof(TimePickerPage) },
                    },
                },
                new SidebarEntry
                {
                    Label = "Dialogs & flyouts",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Chat24 },
                    TargetPageType = typeof(DialogsAndFlyoutsPage),
                    Children =
                    {
                        new SidebarEntry { Label = nameof(Snackbar), TargetPageType = typeof(SnackbarPage) },
                        new SidebarEntry { Label = nameof(ContentDialog), TargetPageType = typeof(ContentDialogPage) },
                        new SidebarEntry { Label = nameof(Flyout), TargetPageType = typeof(FlyoutPage) },
                        new SidebarEntry { Label = nameof(Wpf.Ui.Controls.MessageBox), TargetPageType = typeof(MessageBoxPage) },
                    },
                },
#if DEBUG
                new SidebarEntry
                {
                    Label = "Layout",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.News24 },
                    TargetPageType = typeof(LayoutPage),
                    Children =
                    {
                        new SidebarEntry { Label = "Expander", TargetPageType = typeof(ExpanderPage) },
                        new SidebarEntry { Label = "CardControl", TargetPageType = typeof(CardControlPage) },
                        new SidebarEntry { Label = "CardAction", TargetPageType = typeof(CardActionPage) },
                    },
                },
#endif
                new SidebarEntry
                {
                    Label = "Media",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.PlayCircle24 },
                    TargetPageType = typeof(MediaPage),
                    Children =
                    {
                        new SidebarEntry { Label = "Image", TargetPageType = typeof(ImagePage) },
                        new SidebarEntry { Label = "Canvas", TargetPageType = typeof(CanvasPage) },
                        new SidebarEntry { Label = "WebView", TargetPageType = typeof(WebViewPage) },
                        new SidebarEntry { Label = "WebBrowser", TargetPageType = typeof(WebBrowserPage) },
                    },
                },
                new SidebarEntry
                {
                    Label = "Navigation",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Navigation24 },
                    TargetPageType = typeof(NavigationPage),
                    Children =
                    {
                        new SidebarEntry { Label = "BreadcrumbBar", TargetPageType = typeof(BreadcrumbBarPage) },
                        new SidebarEntry { Label = "NavigationView", TargetPageType = typeof(NavigationViewPage) },
                        new SidebarEntry { Label = "Menu", TargetPageType = typeof(MenuPage) },
                        new SidebarEntry { Label = "Multilevel navigation", TargetPageType = typeof(MultilevelNavigationPage) },
                        new SidebarEntry { Label = "TabControl", TargetPageType = typeof(TabControlPage) },
                    },
                },
                new SidebarEntry
                {
                    Label = "Status & info",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.ChatBubblesQuestion24 },
                    TargetPageType = typeof(StatusAndInfoPage),
                    Children =
                    {
                        new SidebarEntry { Label = "InfoBadge", TargetPageType = typeof(InfoBadgePage) },
                        new SidebarEntry { Label = "InfoBar", TargetPageType = typeof(InfoBarPage) },
                        new SidebarEntry { Label = "ProgressBar", TargetPageType = typeof(ProgressBarPage) },
                        new SidebarEntry { Label = "ProgressRing", TargetPageType = typeof(ProgressRingPage) },
                        new SidebarEntry { Label = "ToolTip", TargetPageType = typeof(ToolTipPage) },
                    },
                },
                new SidebarEntry
                {
                    Label = "Text",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.DrawText24 },
                    TargetPageType = typeof(TextPage),
                    Children =
                    {
                        new SidebarEntry { Label = nameof(AutoSuggestBox), TargetPageType = typeof(AutoSuggestBoxPage) },
                        new SidebarEntry { Label = nameof(NumberBox), TargetPageType = typeof(NumberBoxPage) },
                        new SidebarEntry { Label = nameof(Wpf.Ui.Controls.PasswordBox), TargetPageType = typeof(PasswordBoxPage) },
                        new SidebarEntry { Label = nameof(Wpf.Ui.Controls.RichTextBox), TargetPageType = typeof(RichTextBoxPage) },
                        new SidebarEntry { Label = nameof(Label), TargetPageType = typeof(LabelPage) },
                        new SidebarEntry { Label = nameof(Wpf.Ui.Controls.TextBlock), TargetPageType = typeof(TextBlockPage) },
                        new SidebarEntry { Label = nameof(Wpf.Ui.Controls.TextBox), TargetPageType = typeof(TextBoxPage) },
                    },
                },
                new SidebarEntry
                {
                    Label = "System",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Desktop24 },
                    TargetPageType = typeof(OpSystemPage),
                    Children =
                    {
                        new SidebarEntry { Label = "Clipboard", TargetPageType = typeof(ClipboardPage) },
                        new SidebarEntry { Label = "FilePicker", TargetPageType = typeof(FilePickerPage) },
                    },
                },
                new SidebarEntry { Label = "Windows", Icon = new SymbolIcon { Symbol = SymbolRegular.WindowApps24 }, TargetPageType = typeof(WindowsPage) },
            },
        },
        new SidebarEntry { IsSeparator = true },
        new SidebarEntry()
        {
            Label = "PLC 连接",
            Icon = new SymbolIcon { Symbol = SymbolRegular.PlugDisconnected24 },
            // CustomContent 由代码后置注入 PpeConnectionSection 面板
        },
        new SidebarEntry
        {
            Label = "PLC 监视模块",
            Icon = new SymbolIcon { Symbol = SymbolRegular.Desktop24 },
            TargetPageType = typeof(IoMonitorPage),
            Children =
            {
                new SidebarEntry { Label = "I/Q/M 监控", Icon = new SymbolIcon { Symbol = SymbolRegular.List24 }, TargetPageType = typeof(IoMonitorPage) },
                new SidebarEntry { Label = "趋势图", Icon = new SymbolIcon { Symbol = SymbolRegular.ChartMultiple24 }, TargetPageType = typeof(TrendChartPage) },
                new SidebarEntry { Label = "仪表盘", Icon = new SymbolIcon { Symbol = SymbolRegular.Gauge24 }, TargetPageType = typeof(GaugeDashboardPage) },
                new SidebarEntry { Label = "DB 块", Icon = new SymbolIcon { Symbol = SymbolRegular.Box24 }, TargetPageType = typeof(DbMonitorPage) },
                new SidebarEntry { Label = "报警管理", Icon = new SymbolIcon { Symbol = SymbolRegular.Alert24 }, TargetPageType = typeof(AlarmPage) },
                new SidebarEntry { Label = "图库画廊", Icon = new SymbolIcon { Symbol = SymbolRegular.ChartMultiple24 }, TargetPageType = typeof(LvcGalleryPage) },
                new SidebarEntry { Label = "配方管理", Icon = new SymbolIcon { Symbol = SymbolRegular.DocumentData24 }, TargetPageType = typeof(RecipePage) },
            },
        },
    ];

    [ObservableProperty]
    private ObservableCollection<SidebarEntry> _footerMenuItems =
    [
        new SidebarEntry { Label = "Settings", Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 }, TargetPageType = typeof(SettingsPage) },
    ];

    [ObservableProperty]
    private ObservableCollection<Control> _trayMenuItems =
    [
        new Wpf.Ui.Controls.MenuItem()
        {
            Header = "Home",
            Tag = "tray_home",
            Icon = new SymbolIcon { Symbol = SymbolRegular.Home24 },
        },
        new Wpf.Ui.Controls.MenuItem()
        {
            Header = "Settings",
            Tag = "tray_settings",
            Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
        },
        new Separator(),
        new Wpf.Ui.Controls.MenuItem()
        {
            Header = "Close",
            Tag = "tray_close",
            Icon = new SymbolIcon { Symbol = SymbolRegular.Dismiss24 },
        },
    ];
}
