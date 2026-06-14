/**
 * PLC 监控系统 — 服务端入口
 *
 * 启动方式：npm start  或  npm run dev（自动重启）
 * 浏览器打开：http://localhost:3000
 */

const express = require('express');
const path = require('path');
const { S7Client, S7Consts } = require('node-snap7js');
const plcConfig = require('./plc-config');

// ─── 类型映射 ─────────────────────────────────────────────
const TYPE_MAP = {
  real:  { wordLen: S7Consts.S7WLReal,   bytes: 4 },
  int:   { wordLen: S7Consts.S7WLWord,   bytes: 2 },
  dint:  { wordLen: S7Consts.S7WLDWord,  bytes: 4 },
  word:  { wordLen: S7Consts.S7WLWord,   bytes: 2 },
  dword: { wordLen: S7Consts.S7WLDWord,  bytes: 4 },
  byte:  { wordLen: S7Consts.S7WLByte,   bytes: 1 },
  bool:  { wordLen: S7Consts.S7WLByte,   bytes: 1 }, // bool 读整字节，按位解析
};

// ─── 全局状态 ─────────────────────────────────────────────
const latestData = {};        // { variableName: value }
const sseClients = new Set();  // SSE 连接集合
const app = express();

app.use(express.json());
app.use(express.static(path.join(__dirname, 'public')));

// ─── PLC 连接管理 ─────────────────────────────────────────
let plcClient = null;
let connected = false;

async function connectPLC() {
  plcClient = new S7Client();
  const { ip, rack, slot } = plcConfig.plc;
  console.log(`[PLC] 正在连接 ${ip}:102 (rack=${rack}, slot=${slot}) ...`);
  try {
    const result = await plcClient.ConnectTo(ip, rack, slot);
    if (result === 0) {
      connected = true;
      console.log(`[PLC] ✅ 连接成功`);
    } else {
      connected = false;
      console.error(`[PLC] ❌ 连接失败 (错误码: ${result})`);
    }
  } catch (err) {
    connected = false;
    console.error(`[PLC] ❌ 连接异常:`, err.message);
  }
}

async function disconnectPLC() {
  if (plcClient && connected) {
    plcClient.Disconnect();
    connected = false;
    console.log('[PLC] 已断开');
  }
}

// ─── 读取 DB 块 ───────────────────────────────────────────
/**
 * 从同一 DB 号中读取一块字节区域，然后按变量配置解析出各个值。
 * 为了简化，每个变量独立读取（适用于变量不多的情况）。
 * 如果有大量变量，可以优化为按 DB 号合并读取。
 */
async function readVariable(varCfg) {
  if (!plcClient || !connected) throw new Error('PLC 未连接');

  const typeInfo = TYPE_MAP[varCfg.type];
  if (!typeInfo) throw new Error(`不支持的类型: ${varCfg.type}`);

  const buffer = Buffer.alloc(typeInfo.bytes);
  const amount = varCfg.type === 'bool' ? 1 : 1; // amount 是"wordLen 单位的数量"

  const result = await plcClient.ReadArea(
    S7Consts.S7AreaDB,
    varCfg.dbNumber,
    varCfg.offset,
    amount,
    typeInfo.wordLen,
    buffer
  );

  if (result !== 0) {
    console.warn(`[PLC] 读取失败: ${varCfg.name} (DB${varCfg.dbNumber}.${varCfg.offset}), 错误码: ${result}`);
    return null;
  }

  return parseBuffer(buffer, varCfg.type, varCfg.bit);
}

function parseBuffer(buffer, type, bit) {
  switch (type) {
    case 'real':
      return buffer.readFloatBE(0); // Snap7 返回大端序
    case 'int':
      return buffer.readInt16BE(0);
    case 'dint':
      return buffer.readInt32BE(0);
    case 'word':
      return buffer.readUInt16BE(0);
    case 'dword':
      return buffer.readUInt32BE(0);
    case 'byte':
      return buffer.readUInt8(0);
    case 'bool':
      if (bit === undefined || bit === null) {
        console.warn('[PLC] bool 类型需要指定 bit 索引');
        return null;
      }
      return (buffer.readUInt8(0) & (1 << bit)) !== 0;
    default:
      return null;
  }
}

// ─── 轮询读取 ─────────────────────────────────────────────
async function pollPLC() {
  if (!connected) {
    console.log('[PLC] 未连接，尝试重连...');
    await connectPLC();
    if (!connected) return;
  }

  const vars = plcConfig.variables;
  let hasError = false;

  for (const v of vars) {
    try {
      const value = await readVariable(v);
      if (value !== null) {
        latestData[v.name] = { value, type: v.type, writable: !!v.writable, dbNumber: v.dbNumber, offset: v.offset };
      }
    } catch (err) {
      hasError = true;
      console.error(`[PLC] 读取 ${v.name} 失败:`, err.message);
    }
  }

  // 推送给所有 SSE 客户端
  const payload = JSON.stringify(latestData);
  for (const client of sseClients) {
    client.write(`data: ${payload}\n\n`);
  }

  if (hasError) {
    // 有错误时主动断开，下次轮询会重连
    disconnectPLC();
  }
}

// ─── API 路由 ──────────────────────────────────────────────

/** 获取所有 PLC 点的最新值 */
app.get('/api/plc/data', (req, res) => {
  res.json(latestData);
});

/** 写入 PLC 点 */
app.post('/api/plc/write', async (req, res) => {
  const { name, value } = req.body;

  if (!name || value === undefined) {
    return res.status(400).json({ error: '请提供 name 和 value' });
  }

  // 查找变量配置
  const varCfg = plcConfig.variables.find(v => v.name === name);
  if (!varCfg) {
    return res.status(404).json({ error: `未找到变量: ${name}` });
  }
  if (!varCfg.writable) {
    return res.status(403).json({ error: `${name} 不可写` });
  }

  try {
    if (!connected) {
      await connectPLC();
      if (!connected) {
        return res.status(502).json({ error: 'PLC 未连接' });
      }
    }

    const typeInfo = TYPE_MAP[varCfg.type];

    if (varCfg.type === 'bool') {
      // 先读出当前字节，再修改指定位，写回
      const readBuf = Buffer.alloc(1);
      await plcClient.ReadArea(S7Consts.S7AreaDB, varCfg.dbNumber, varCfg.offset, 1, S7Consts.S7WLByte, readBuf);
      if (value) {
        readBuf[0] |= (1 << varCfg.bit);
      } else {
        readBuf[0] &= ~(1 << varCfg.bit);
      }
      await plcClient.WriteArea(S7Consts.S7AreaDB, varCfg.dbNumber, varCfg.offset, 1, S7Consts.S7WLByte, readBuf);
    } else {
      const writeBuf = Buffer.alloc(typeInfo.bytes);
      switch (varCfg.type) {
        case 'real':
          writeBuf.writeFloatBE(Number(value), 0);
          break;
        case 'int':
          writeBuf.writeInt16BE(Number(value), 0);
          break;
        case 'dint':
          writeBuf.writeInt32BE(Number(value), 0);
          break;
        case 'word':
          writeBuf.writeUInt16BE(Number(value), 0);
          break;
        case 'dword':
          writeBuf.writeUInt32BE(Number(value), 0);
          break;
        case 'byte':
          writeBuf.writeUInt8(Number(value), 0);
          break;
        default:
          return res.status(400).json({ error: `不支持的写入类型: ${varCfg.type}` });
      }
      await plcClient.WriteArea(S7Consts.S7AreaDB, varCfg.dbNumber, varCfg.offset, 1, typeInfo.wordLen, writeBuf);
    }

    // 更新缓存
    latestData[name] = { value, type: varCfg.type, writable: true, dbNumber: varCfg.dbNumber, offset: varCfg.offset };

    res.json({ success: true, name, value });
  } catch (err) {
    console.error(`[PLC] 写入 ${name} 失败:`, err.message);
    res.status(500).json({ error: `写入失败: ${err.message}` });
  }
});

/** SSE 实时推送 */
app.get('/api/plc/stream', (req, res) => {
  res.writeHead(200, {
    'Content-Type': 'text/event-stream',
    'Cache-Control': 'no-cache',
    Connection: 'keep-alive',
    'Access-Control-Allow-Origin': '*',
  });

  // 立即推一次当前数据
  res.write(`data: ${JSON.stringify(latestData)}\n\n`);

  sseClients.add(res);
  req.on('close', () => {
    sseClients.delete(res);
  });
});

// ─── 配置信息接口 ─────────────────────────────────────────
/** 返回 PLC 配置（用于前端动态渲染） */
app.get('/api/plc/config', (req, res) => {
  res.json({
    pollInterval: plcConfig.pollInterval,
    variables: plcConfig.variables.map(v => ({
      name: v.name,
      type: v.type,
      writable: !!v.writable,
    })),
  });
});

// ─── 启动 ──────────────────────────────────────────────────
const PORT = process.env.PORT || 3000;
const POLL_INTERVAL = plcConfig.pollInterval || 2000;

async function main() {
  // 先尝试连接 PLC
  await connectPLC();

  // 启动轮询
  setInterval(pollPLC, POLL_INTERVAL);

  // 启动一次立即读取
  setTimeout(pollPLC, 500);

  // 启动 HTTP 服务
  app.listen(PORT, () => {
    console.log(`\n========================================`);
    console.log(`  PLC 监控系统已启动`);
    console.log(`  http://localhost:${PORT}`);
    console.log(`  推流: http://localhost:${PORT}/api/plc/stream`);
    console.log(`========================================\n`);
  });
}

// 优雅退出
process.on('SIGINT', async () => {
  console.log('\n正在关闭...');
  await disconnectPLC();
  process.exit(0);
});

main().catch(err => {
  console.error('启动失败:', err);
  process.exit(1);
});
