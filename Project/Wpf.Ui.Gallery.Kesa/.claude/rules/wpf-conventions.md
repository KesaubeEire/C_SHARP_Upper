# WPF 项目编码约定（Kesa_PLC_TEST）

## XAML

- 控件用 `x:Name` 仅用于：presenter 绑定（SnackbarPresenter）、控件自身事件挂载、Storyboard 动画引用。数据驱动请走 Binding + ViewModel
- 样式使用 Wpf.Ui 提供的主题资源，不额外定义全局样式
- 资源键使用 PascalCase（如 `AccentBlue`, `TextPrimary`）
- Converter 集中在 `Helpers/` 下定义
- 颜色/画刷使用 `DynamicResource` 以支持运行时主题切换
- 合并字典顺序：Wpf.Ui 默认 → 自定义覆盖 → 页面级资源

## C# 后端（code-behind）

- 私有字段：`_camelCase` 前缀
- 事件处理方法：`On<EventName>` 命名（如 `OnConnect`, `OnSendClick`）
- 跨线程 UI 更新通过 `Dispatcher.InvokeAsync`
- 不使用异步 `async void` 事件处理程序，除 UI 事件处理外
- 避免在构造函数中执行耗时操作，改为 `OnInitialized` 或 `Loaded` 事件

## 服务层（Wpf.Ui 风格）

- 使用 `Microsoft.Extensions.Hosting` DI 容器注册服务
- ViewModel 通过构造函数注入服务
- 页面通过 Wpf.Ui 导航系统注册，使用 `INavigationService` 导航
- 服务间通信通过事件或 `CommunityToolkit.Mvvm` 的 `WeakReferenceMessenger`

## MVVM 模式

- 使用 `CommunityToolkit.Mvvm` 源生成器（`[ObservableProperty]`, `[RelayCommand]`）
- ViewModel 继承 `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`  
  或 Wpf.Ui 的 `ViewModel` 基类
- View 通过 DataContext 绑定 ViewModel（DI 自动注入）
- 避免在 ViewModel 中直接操作 UI 控件

## ViewModel 基类约定

- 所有 ViewModel **必须**继承项目的 `ViewModel` 基类，**不要**直接继承 `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`
- 例外：纯数据模型（如 `RecipeParameter`）可直接继承 `ObservableObject`，但必须用 `[ObservableProperty]` 源生成器，不要手工实现 `INotifyPropertyChanged`

## Model 类 INPC

- 需要在 UI 中编辑的 Model 类标记 `partial`，继承 `ObservableObject`，用 `[ObservableProperty]` 生成属性
- **禁止**手工实现 `INotifyPropertyChanged`（冗余代码，且容易遗漏属性通知）
- **禁止**属性"半 INPC"（部分通知、部分不通知） — 要么全通知，要么是不可变 DTO

## 主题适配

- 代码中**禁止**硬编码颜色值（`new SolidColorBrush(Color.FromRgb(...))`、`Fill="#XXXXXX"`）
- 状态色使用 `Application.Current.FindResource("SystemFillColorSuccessBrush")` / `"SystemFillColorCriticalBrush"` / `"SystemFillColorNeutralBrush"`
- XAML 中颜色必须使用 `DynamicResource` 以支持运行时主题切换
- 需要自定义颜色时，在 XAML 中定义为 `DynamicResource`，代码中通过 `FindResource` 获取

## Code-behind 界限

- Code-behind 只放：初始化设置（presenter 绑定、事件挂载）、纯 UI 行为（窗口尺寸响应、拖拽）
- **严禁**在 code-behind 中操作 `x:Name` 控件属性（`btn.Visibility = ...`、`indicator.Fill = ...`）
- 业务逻辑（连接/断开/数据处理/状态变更）一律放 ViewModel 或 Service
- 复杂 UserControl（如 Sidebar 面板）**必须**有对应的 ViewModel
