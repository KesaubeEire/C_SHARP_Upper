# Trioop 式 PLC 实时监控系统

## 这是什么

一个纯 Node.js 的西门子 S7-1200 实时监控与控制 Web 系统。无需 Docker、无需 Grafana，`npm install && npm start` 就能跑。

浏览器里就能看到 PLC 的所有 IO 点、温度/压力/转速等实时数值，还能直接点按钮写回 PLC 控制设备启停。

## 架构

```
┌──────────────┐     ISO-on-TCP      ┌──────────────────┐      HTTP/SSE      ┌──────────────┐
│  S7-1200 PLC │ ◄──── port 102 ──► │  Node.js 服务端   │ ◄───────────────► │  浏览器仪表盘 │
│  DB1, DB3... │                     │  node-snap7js     │                   │  实时数据 +   │
│              │                     │  Express API      │                   │  控制按钮     │
└──────────────┘                     └──────────────────┘                   └──────────────┘
```

## 前置条件

### 硬件
- 一台能连 S7-1200 的 Windows/Linux/Mac 机器
- 网线（直连或用交换机均可）
- S7-1200 PLC（已通电并配置好 IP）

### 软件
- **Node.js 18+**（推荐 20 LTS）
- **npm**

### PLC 端配置（TIA Portal）

| 步骤 | 说明 |
|------|------|
| 1. 启用 PUT/GET | PLC 属性 → 保护 → 勾选"允许从远程伙伴（PUT/GET）访问" |
| 2. 取消优化块访问 | 每个要读写的 DB 块 → 右键属性 → 取消"优化的块访问" |
| 3. 设固定 IP | PLC 属性 → PROFINET 接口 → 以太网地址 → 设固定 IP |
| 4. 记下变量表 | 记下 DB 块号、变量名、数据类型、偏移量 |

> ⚠️ 如果不取消优化块访问，读出来的数据地址会错位！（S7-1200 默认开启）

## 快速开始

```bash
# 1. 进入项目目录
cd research/trioop

# 2. 安装依赖
npm install

# 3. 编辑 PLC 配置
#    打开 plc-config.js，设置你的 PLC IP 和 DB 地址
code plc-config.js

# 4. 启动
npm start

# 5. 浏览器打开
#    http://localhost:3000
```

## 配置说明

### `plc-config.js`

```javascript
// PLC 连接信息
plc: {
  ip: '192.168.1.100',   // ← 改成你的 PLC IP
  rack: 0,
  slot: 1,               // S7-1200 固定 slot=1
},

// DB 块变量
variables: [
  { name: '温度_1',   offset: 0, type: 'real' },     // 4字节浮点
  { name: '电机转速', offset: 4, type: 'int' },       // 2字节整数
  { name: '运行状态', offset: 6, type: 'bool', bit: 0 }, // 1位布尔
  { name: '设定温度', offset: 8, type: 'real', writable: true }, // 可写
]
```

**支持的数据类型：**

| type | 字节数 | 说明 |
|------|--------|------|
| `real` | 4 | 浮点数（IEEE 754） |
| `int` | 2 | 有符号 16 位整数 |
| `dint` | 4 | 有符号 32 位整数 |
| `word` | 2 | 无符号 16 位整数 |
| `dword` | 4 | 无符号 32 位整数 |
| `bool` | 1 bit | 布尔值（需要指定 `bit` 索引） |
| `byte` | 1 | 单字节 |

## API 接口

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/plc/data` | 获取所有 PLC 点最新值 |
| POST | `/api/plc/write` | 写入 PLC 点（body: `{name, value}`） |
| GET | `/api/plc/stream` | SSE 实时推送数据流 |

**写入示例：**

```bash
curl -X POST http://localhost:3000/api/plc/write \
  -H "Content-Type: application/json" \
  -d '{"name":"设定温度", "value":35.5}'
```

## 常见问题

### 连不上 PLC
1. `ping 192.168.1.100` 通不通？
2. TIA Portal 里 PUT/GET 开了吗？
3. DB 块取消优化访问了吗？
4. 电脑 IP 和 PLC IP 在同一网段吗？

### 读出来的数据不对
- 检查 DB 块是否取消了优化访问
- 检查偏移量和数据类型是否匹配
- REAL 和 DINT 都是 4 字节，容易混淆

### 写不进去
- 检查 `plc-config.js` 里该变量是否设置了 `writable: true`
- 检查 PLC 端写权限设置
- 检查写入值类型是否匹配

## 项目结构

```
trioop/
├── README.md              # 本文件
├── CLAUDE.md              # Claude Code 指令
├── package.json
├── plc-config.js          # ← 你只需要改这里
├── server.js              # 服务端主程序
└── public/
    └── index.html         # 前端仪表盘（可自由定制）
```

## 与 TEST_101 项目的关系

`TEST_101` 是 C# WinForms Modbus 上位机（本仓库中的学习项目）。
本项目是同一仓库下的另一个独立工具，走 Node.js 全栈路线，面向西门子 S7 协议。

以后可以把这里的 Snap7 通信逻辑移植到 C# 中，实现 C# 全栈方案。
