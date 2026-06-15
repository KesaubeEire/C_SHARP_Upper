# 2024-06-14 下午鼓捣总结

## 项目总览

C# WinForms Modbus 上位机学习实战项目 + 配套面试文档 + Node.js 对照研究项目。

### 核心代码 `Project/TEST_101/`

**三层架构**：
- `ModbusProtocol.cs` — 纯协议层（帧组装/解析、CRC16-Modbus 校验）
- `ModbusTransport.cs` — 传输层（串口/TCP 抽象）
- `ModbusForm.cs` + `MainForm.cs` — UI 层（带 TabControl 的监控系统）

**辅助模块**：
- `Core/` — EventBus 事件总线、DataPoint 数据点
- `Alarm/` — 报警管理（阈值检测 + 冷却防抖）
- `Recipe/` — 配方管理（JSON 序列化）
- `Chart/` — ScottPlot 实时曲线
- `Storage/` — SQLite + SQL Server 双数据库
- `Report/` — EPPlus Excel 报表导出

**启动模式**：`Program.cs` — 有命令行参数走控制台模式，无参数启动 WinForms MainForm

### 研究项目 `research/trioop/`

Node.js + TypeScript + React/Vite 实现的 PLC 监控系统（C# 上位机的 Web 版对照）。
- 后端：Express/WebSocket 服务 + PLC 模拟器
- 前端组件：PLCCard、ConnectionPanel、DBBlockPanel、IOGrid
- 已迁移到 pnpm 包管理

---

## Git diff 技巧

```bash
# 工作区 vs 暂存区
git diff -- file.cs

# 暂存区 vs HEAD
git diff --cached -- file.cs

# 特定 commit 之间
git diff <commit1>..<commit2> -- file.cs

# 用 blob hash 直接 diff（当 path diff 失灵时兜底）
git diff <blob-hash-1> <blob-hash-2>

# 查看 commit 改了哪些文件
git show --stat <commit>

# 查看某个文件的历史
git log --oneline -- file.cs
```

注意：`git show <commit> -- path` 对某些文件可能返回空（疑似 CRLF 相关 bug），此时用 blob hash diff 可绕过。

---

## 代理配置

Windows 机器通过端口 7897 走代理：

```bash
git config --global http.proxy http://127.0.0.1:7897
git config --global https.proxy http://127.0.0.1:7897

# gh CLI 需要环境变量
export HTTPS_PROXY=http://127.0.0.1:7897
export HTTP_PROXY=http://127.0.0.1:7897
```

---

## GitHub CLI 安装与认证

```bash
# 通过 PowerShell 安装
powershell -Command "winget install --id GitHub.cli -e --source winget"

# 添加 PATH（Git Bash 需要）
export PATH="$PATH:/c/Program Files/GitHub CLI"

# Web 认证（注意代理）
HTTPS_PROXY=http://127.0.0.1:7897 gh auth login

# 查看认证状态
gh auth status
```

---

## 代码阅读笔记

### EventBus 重构（commit 54a696b）

**改前**：`ConcurrentDictionary<Type, ConcurrentBag<Delegate>>`
- `ConcurrentBag` 不支持元素移除
- `Unsubscribe()` 是空实现
- 长时间运行会内存泄漏

**改后**：`ConcurrentDictionary<Type, List<Delegate>>` + `_lock` 对象
- `lock` 内 `Add` / `Remove`
- `Publish` 遍历时先锁内 `ToArray()` 快照再遍历
- 既线程安全又防止遍历时被修改

教训：`ConcurrentBag` 看着线程安全但不支持移除，`List + lock` 反而更可靠。

### ModbusProtocol 亮点
- 纯协议逻辑不含 IO，可独立单元测试
- RTU（串口）和 TCP（网口）的帧组装分离
- 响应解析有完整的数据边界校验
- 功能码上限限制（线圈 2000、寄存器 125）防止单次读取超时
