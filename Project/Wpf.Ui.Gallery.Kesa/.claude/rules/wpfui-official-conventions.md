# WPF UI 库官方规范摘要

> 来源：[WPF UI 官方文档](https://wpfui.lepo.co/documentation/getting-started.html)
> 最后同步：2026-06-26（NuGet 包引用模式）

## 依赖管理（2026-06-26 更新）

Wpf.Ui 通过 **NuGet 包引用** 引入，不再依赖本地源码编译：

```xml
<ItemGroup>
  <PackageReference Include="WPF-UI" Version="4.3.0" />
  <PackageReference Include="WPF-UI.DependencyInjection" Version="4.3.0" />
  <PackageReference Include="WPF-UI.Tray" Version="4.3.0" />
</ItemGroup>
```

### 注意事项

- `WPF-UI.SyntaxHighlight` 和 `WPF-UI.ToastNotifications` **不发布到 NuGet**
- 这些子包的功能已被移除或替换为原生控件实现
- 本地 `Project/wpfui/` 源码目录以 git clone (depth 1) 形式保留，仅供查阅，**不参与构建**
- 框架目标 `net10.0-windows`，与 WPF-UI 4.3.0 兼容

## App.xaml — 字典配置

```xml
<!-- 标准模板 -->
<Application xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ui:ThemesDictionary Theme="Dark" />
        <ui:ControlsDictionary />
        <!-- 子控件字典在 Wpf.Ui 默认之后加载 -->
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

### 合并顺序规则

1. `ui:ThemesDictionary`（主题画刷/颜色/字体）
2. `ui:ControlsDictionary`（控件默认样式）
3. 自定义覆盖（ControlExample、TypographyControl 等）
4. Converters / 页面级资源

## 主题系统（ApplicationThemeManager）

```csharp
using Wpf.Ui.Appearance;

// 启动时应用主题
ApplicationThemeManager.Apply(ApplicationTheme.Dark);

// 运行时切换
ApplicationThemeManager.Apply(
    ApplicationTheme.Light,
    WindowBackdropType.Mica   // FluentWindow 必须用 WindowBackdropType
);

// 获取当前主题
ApplicationTheme currentAppTheme = ApplicationThemeManager.GetAppTheme();

// 监听系统主题变化
SystemThemeWatcher.Watch(this);  // 在 FluentWindow 构造函数中调用
```

| 枚举值 | 含义 |
|--------|------|
| `ApplicationTheme.Light` | 浅色主题 |
| `ApplicationTheme.Dark` | 深色主题 |
| `ApplicationTheme.HighContrast` | 跟随 Windows 高对比度 |

### 主题适配硬性要求

> **"For theme changes to apply correctly, your colors and brushes should be referenced as `DynamicResource`."**  
> — WPF UI 官方文档

- XAML：必须 `{DynamicResource ...}`，不是 `{StaticResource ...}`
- C# 代码后置：必须 `Application.Current.FindResource("...")`，不是 `new SolidColorBrush(...)`

## 窗口 — FluentWindow

```xml
<ui:FluentWindow
    WindowBackdropType="Mica"
    WindowCornerPreference="Default"
    ExtendsContentIntoTitleBar="True">
  <!-- 内容 -->
</ui:FluentWindow>
```

窗口类**必须**使用 `ui:FluentWindow`，不是原生 `System.Windows.Window`。

### TitleBar 集成

```xml
<ui:TitleBar x:Name="TitleBar" Title="App Name">
  <ui:TitleBar.Icon>
    <ui:ImageIcon Source="pack://application:,,,/Assets/icon.png" />
  </ui:TitleBar.Icon>
</ui:TitleBar>
```

NavigationView 的 `TitleBar` 属性绑定到 TitleBar 的 x:Name。

## 导航系统 — NavigationView

```xml
<ui:NavigationView
    x:Name="NavigationView"
    MenuItemsSource="{Binding ViewModel.MenuItems}"
    BreadcrumbBar="{Binding ElementName=BreadcrumbBar}"
    PaneDisplayMode="Left"
    OpenPaneLength="310">
  <ui:NavigationView.Header>
    <ui:BreadcrumbBar x:Name="BreadcrumbBar" />
  </ui:NavigationView.Header>
  <ui:NavigationView.ContentOverlay>
    <ui:SnackbarPresenter x:Name="SnackbarPresenter" />
  </ui:NavigationView.ContentOverlay>
</ui:NavigationView>
```

### 导航注册（App.xaml.cs）

```csharp
// 必须的 3 个导航核心服务
services.AddNavigationViewPageProvider();  // ← 关键
services.AddSingleton<INavigationService, NavigationService>();
services.AddSingleton<ISnackbarService, SnackbarService>();
services.AddSingleton<IContentDialogService, ContentDialogService>();

// 页面注册
services.AddSingleton<DashboardPage>();
services.AddSingleton<DashboardViewModel>();

// 批量注册
services.AddTransientFromNamespace("Wpf.Ui.Gallery.Views", GalleryAssembly.Asssembly);
services.AddTransientFromNamespace("Wpf.Ui.Gallery.ViewModels", GalleryAssembly.Asssembly);
```

## 控件使用优先级

WPF UI 提供了增强版原生控件，应优先使用：

| 原生 WPF 控件 | 使用 WPF UI 替代 |
|-------------|----------------|
| `System.Windows.Controls.Button` | 直接使用，WPF UI 通过字典覆盖样式 |
| `System.Windows.Controls.TextBox` | `ui:TextBox` — 支持占位文本 |
| `System.Windows.Controls.TextBlock` | `ui:TextBlock` — 支持 `FontTypography` |
| `System.Windows.Controls.PasswordBox` | `ui:PasswordBox` |
| `System.Windows.Controls.ComboBox` | 直接使用，WPF UI 自动覆盖样式 |
| `System.Windows.Controls.ListBox` | `ui:ListView` |
| `System.Windows.Controls.DataGrid` | WPF UI DataGrid（如果可用） |

> 原生控件虽然被 WPF UI 自动覆盖了部分样式，但 `ui:` 前缀控件额外提供了
> `FontTypography`、`PlaceholderText`、`Icon` 等属性。

## 依赖注入约定

```csharp
// Host 构建
private static readonly IHost _host = Host.CreateDefaultBuilder()
    .ConfigureServices((_, services) =>
    {
        // 导航核心服务（单例）
        services.AddNavigationViewPageProvider();
        services.AddHostedService<ApplicationHostService>();

        // 窗口 & 导航
        services.AddSingleton<IWindow, MainWindow>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<INavigationService, NavigationService>();

        // 页面（单例，保持状态）
        services.AddSingleton<DashboardPage>();
        services.AddSingleton<DashboardViewModel>();

        // 面板（瞬态，每次新实例）
        services.AddTransient<PpeConnectionSection>();

        // 批量：自动发现
        services.AddTransientFromNamespace("Wpf.Ui.Gallery.Views", Assembly.Asssembly);
    })
    .Build();
```

| 生命周期 | 场景 |
|---------|------|
| `Singleton` | 页面、导航服务、PLC 服务 |
| `Transient` | UserControl 面板、轻量组件 |

## 目录结构（WPF UI 推荐）

```
Project/
├── App.xaml / App.xaml.cs          ← 主题字典 + DI Host
├── Views/
│   ├── Windows/MainWindow.xaml     ← FluentWindow
│   └── Pages/                      ← 页面
├── ViewModels/
│   ├── Windows/MainWindowViewModel.cs
│   └── Pages/                      ← ViewModel（对应 Views/Pages 结构）
├── Controls/                       ← 自定义 UserControl
├── Services/                       ← 服务层
├── Models/                         ← 数据模型
└── Helpers/                        ← Converters、工具函数
```

## 常用控件速查

| 控件 | 命名空间 | 关键属性 |
|------|---------|---------|
| `ui:FluentWindow` | `Wpf.Ui.Controls` | `WindowBackdropType`, `ExtendsContentIntoTitleBar` |
| `ui:NavigationView` | `Wpf.Ui.Controls` | `MenuItemsSource`, `BreadcrumbBar`, `PaneDisplayMode` |
| `ui:TitleBar` | `Wpf.Ui.Controls` | `Title`, `Icon` |
| `ui:Button` | `Wpf.Ui.Controls` | `Appearance="Primary\|Secondary\|Success\|Danger"`, `Icon` |
| `ui:TextBox` | `Wpf.Ui.Controls` | `PlaceholderText` |
| `ui:TextBlock` | `Wpf.Ui.Controls` | `FontTypography="Title\|Subtitle\|Body\|BodyStrong\|Caption"` |
| `ui:CardControl` | `Wpf.Ui.Controls` | `Icon`, `Header` |
| `ui:CardAction` | `Wpf.Ui.Controls` | `IsChevronVisible`, `Command` |
| `ui:CardExpander` | `Wpf.Ui.Controls` | `ContentPadding`, `Icon` |
| `ui:AutoSuggestBox` | `Wpf.Ui.Controls` | `PlaceholderText`, `Icon` |
| `ui:BreadcrumbBar` | `Wpf.Ui.Controls` | — |
| `ui:SnackbarPresenter` | `Wpf.Ui.Controls` | — |
| `ui:ContentDialogHost` | `Wpf.Ui.Controls` | — |
| `ui:SymbolIcon` | `Wpf.Ui.Controls` | `Symbol="SymbolRegular.xxx"` |
| `ui:ImageIcon` | `Wpf.Ui.Controls` | `Source="pack://..."` |
| `ui:Anchor` | `Wpf.Ui.Controls` | `NavigateUri` |
| `ui:HyperlinkButton` | `Wpf.Ui.Controls` | `NavigateUri` |
