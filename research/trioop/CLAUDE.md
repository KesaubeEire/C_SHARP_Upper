# Trioop 式 PLC 实时监控系统

本项目是一个 **纯 Node.js** 的西门子 S7-1200 PLC 监控与控制系统，无需 Docker、无需 Grafana，一个 `npm start` 搞定。

## 快速启动

```bash
# 1. 安装依赖
npm install

# 2. 配置 PLC 地址（编辑 plc-config.js）
#    - 设置你的 PLC IP
#    - 填写你要读写的 DB 块地址

# 3. 启动
npm start
```

浏览器打开 `http://localhost:3000` 即可看到仪表盘。

## 前置条件

### 硬件连接
- 你的机器和 S7-1200 通过**网线**连接（直连或通过交换机均可）
- 确认两者在**同一网段**（例如 PLC 设 `192.168.1.100`，机器设 `192.168.1.x`）

### PLC 端配置（TIA Portal 中必须做）
| 配置 | 位置 | 操作 |
|------|------|------|
| 启用 PUT/GET 访问 | PLC 属性 → 保护 | 勾选"允许从远程伙伴（PUT/GET）访问" |
| 取消优化块访问 | 每个 DB 块右键 → 属性 | 取消勾选"优化的块访问" |
| 固定 IP | PLC 属性 → PROFINET 接口 → 以太网地址 | 设置固定 IP（如 192.168.1.100） |

## 项目结构

```
trioop/
├── CLAUDE.md                        ← 本文件
├── README.md                        ← 更详细的使用文档
├── package.json
├── plc-config.js                    ← ✅ 你只需要编辑这个文件
├── server.js                        ← 服务端（Express + S7 通信）
└── public/
    └── index.html                   ← 前端仪表盘
```

## 当用户说"帮我配置PLC点"时

1. 打开 `plc-config.js`
2. 按用户提供的 TIA Portal DB 块变量表修改 `variables` 数组
3. 确认 `type` 匹配（real / int / bool / word / dword / byte）
4. `writable: true` 表示这个点可以在仪表盘上控制写入
5. 启动后验证数据是否正确显示

## 技术栈

- **后端**: Node.js + Express + node-snap7js（纯 JS S7 协议实现）
- **前端**: 原生 HTML/CSS/JS（用户是前端开发者，会自行定制）
- **通信**: ISO-on-TCP（S7 协议，端口 102）
- **实时推送**: Server-Sent Events (SSE)
