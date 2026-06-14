import type { PLCConfig } from '../shared/types.js'

/**
 * PLC 地址配置
 * 按你的 TIA Portal DB 块变量表修改这里
 */
const config: PLCConfig = {
  plc: {
    ip: '192.168.1.100',
    rack: 0,
    slot: 1,     // S7-1200 固定 slot=1
  },

  pollInterval: 2000,

  variables: [
    // ─── DB1 设备状态与模拟量 ───
    { name: '设备运行状态',  dbNumber: 1, offset: 0, type: 'bool', bit: 0 },
    { name: '故障报警',      dbNumber: 1, offset: 0, type: 'bool', bit: 1 },
    { name: '温度_1',        dbNumber: 1, offset: 2, type: 'real' },
    { name: '压力_1',        dbNumber: 1, offset: 6, type: 'real' },
    { name: '电机转速',      dbNumber: 1, offset: 10, type: 'int' },
    { name: '设定温度',      dbNumber: 1, offset: 12, type: 'real', writable: true },
    { name: '启动/停止',     dbNumber: 1, offset: 16, type: 'bool', bit: 0, writable: true },

    // ─── DB3 生产数据（取消注释后可用）───
    // { name: '当前产量',   dbNumber: 3, offset: 0, type: 'dint' },
    // { name: '目标产量',   dbNumber: 3, offset: 4, type: 'dint', writable: true },
  ],
}

export default config
