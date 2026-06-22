# WPF 项目编码约定

## XAML

- 控件统一使用 `x:Name` (camelCase)，代码后置通过名称直接引用
- 样式定义在 `Styles.xaml` / `Themes/*.xaml`，不内联
- 资源键使用 PascalCase（如 `AccentBlue`, `TextPrimary`）
- Converter 集中定义在 `Converters/Converters.cs`
- 颜色/画刷使用 `DynamicResource` 以支持运行时主题切换
- 合并字典顺序：MaterialDesign 默认 → 基础主题 → 自定义主题覆盖 → 全局样式

## C# 后端（code-behind）

- 私有字段：`_camelCase` 前缀
- 事件处理方法：`On<EventName>` 命名（如 `OnConnect`, `OnPollStart`）
- Panel 控件通过 `Init(service)` 方法注入依赖（本项目无 DI 容器）
- 跨线程 UI 更新通过 `Dispatcher.InvokeAsync`
- 不使用异步 `async void` 事件处理程序，除 UI 事件处理外。
- 避免在构造函数中执行耗时操作，改为单独的 `Init`/`Load` 方法

## 服务层

- `S7Service` 是单例共享，所有 Read/Write 需考虑线程安全（使用 `lock`）
- `PollingScheduler` 和 `VariableMonitor` 使用 Timer（`AutoReset=false`） + `_busy` 标志防止重入
- 配置持久化使用 `AppConfig.Load/Save`（JSON 文件 `app_config.json`）
- 服务间通信通过 .NET 事件 (`event EventHandler<T>`) 而非直接方法调用
