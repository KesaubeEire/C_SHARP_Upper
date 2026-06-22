# WPF_S71200_SAMPLE — 产品上下文

> 每次会话启动时注入，确保工作始终从产品愿景出发。
> 编辑此文件以更改注入内容。

**WPF_S71200_SAMPLE** 是 C# WPF 上位机项目，用于 Siemens S7-1200 PLC 的实时监控与控制。目标是实现工业现场级的 I/Q/M/DB 变量监控、实时趋势、仪表盘可视化。

## 技术栈

- **框架**: WPF (.NET 10)
- **通信**: Sharp7（S7 协议）
- **图表**: LiveChartsCore v2.0 (SkiaSharp)
- **UI 主题**: MaterialDesignInXaml v5.3
- **测试**: xunit + coverlet

## 核心架构

- **S7Service**: Sharp7 S7Client 线程安全封装（lock 保护所有 Read/Write）
- **PollingScheduler**: 定时轮询 I/Q/M/DB，Timer + `AutoReset=false` + `_busy` 标志防重叠
- **VariableMonitor**: 高频 DB 变量读取（100ms 间隔），驱动趋势图和仪表盘
- **数据流**: Timer tick → S7 Read → `DataUpdated` event → `Dispatcher.InvokeAsync` → UI 绑定更新
- **配置持久化**: `AppConfig.Load/Save`，JSON 文件 `app_config.json`

## UI 布局

```
┌─────────────────────────────────────────────────┐
│  Sidebar (连接 + 轮询 + 导入)  │  TabControl     │
│                                │  ├─ I/Q/M 手动读取│
│                                │  ├─ 趋势图 (Live)│
│                                │  ├─ 仪表盘       │
│                                │  └─ DB 变量浏览  │
└─────────────────────────────────────────────────┘
```

## 设计约束

- **所有 UI 控件在 XAML 声明**，代码后置绑定事件
- **无 DI 容器**，服务通过 `Init(service)` 方法手动注入 Panel 控件
- **WPF 本地开发**，Windows Only
- **CS1591 抑制**：不要求每个成员都有 XML 文档注释
- **TreatWarningsAsErrors**: 所有代码风格警告视为错误
- **私有字段**: `_camelCase` 前缀
