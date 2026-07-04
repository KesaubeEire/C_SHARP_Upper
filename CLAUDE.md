# VS_Dev 项目说明

## 项目概述

这是一个 **C# 上位机+WPF 控件展示**综合学习/实战项目仓库，包含多个独立子项目。

## 目录结构

```
VS_Dev/
├── CLAUDE.md                          ← 本文件
│
├── Project/WpfScada/                  ← 主项目（WPF SCADA 上位机 + WPF-UI 控件画廊）
│   ├── WpfScada.sln                   ← 解决方案
│   ├── WpfScada.csproj                ← 项目文件（net10.0-windows, WPF）
│   ├── App.xaml / App.xaml.cs         ← DI Host + 主题
│   ├── Views/…                        ← 页面（PLC 监控 + 控件画廊 50+ 页面）
│   ├── ViewModels/…                   ← MVVM ViewModel
│   ├── Services/Plc/                  ← PLC 服务
│   │   ├── S7Service.cs              ← Sharp7 封装（西门子 S7）
│   │   ├── PollingScheduler.cs       ← 双路径轮询引擎
│   │   ├── AlarmService.cs           ← 报警管理（ISA 18.2）
│   │   ├── RecipeService.cs          ← 配方管理 + 版本历史
│   │   └── Modbus/…                  ← Modbus RTU/TCP 协议栈
│   ├── Controls/…                     ← 自定义 UserControl
│   ├── Models/…                       ← 数据模型
│   └── Helpers/…                      ← Converters
│
├── Project/WinForms_References/       ← WinForms Modbus 上位机（参考保留）
│   ├── Modbus/                        ← Modbus 协议+传输+轮询
│   ├── Alarm/ Recipe/ Report/         ← 报警/配方/报表
│   ├── Storage/                       ← 数据库抽象（SQLite+SQL Server）
│   ├── Core/                          ← EventBus + 数据模型
│   ├── Forms/                         ← WinForms UI
│   └── CSharpDemos/                   ← C# 语法教程
│
├── Project/wpfui/                     ← lepoco/wpfui 官方库（clone，仅供查阅）
│
├── ModbusForm代码讲解_面试深度.html     ← 面试文档
├── ModbusForm代码面试级解析.html
├── Modbus_上位机_面试题库.html
└── WinForms上位机开发最佳实践_2024-2026.html
```

## 技术栈

### WpfScada（主项目）
- **框架**: .NET 10.0-windows, WPF
- **UI**: WPF-UI 4.3.0 (Fluent Design), NuGet 包引用
- **MVVM**: CommunityToolkit.Mvvm（源生成器）
- **DI**: Microsoft.Extensions.Hosting
- **PLC 通信**: Sharp7 (S7) + 自研 Modbus 协议栈
- **图表**: LiveChartsCore.SkiaSharpView 2.0.5 + SkiaSharp
- **i18n**: Lepo.i18n

## 架构设计要点（WpfScada）

### 分层架构
- **Services**: 无 UI 依赖的纯服务层（S7Service, PollingScheduler, AlarmService, RecipeService, Modbus 协议栈）
- **ViewModels**: 继承 ViewModel 基类，注入服务，使用 `[ObservableProperty]` + `[RelayCommand]`
- **Views**: 通过 DI 注入 ViewModel，XAML 绑定，code-behind 只放 UI 行为
- **Modbus**: 独立协议栈，Protocol / Transport / PollingService 三层

### DI 注册（App.xaml.cs）
```csharp
services.AddSingleton<S7Service>();
services.AddSingleton<AlarmService>();
services.AddSingleton<ModbusPage>();
services.AddSingleton<ModbusViewModel>();
// 页面通过 AddTransientFromNamespace 批量注册
```

## 协作规则（重要）

- **不自动 commit**：改完代码先给用户检查
- **不自动 push**：未经允许绝不推送到任何远程
- **git 操作前先问**：commit、push、merge、branch 必须用户明确指令

## 关键注意事项

### C# / .NET 约定
- 目标框架 net10.0-windows，Nullable + ImplicitUsings 已开启
- 私有字段 `_camelCase`，类型 PascalCase
- 所有 ViewModel 继承项目的 `ViewModel` 基类
- 禁止硬编码颜色，必须使用 `DynamicResource`
- XAML 使用 `ui:` 前缀覆盖 WPF-UI 控件
- 颜色/画刷使用 `DynamicResource` 以支持运行时主题切换

### PowerShell 5.1 限制
- 不支持 `&&` / `||` 管道链、三元运算符、null 合并
- 默认编码 UTF-16 LE，写文件给其他工具读用 `-Encoding utf8`

### Git 规范
- 主分支: `master`
- 提交格式: `@ <描述> @`（用户自定义格式）

## 面试文档

根目录的 4 份 HTML 文档是配套学习资料。

## WpfScada 子项目约定

在该目录下工作时，`.claude/rules/` 下的规则文件会**自动加载**：
- `csharp-conventions.md` — C# 编码规范
- `wpf-conventions.md` — WPF 项目约定
- `wpfui-official-conventions.md` — WPF-UI 库官方规范
