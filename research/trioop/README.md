# Trioop PLC Monitor

纯 Node.js 西门子 S7-1200 实时监控与控制系统。

**技术栈：** pnpm + Vite + React + TypeScript + Express + node-snap7js

## 架构

```
┌──────────┐  ISO-on-TCP  ┌────────────────────┐  HTTP/SSE  ┌──────────────┐
│ S7-1200  │ ◄── :102 ──► │  Express API 服务   │ ◄────────► │  React 仪表盘 │
│ DB 块    │              │  node-snap7js       │            │  实时数据     │
│          │              │  SSE 推送           │   Vite     │  控制按钮    │
└──────────┘              └────────────────────┘  HMR       └──────────────┘
```

## 快速开始

### 前置条件

- **Node.js 20+** + **pnpm**
- S7-1200 PLC（以太网连接，同网段）
- PLC 端配置：启用 PUT/GET 访问、DB 块取消优化块访问

### 启动

```bash
# 1. 安装依赖
pnpm install

# 2. 编辑 PLC 配置
#    打开 server/config.ts
#    设置 PLC IP 和要读写的 DB 地址

# 3. 启动开发模式
pnpm dev
```

浏览器打开 **http://localhost:5173**

### 模式说明

| 命令 | 说明 |
|------|------|
| `pnpm dev` | 开发模式：Vite HMR (:5173) + API (:3001) |
| `pnpm dev:server` | 只启动后端 API |
| `pnpm dev:client` | 只启动前端 HMR |
| `pnpm build` | 构建前端到 `dist/` |
| `pnpm start` | 生产模式：一个端口 (:3000) 提供 API + 前端 |

## 项目结构

```
trioop/
├── shared/types.ts       ← 前后端共享类型（TS）
├── server/               ← 后端
│   ├── index.ts          ← Express 服务入口
│   ├── config.ts         ← PLC 地址配置（你改这个）
│   ├── plc.ts            ← S7Client 封装（读/写）
│   └── sse.ts            ← SSE 推送管理
├── src/                  ← 前端（React + Vite）
│   ├── main.tsx          ← 入口
│   ├── App.tsx           ← 主应用
│   ├── App.css           ← 样式
│   ├── hooks/
│   │   ├── usePLCData.ts ← SSE 实时数据 hook
│   │   └── usePLCWrite.ts← 写入操作 hook
│   └── components/
│       ├── StatusBar.tsx  ← 顶部状态栏
│       ├── PLCGrid.tsx    ← 变量网格
│       ├── PLCCard.tsx    ← 单个变量卡片
│       └── WriteControl.tsx ← 写入控件
├── index.html            ← Vite 入口
├── vite.config.ts        ← Vite 配置（API proxy）
├── tsconfig.json         ← 前端 TS 配置
├── tsconfig.server.json  ← 后端 TS 配置
└── package.json
```

## API 接口

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/plc/data` | 获取所有 PLC 点最新值 |
| GET | `/api/plc/config` | 获取 PLC 配置（变量列表） |
| POST | `/api/plc/write` | 写入 PLC（body: `{name, value}`） |
| GET | `/api/plc/stream` | SSE 实时推送流 |
