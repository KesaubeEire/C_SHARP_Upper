# Kesa_PLC_TEST — 产品上下文

> 每次会话启动时注入，确保工作始终从产品愿景出发。
> 编辑此文件以更改注入内容。

**Kesa_PLC_TEST** 是基于 Wpf.Ui 组件库（NuGet 4.3.0）的 WPF 上位机测试项目，用于西门子 PLC (S7) 的调试与监控。基于 Wpf.Ui.Gallery 裁剪改造，保留其现代化 UI 框架，替换业务逻辑为 S7 通信 + Modbus。

## 技术栈

- **框架**: WPF (.NET 10) + Wpf.Ui 组件库（NuGet 4.3.0）
- **通信**: S7 协议（Sharp7 库） + Modbus RTU/TCP（`System.IO.Ports` + 自定义协议层）
- **UI 主题**: Wpf.Ui (Fluent Design 风格)
- **DI 容器**: Microsoft.Extensions.Hosting
- **MVVM**: CommunityToolkit.Mvvm（源生成器）
- **全局状态**: PollingStore（ObservableObject 单例，取代事件广播）

## 架构

- **ModbusProtocol**: 纯协议逻辑 — 帧组装/解析、CRC 计算
- **ModbusTransport**: 传输层抽象 — 串口/TCP 连接管理、收发字节流
- **S7Service**: 应用层服务 — 封装西门子 S7 读/写操作
- **PollingScheduler**: 定时轮询引擎 — 写入 PollingStore（响应式状态）
- **PollingStore**: 全局响应式状态（ObservableObject） — XAML 直接绑定，零 C# 同步代码
- **AlarmService**: 报警服务 — 死区/延迟/搁置/持久化/规则管理
- **Views & ViewModels**: 基于 Wpf.Ui 的导航页面，通过 DI 注入服务

## UI 布局

Wpf.Ui 的导航式布局，左侧导航栏 + 右侧页面区域。主窗口在 MainWindow.xaml 中定义，各页面在 Views/Pages/ 下。

## 设计约束

- **WPF 本地开发**，Windows Only
- **CS1591 抑制**：不要求每个成员都有 XML 文档注释
- **TreatWarningsAsErrors**: 所有代码风格警告视为错误
- **私有字段**: `_camelCase` 前缀
- **namespace 沿用 `Wpf.Ui.Gallery`**（原始项目命名空间，后续可改）
