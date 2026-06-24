# 侧栏 Gallery 改用 TreeView 改造记录

## 目标

解决 WPF UI `NavigationView` 自带的 `NavigationViewItem` 侧栏菜单**仅支持二级**层级的问题，改用原生 `TreeView` 实现**无限层级**的导航菜单。

## 最终方案

**混合布局：** 保留 WPF UI `NavigationView` 作为页面内容容器（后退按钮、面包屑导航、页面切换动画、`INavigationService` 兼容），用原生 `TreeView` 替换其左侧菜单面板。

### 布局结构

```
Grid
├─ Column 0 (310px): [TreeView 侧栏]
├─ Column 1 (*):      [NavigationView (IsPaneVisible=False)]
│
├─ TreeView            → Grid.Column="0"
├─ NavigationView      → Grid.Column="1"   ← pane 完全隐藏，只渲染页面内容
├─ TitleBar            → Grid.ColumnSpan="2" (窗口标题栏全宽)
├─ NotifyIcon          → Grid.ColumnSpan="2"
└─ ContentDialogHost   → Grid.ColumnSpan="2"
```

### 关键特性

- **支持无限层级：** `SidebarEntry.Children` 可嵌套任意深度，TreeView 原生递归渲染
- **WPF UI 主题适配：** 复用 `TreeViewItemBackground` / `TreeViewItemSelectionIndicatorForeground` 等主题资源，深浅色自动切换
- **NavigationView 功能完整保留：** 后退按钮、页面过渡动画 (FadeInWithSlide)、面包屑同步、搜索框、托盘菜单全部可用
- **`INavigationService` 兼容：** 其他页面通过 `INavigationService.Navigate()` 导航不受影响

## 变更的文件

| 文件 | 操作 | 说明 |
|------|------|------|
| `Models/Navigation/SidebarEntry.cs` | **新建** | 树形导航数据模型，`[ObservableProperty]` 源生成器 |
| `Controls/SidebarTemplateSelector.cs` | **新建** | `DataTemplateSelector`，区分叶子/分隔线/自定义内容模板 |
| `ViewModels/Windows/MainWindowViewModel.cs` | **修改** | `_menuItems` 从 `NavigationViewItem[]` 改为 `SidebarEntry[]` |
| `Views/Windows/MainWindow.xaml` | **修改** | Grid 双列布局：TreeView 侧栏 + NavigationView 内容容器 |
| `Views/Windows/MainWindow.xaml.cs` | **修改** | TreeView 选中→导航、PLC 面板注入、窗口尺寸切换侧栏 |

## 技术细节

### SidebarEntry 模型 (`Models/Navigation/SidebarEntry.cs`)

```csharp
public partial class SidebarEntry : ObservableObject
{
    [ObservableProperty] string _label;
    [ObservableProperty] IconElement? _icon;
    [ObservableProperty] Type? _targetPageType;
    [ObservableProperty] ObservableCollection<SidebarEntry> _children;
    [ObservableProperty] bool _isExpanded;
    [ObservableProperty] bool _isSeparator;
    [ObservableProperty] bool _isActive;
    [ObservableProperty] object? _customContent;  // 用于 PLC 连接面板
}
```

### 导航流程

```
TreeView.SelectedItemChanged
  → OnSidebarTreeViewSelectedItemChanged()
  → 递归清除所有条目 IsActive
  → 设置选中条目 IsActive = true
  → NavigationView.Navigate(TargetPageType)
  → NavigationView 接管：页面实例化、过渡动画、面包屑同步、导航栈
```

### 窗口尺寸自适应

窗口宽度 >1200px 时展开侧栏 (310px)，≤1200px 时折叠 (0px)：

```csharp
private void MainWindow_OnSizeChanged(...)
{
    SidebarColumn.Width = e.NewSize.Width > 1200
        ? new GridLength(310) : new GridLength(0);
}
```

### PLC 连接面板

从 DI 获取 `PpeConnectionSection` 实例，注入到"PLC 连接"条目的 `CustomContent` 属性。TreeView 的模板选择器检测到 `CustomContent != null` 时使用 `CustomContentSidebarTemplate` 渲染自定义内容。

## 未修改的文件

- `App.xaml.cs` — DI 注册不变，`NavigationService` 仍指向 `NavigationView`
- `Services/ApplicationHostService.cs` — `mainWindow.NavigationView.Navigate()` 仍有效
- 所有 Page / ViewModel — 不受影响，仍通过 `INavigationService` 导航
- `Controls/Sidebar/PpeConnectionSection.xaml(.cs)` — PLC 面板代码不变
