import type { PLCConfig } from '../shared/types.js'

/**
 * PLC 地址配置
 * 按你的 TIA Portal DB 块变量表修改这里
 * I 区和 Q 区由前端自动读取字节范围，无需在此配置
 */
const config: PLCConfig = {
  plc: {
    ip: '192.168.0.1',
    rack: 0,
    slot: 1,     // S7-1200 固定 slot=1
  },

  pollInterval: 1000,  // 1秒轮询（I/Q 区每 2 秒更新一次）

  variables: [
    // ─── 在这里按实际 PLC 配置 DB 变量 ───
    // 格式: { name: '变量名', dbNumber: 1, offset: 0, type: 'bool', bit: 0 }
    //
    // 例: 运行状态 / 故障报警 / 报警码 等
    // { name: '运行状态',  dbNumber: 1, offset: 0, type: 'bool', bit: 0 },
    // { name: '故障报警',  dbNumber: 1, offset: 0, type: 'bool', bit: 1 },
    // { name: '报警码',    dbNumber: 1, offset: 2, type: 'word' },
  ],
}

export default config
