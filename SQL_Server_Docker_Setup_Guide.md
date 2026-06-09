# SQL Server 安装踩坑全记录：从崩溃到 Docker 一键搞定

> 耗时近 24 小时，踩遍了 SQL Server 安装的所有坑，最终用 Docker 完美解决。
> 如果你也遇到了 "等待数据库引擎恢复句柄失败" 的错误，这篇文章能帮你省下大半天时间。

---

## 背景

我在做一个 **C# WinForms Modbus 上位机**项目，需要 SQL Server 作为数据库支撑。本以为装个数据库很简单，没想到掉进了一个巨坑。

**系统环境：**
- Windows 11 Pro (Build 26200)
- ASUS 主板（ROG 系列）
- 已安装 Visual Studio 2022（自带 LocalDB）
- 已安装 AutoCAD 2023

---

## 第一阶段：尝试 SQL Server 2025 Express

### 安装报错

从微软官网下载 SQL Server 2025 Express，选"基本"安装，一路下一步，最后弹出：

```
等待数据库引擎恢复句柄失败
错误代码: 0x851A001A
```

### 排查过程

**1. 检查 Windows 用户名是否中文**

网上说这是最常见的原因，但我的用户名是 `Kesa_Win`，纯英文，排除。

**2. 检查是否有旧版本残留**

发现系统里有**两个** SQL Server 实例同时存在：
- `MSSQLSERVER`（默认实例）
- `SQLEXPRESS`（Express 实例）

两个都是停止状态，退出码 `1067`（进程异常终止）。互相冲突。

**3. 彻底卸载重装**

控制面板卸载所有 SQL Server 组件 → 删除残留文件夹 → 清理注册表 → 重启 → 重装

还是同样的错误。

---

## 第二阶段：尝试 LocalDB

想着完整的 Express 装不上，试试轻量级的 LocalDB 吧。

```bash
sqllocaldb start MSSQLLocalDB
# Error occurred during LocalDB instance startup: SQL Server process failed to start.
```

创建新实例也不行：

```bash
sqllocaldb create NewDB
# Error occurred during LocalDB instance startup: SQL Server process failed to start.
```

**关键发现：** LocalDB 和 Express 用的是**同一个 sqlservr.exe 引擎**，所以只要这个引擎有问题，不管装什么形式的 SQL Server 都会崩。

---

## 第三阶段：查 Windows 事件日志

终于找到了真正的错误信息：

```
SQLLocalDB 17.0 - Error code: 575
Windows API call WaitForMultipleObjects returned error code: 575.
Windows system error message is: {Application Error}
应用程序无法正常启动(0xc0000005)
```

**错误码 575 = 应用程序无法启动。**

### 发现系统异常

事件日志里还有这些：

```
Aac3572MbHal_x86.exe 崩溃 → combase.dll（COM 基础设施）
LightingService.exe 崩溃 → ntdll.dll（系统核心）
VSS 错误 → 系统正在关机（但并没有关机）
```

**这些全是华硕主板的软件：**
- `Aac3572MbHal_x86.exe` — 华硕 Aura 灯光控制
- `LightingService.exe` — RGB 灯光服务
- `ArmouryCrate.Service.exe` — 华硕奥创中心

### 尝试禁用华硕服务

```powershell
sc stop LightingService
sc stop AsusCertService
sc stop AsusFanControlService
taskkill /F /IM Aac3572MbHal_x86.exe
```

禁用后重启，再启动 SQL Server → **还是崩溃**。

### 检查 SQL Server 错误日志

```
C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\Log\ERRORLOG
```

日志显示 SQL Server 能启动到一定阶段：
```
Microsoft SQL Server 2025 (RTM) - 17.0.1000.7 (X64)
Express Edition (64-bit) on Windows 10 Pro
...
CLR version v4.0.30319 loaded.
External governance manager initialized
Detected pause instruction latency: 97 cycles.
```

然后就崩溃了，报 `Unable to create stack dump file due to stack shortage`。

### 检查 Windows 系统文件

```powershell
sfc /scannow
# Windows 资源保护未找到任何完整性违规。
```

系统文件没问题，不是 Windows 损坏。

---

## 第四阶段：尝试 SQL Server 2022

想着 2025 是预览版不稳定，试试 2022。

**结果：微软官网所有 2022 的链接都跳转到 2025。** 已经没有 2022 的下载了。

---

## 第五阶段：求助社区 + 换思路

### 问题定位

综合所有线索：
- SQL Server 2025 的 `sqlservr.exe` 在这台 ASUS 电脑上无法启动
- 不管是 Express、LocalDB、任何实例名，都用同一个引擎，都会崩
- 华硕的主板软件干扰了 COM 基础设施
- 系统文件没问题，但底层 COM 组件跟 SQL Server 2025 不兼容

### 最终方案：Docker

既然 Windows 原生跑不了 SQL Server，那就用 Docker 容器跑，绕过系统兼容性问题。

---

## 最终解决方案：Docker + SQL Server 2019

### 第 1 步：安装 Docker Desktop

```powershell
# 用 winget 一键安装
winget install Docker.DockerDesktop

# 或者手动下载
# https://www.docker.com/products/docker-desktop/
```

安装完重启电脑。

### 第 2 步：启动 Docker Desktop

开始菜单打开 Docker Desktop，等任务栏鲸鱼图标变稳定。

### 第 3 步：一条命令启动 SQL Server

```powershell
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Sa@123456" -p 1433:1433 --name sqlserver -d mcr.microsoft.com/mssql/server:2019-latest
```

参数说明：
- `ACCEPT_EULA=Y` — 同意许可协议
- `SA_PASSWORD=Sa@123456` — SA 账号密码（密码必须复杂度够）
- `-p 1433:1433` — 把容器的 1433 端口映射到本机
- `--name sqlserver` — 给容器起个名字
- `-d` — 后台运行

### 第 4 步：SSMS 连接

```
服务器名称：localhost
身份验证：SQL Server 身份验证
登录名：sa
密码：Sa@123456
```

**连接成功！**

### 常用命令

```powershell
# 启动（开机后容器可能停了）
docker start sqlserver

# 停止
docker stop sqlserver

# 查看状态
docker ps

# 查看日志
docker logs sqlserver

# 删除容器（数据会丢失）
docker rm -f sqlserver
```

---

## 踩坑总结

| 尝试 | 结果 | 原因 |
|------|------|------|
| SQL Server 2025 Express | 崩溃 | sqlservr.exe 跟系统不兼容 |
| SQL Server 2025 LocalDB | 崩溃 | 同一个引擎，同样的问题 |
| 禁用华硕服务 | 无效 | 根因不在华硕服务 |
| sfc /scannow | 无效 | 系统文件完好 |
| SQL Server 2022 | 下载不到 | 微软已下架，全部跳转 2025 |
| **Docker + SQL Server 2019** | **成功** | **容器化隔离，绕过系统兼容性** |

---

## 经验教训

### 1. 不要迷信官方安装程序

SQL Server 的安装程序出了名的脆弱，稍微有点环境问题就装不上。这次的 `0x851A001A` 错误在网上有大量案例，但几乎没有标准解决方案。

### 2. Docker 是 Windows 装 SQL Server 的最佳方案

- 不受系统环境影响
- 不会跟其他软件冲突
- 一条命令搞定
- 数据可以持久化挂载到本地目录
- 随时可以删除重建

### 3. 遇到问题要学会查日志

```
Windows 事件日志：eventvwr.msc
SQL Server 错误日志：C:\Program Files\Microsoft SQL Server\MSSQL17.*\MSSQL\Log\ERRORLOG
```

很多时候安装程序给的错误信息毫无用处，真正的答案藏在系统日志里。

### 4. 数据库选型建议

| 数据库 | 安装难度 | 适用场景 |
|--------|---------|---------|
| SQLite | 零配置 | 单机、嵌入式、开发测试 |
| SQL Server Express | 中等（经常翻车） | 企业级、Windows 生态 |
| SQL Server (Docker) | 简单 | 本地开发、CI/CD |
| MySQL | 简单 | 跨平台、Web 应用 |

**本地开发推荐用 Docker 跑 SQL Server，生产环境再部署正式的 SQL Server 实例。**

---

## 环境信息

- 系统：Windows 11 Pro 26200
- 主板：ASUS ROG
- Docker Desktop：4.76.0
- SQL Server：2019-latest (Docker)
- SSMS：20.x

---

*最后更新：2026-06-09*
