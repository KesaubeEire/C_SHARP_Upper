# WPF 项目编码约定（Kesa_PLC_TEST）

## XAML

- 控件统一使用 `x:Name` (camelCase)，代码后置通过名称直接引用
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
