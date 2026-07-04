# WpfScada

基于 Wpf.Ui (NuGet 4.3.0) 的 WPF 上位机测试项目，用于西门子 S7 PLC 调试与监控。

## 数据持久化

所有运行时数据存储在 **Roaming AppData** 目录下：

```
%APPDATA%\WpfScada\
→ C:\Users\<user>\AppData\Roaming\WpfScada\
```

| 文件/目录 | 用途 |
|-----------|------|
| `recipes/` | 配方数据（JSON） |
| `recipes/_versions/` | 配方版本历史快照 |
| `kesa_config.json` | 应用配置（IP、端口、窗口位置等） |
| `alarms.json` | 报警持久化记录 |
| `rules.json` | 轮询规则 |
| `default-rules.json` | 默认轮询规则 |

> 不依赖 `bin/` 目录，`dotnet clean` / `dotnet build` 不会丢失数据。

## 目录结构

```
Views/          ← XAML 页面
ViewModels/     ← ViewModel（继承 ViewModel 基类）
Services/       ← 服务层（S7、报警、配置、配方）
Models/         ← 数据模型
Helpers/        ← IValueConverter 等
Controls/       ← 自定义 UserControl
```

## 编码约定

详见 `.claude/rules/` 下的约定文件：

- `csharp-conventions.md` — C# 命名、风格、静态分析
- `wpf-conventions.md` — XAML / MVVM / 主题适配
- `wpfui-official-conventions.md` — Wpf.Ui 库官方用法

## 关于本仓库

本项目最初为 WPF-UI Gallery 的裁剪改造版，保留了其现代化 UI 框架（Fluent Design、MVVM、DI），替换业务逻辑为西门子 S7 通信 + 报警/配方/趋势图等工控功能。

### 2026-07-04 重构记录

- **重命名**：`Wpf.Ui.Gallery.Kesa` → `WpfScada`，程序集 `WpfScada`，根命名空间 `WpfScada`
- **新增 Modbus**：完整 Modbus RTU/TCP 协议栈（Protocol → Transport → PollingService），含 XAML 调试页面
- **删除**：`BasicUISystem`（无价值 WinForms 登录）、`WPF_S71200_SAMPLE`（功能已继承）
- **保留**：`WinForms_References/`（原 TEST_101，改名保留当 WinForms 参考）
- **路径**：运行时数据统一在 `%APPDATA%\WpfScada\`
