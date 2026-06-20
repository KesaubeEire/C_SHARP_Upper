# VS_Dev 项目说明

## 项目概述

这是一个 **C# WinForms Modbus 上位机**学习/实战项目，同时配套生成了面试深度解析文档。
目标是：边做项目边沉淀知识，最终形成一套可用于面试准备和实际开发的完整素材。

## 技术栈

- **框架**: .NET 10.0-windows (WinForms)
- **语言**: C# 12+（Nullable + ImplicitUsings 均开启）
- **协议**: Modbus RTU/TCP
- **依赖**: `System.IO.Ports` 10.0.8（串口通信）
- **Shell**: PowerShell 5.1（Windows 环境，注意不支持 `&&` 管道链、三元运算符等 Bash 语法）

## 目录结构

```
VS_Dev/
├── CLAUDE.md                          ← 本文件
├── Project/TEST_101/                  ← 主项目（Modbus 上位机）
│   ├── TEST_101.slnx                  ← 解决方案（.slnx 格式）
│   ├── TEST_101.csproj                ← 项目文件（net10.0-windows）
│   ├── Program.cs                     ← 入口：控制台 or WinForms 切换
│   ├── ConsoleRunner.cs               ← 控制台模式运行器
│   ├── ModbusForm.cs / .Designer.cs   ← 主窗口 UI + 逻辑
│   ├── ModbusProtocol.cs             ← Modbus 帧组装/解析（协议层）
│   ├── ModbusTransport.cs            ← Modbus 传输层（串口/TCP 抽象）
│   ├── TestForm.cs / .Designer.cs    ← 测试窗口
│   ├── HistoryDropDown.cs            ← 下拉历史记录控件
│   ├── InputHistoryManager.cs        ← 输入历史管理器
│   ├── CSharpConceptsDemo.cs         ← C# 核心概念示例代码
│   └── CSharpMasterGuide.cs          ← C# 语法全攻略（21章，约2800行）
│
├── ModbusForm代码讲解_面试深度.html      ← ModbusForm 逐函数面试讲解
├── ModbusForm代码面试级解析.html          ← ModbusForm 代码结构面试解析
├── Modbus_上位机_面试题库.html            ← Modbus 上位机相关面试题集合
└── WinForms上位机开发最佳实践_2024-2026.html ← WinForms 最佳实践总结
```

## 架构设计要点

### Modbus 分层架构
- **ModbusProtocol**: 纯协议逻辑，不含 IO — 负责帧的组装（请求）和解析（响应），计算 CRC
- **ModbusTransport**: 传输层抽象 — 封装串口/TCP 连接管理、发送接收字节流
- **ModbusForm**: UI 层 — 调用 Transport 发送 Protocol 组装的帧，接收并显示结果

### 启动模式
`Program.cs` 支持两种运行模式：
- **WinForms 模式**：启动 `ModbusForm` 图形界面
- **控制台模式**：通过 `ConsoleRunner` 在终端运行，便于调试

## 协作规则（重要）

- **不自动 commit**：改完代码先给用户检查，用户确认后再提交
- **不自动 push**：未经用户允许绝不推送到任何远程仓库（internal / origin 等）
- **git 操作前先问**：commit、push、merge、branch 等操作必须用户明确指令

## 关键注意事项

### C# / .NET 约定
- 目标框架是 **net10.0**，可用最新 C# 语法特性
- `Nullable` 和 `ImplicitUsings` 已开启，无需手动写 `using System;` 等
- WinForms 项目需要 `UseWindowsForms` 标志

### PowerShell 环境限制
Windows PowerShell 5.1 环境下：
- **不支持** `&&` 和 `||` 管道链操作符 — 用 `A; if ($?) { B }` 替代
- **不支持** 三元运算符 `?:`、null 合并 `??`、null 条件 `?.`
- 用 `if/else` 和 `$null -eq` 检查替代
- 默认编码 UTF-16 LE，写文件给其他工具读时用 `-Encoding utf8`

### Git 规范
- 主分支: `master`
- 提交信息格式: `@ <描述> @`（用户自定义格式）
- 新功能开发建议先创建分支，完成后合并回 master

## 面试文档说明

根目录的 4 份 HTML 文档是配套学习资料：
1. **代码讲解** — ModbusForm 逐函数、逐行深度讲解，适合面试口述
2. **代码解析** — 从架构到细节的整体面试级分析
3. **面试题库** — Modbus 协议 + 上位机开发相关面试题汇总
4. **最佳实践** — 2024-2026 WinForms 开发工程化最佳实践

## 下一步方向（待定）

- [ ] 完善 Modbus 功能（多寄存器读写、异常处理、断线重连）
- [ ] TestForm 测试窗口功能扩展
- [ ] 补充更多 C# 语法实战示例
- [ ] 面试题库持续更新

## Agent skills

### 问题追踪器

Issue 以本地 markdown 文件形式存放在 `.scratch/<功能名>/` 下。详见 `docs/agents/issue-tracker.md`。

### 分类标签

五个标准分类标签：`needs-triage`、`needs-info`、`ready-for-agent`、`ready-for-human`、`wontfix`。详见 `docs/agents/triage-labels.md`。

### 领域文档

单上下文布局：项目根目录下 `CONTEXT.md` + `docs/adr/`。详见 `docs/agents/domain.md`。
