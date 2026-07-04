# vPLC — 虚拟 S7-1200 软 PLC

纯 Node.js 实现的虚拟西门子 S7-1200 PLC，支持 S7 协议和 Modbus TCP，内置用户脚本引擎和 Web 管理界面。无需任何硬件、TIA Portal 或原生依赖。

## 快速开始

```bash
# 从项目根目录
pnpm dev:vplc
```

启动后：

| 服务 | 端口 | 说明 |
|:----|:----|:------|
| S7 (ISO-on-TCP) | 1200 | 上位机连接: `127.0.0.1:1200` Rack:0 Slot:1 |
| Web API | 1201 | HTTP REST: `http://localhost:1201/api/vplc` |
| Modbus TCP | 1210 | 标准 Modbus TCP，兼容 SCADA/HMI |
| 前端管理界面 | 1420 | `http://localhost:1420` |

> 端口可在 `vplc/vplc-config.json` 中修改。发生冲突时自动回退，真实端口写入 `.port.json`。

## 架构

### 模块结构

```
vplc/
├── vplc.ts             入口 — 初始化 + 启动所有服务器
├── types.ts            共享类型定义
├── plc-memory.ts       内存区域管理 (DB/I/Q/M/TM/CT)
├── plc-state.ts        RUN/STOP、RTC、LED、诊断缓冲区
├── plc-runtime.ts      OB周期调度、模拟数据、用户脚本引擎
├── s7-protocol.ts      S7协议帧解析/组装 (7个功能码)
├── s7-server.ts        TCP + COTP连接管理
├── modbus-server.ts    Modbus TCP (8个功能码)
├── web-api.ts          HTTP REST API
├── persistence.ts      配置/内存/PID持久化
├── vplcClient.ts       外部 Node.js 客户端 (直写 Q/M 区)
└── frontend/           React + Vite 前端管理界面
```

### 内存布局

| 区域 | 大小 | 说明 |
|:----|:----|:------|
| DB | 动态 | 数据块，按需创建 (默认 DB1:64, DB6:64) |
| I (PE) | 256 字节 | 物理输入 — 自动模拟传感器信号 |
| Q (PA) | 256 字节 | 物理输出 — 上位机写入 |
| M (MK) | 256 字节 | 位存储区 |
| TM | 256 字节 | 定时器 |
| CT | 256 字节 | 计数器 |

### 数据流

```
上位机/SCADA/HMI
    │
    ├─ S7 (ISO-on-TCP:1200) ──→ s7-server → s7-protocol → plc-memory
    │
    ├─ Modbus TCP (:1210) ─────→ modbus-server ─────────→ plc-memory
    │
    └─ HTTP REST (:1201) ──────→ web-api ───────────────→ plc-memory
                                        │
                                        └─ React 前端 (:1420)
                                                   │
                                          ┌────────┴────────┐
                                          │ 监视 | 导入 |    │
                                          │ 触发器 | 脚本    │
                                          └─────────────────┘

plc-runtime (500ms间隔)
    ├─ OB1  自由循环 — 每次执行
    ├─ OB35 500ms周期 — 用户脚本挂载点
    ├─ OB100 启动一次 — 复位M/Q区
    └─ 模拟数据更新 — 温度/压力/位置/I区波动
```

## 功能清单

### ✅ 已实现

**协议**
- S7 ISO-on-TCP (RFC1006) — 完整 COTP + S7 PDU 协商
- S7 Read (0x04) — 读任意 I/Q/M/DB/TM/CT 区域
- S7 Write (0x05) — 写任意 I/Q/M/DB 区域
- S7 Setup Communication (0xF0) — PDU 大小协商
- S7 Read SZL (0x11) — 返回模块标识 S7-1200
- S7 Read Time-of-Day (0x19) — BCD 编码日期时间
- S7 Request Diagnostics (0x1A) — 诊断状态
- S7 Protection (0x2D) — 返回"无密码完全访问"
- Modbus TCP — 8 功能码完整实现

**运行时**
- RUN/STOP 状态切换
- OB 周期管理 (OB1/OB35/OB100)
- RTC (实时时钟，可设偏移)
- 诊断缓冲区 (200条，循环)
- LED 指示灯模拟 (RUN/STOP/ERROR/MAINT)
- 模拟数据自动变化 (温度、压力、位置、I区信号)
- 内存数据持久化 (vplc-memory.json)
- DB/UDT 导入 (TIA Portal .db/.udt 文件解析)

**用户脚本**
- JavaScript 沙箱 (Node.js `vm`，100ms超时)
- 挂载到 OB1/OB35/OB100 执行
- API: `readByte/writeByte/readBit/writeBit/readReal/writeReal/readInt/writeInt/log/now`
- 脚本持久化 (随配置保存/恢复)

**管理界面** (React + Vite)
- 📊 监视 — I/Q/M 区位实时预览 + HEX 值
- 📥 导入 — UDT/DB 上传、DB块管理
- ⚡ 触发器 — 条件触发写入 (UI层，后端stub)
- 📜 脚本 — 在线编辑/启用/删除/OB关联

**基础设施**
- 端口冲突自动回退 + `.port.json` 端口发现
- 优雅退出 + PID 文件管理
- 多模块架构 (10文件)

### 🚧 未实现 (Roadmap)

**协议**
- S7 Block 上传下载 (0x31-0x34)
- S7 BSEND/BRCV (0x1C/0x1D)
- OPC UA Server
- MQTT Bridge
- 多客户端连接管理

**工程功能**
- Watch Table / 变量监控表
- Force / 强制 I/O
- 报警管理 (带确认)
- 网络模拟 (延迟/丢包/断线)
- 优先级抢占的 OB 调度
- 项目文件导入/导出
- 结构化日志
- 单元测试

**安全性**
- S7 密码 / 访问级别 (目前返回"无密码")
- IP 白名单
- TLS/HTTPS

## API 概览

| 端点 | 方法 | 说明 |
|:----|:----|:------|
| `/api/vplc` | GET | 快照 (所有内存区 + 解析值 + OB状态) |
| `/api/vplc/write` | POST | 写入字节/位/REAL |
| `/api/vplc/toggle-bit` | POST | 切换位 |
| `/api/vplc/state` | POST | RUN/STOP |
| `/api/vplc/rtc` | GET/POST | 读取/设置 RTC |
| `/api/vplc/diag` | GET/DELETE | 读取/清空诊断缓冲区 |
| `/api/vplc/leds` | GET | LED 状态 |
| `/api/vplc/ob` | GET | OB 执行统计 |
| `/api/vplc/dbs` | GET/POST/DELETE | DB 块配置 |
| `/api/vplc/import-udt` | POST | 导入 UDT 文件 |
| `/api/vplc/import-db` | POST | 导入 DB 文件 |
| `/api/vplc/scripts` | GET/POST | 用户脚本管理 |

## 配置

编辑 `vplc/vplc-config.json`:

```json
{
  "port": 1200,        // S7 端口 (Web API 自动 +1)
  "host": "0.0.0.0",
  "dbs": { "1": 64, "6": 64 }
}
```
