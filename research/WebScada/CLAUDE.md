# WebScada PLC Monitor

纯 Node.js 西门子 S7-1200 实时监控与控制系统。
**pnpm + Vite + React + TypeScript + Express + node-snap7js**

## 快速启动

```bash
pnpm install
# 编辑 server/config.ts 设置 PLC IP
pnpm dev
```

浏览器打开 `http://localhost:5173`。

## 结构

```
WebScada/
├── shared/types.ts           ← 前后端共享类型
├── server/                   ← 后端（Express + S7）
│   ├── index.ts              ← 主入口
│   ├── config.ts             ← PLC 配置（改这里）
│   ├── plc.ts                ← S7Client 封装
│   └── sse.ts                ← SSE 推送
├── src/                      ← 前端（React + Vite）
│   ├── App.tsx
│   ├── hooks/usePLCData.ts   ← SSE 实时数据
│   ├── hooks/usePLCWrite.ts  ← 写入操作
│   └── components/           ← UI 组件
├── index.html                ← Vite 入口
├── vite.config.ts
├── tsconfig.json
└── package.json
```

## 开发

```bash
pnpm dev          # 同时启动前端(Vite :5173) + 后端(Express :3001)
pnpm dev:server   # 只启动后端
pnpm dev:client   # 只启动前端 HMR
pnpm build        # 构建前端到 dist/
pnpm start        # 生产模式 (:3000)
```

## 配置

编辑 `server/config.ts`：
- 设置 PLC IP
- 填写 DB 块变量（类型、偏移量、读写权限）

## 多 worktree 协作

后端端口根据 worktree 路径 hash 自动分配，互不冲突：

1. 后端启动时从 `3000 + 路径偏移` 开始找空闲端口，写入 `.port.json`
2. 前端 Vite 读 `.port.json` 获取后端地址
3. 每个 worktree 路径不同 → hash 偏移不同 → 端口不同
4. 启动脚本不杀其他 worktree 的进程，只清理自己的 `.port.json`

手动分配端口：`pnpm tsx server/resolve-port.ts`

## PLC 前置条件

1. TIA Portal 启用 PUT/GET 访问
2. DB 块取消「优化的块访问」
3. 固定 IP，与电脑同网段
