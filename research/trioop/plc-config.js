/**
 * PLC 地址配置
 * =============
 *
 * 这是你唯一需要改的文件。
 * 按你的 TIA Portal 项目中的 DB 块变量表来填写。
 *
 * 支持的数据类型：
 *   real  → 4 字节浮点数（IEEE 754）
 *   int   → 2 字节有符号整数（-32768 ~ 32767）
 *   dint  → 4 字节有符号整数
 *   word  → 2 字节无符号整数（0 ~ 65535）
 *   dword → 4 字节无符号整数
 *   bool  → 1 位布尔值（需指定 bit 索引 0-7）
 *   byte  → 1 字节
 *
 * 设置 writable: true 后，仪表盘上会出现写入控件。
 */

module.exports = {
  /** PLC 连接信息 */
  plc: {
    ip: '192.168.1.100',   // ← 改成你的 PLC IP 地址
    rack: 0,                // S7-1200/1500 固定 rack=0
    slot: 1,                // S7-1200 固定 slot=1
  },

  /** 轮询间隔（毫秒），默认 2000ms = 2 秒 */
  pollInterval: 2000,

  /** 要监控的 DB 块变量列表
   *
   *  提示：
   *  - 相同 dbNumber 的变量会被合并为一次读取，提高效率
   *  - 按 offset 从小到大排列（方便检查）
   */
  variables: [
    // ─── DB1 设备状态与模拟量 ───
    { name: '设备运行状态',  dbNumber: 1, offset: 0, type: 'bool', bit: 0, writable: false },
    { name: '故障报警',      dbNumber: 1, offset: 0, type: 'bool', bit: 1, writable: false },
    { name: '温度_1',        dbNumber: 1, offset: 2, type: 'real',             writable: false },
    { name: '压力_1',        dbNumber: 1, offset: 6, type: 'real',             writable: false },
    { name: '电机转速',      dbNumber: 1, offset: 10, type: 'int',             writable: false },
    { name: '设定温度',      dbNumber: 1, offset: 12, type: 'real',            writable: true  },
    { name: '启动/停止',     dbNumber: 1, offset: 16, type: 'bool', bit: 0,    writable: true  },

    // ─── DB3 生产数据（示例，取消注释后可用）───
    // { name: '当前产量',   dbNumber: 3, offset: 0, type: 'dint', writable: false },
    // { name: '目标产量',   dbNumber: 3, offset: 4, type: 'dint', writable: true  },
    // { name: '运行时长',   dbNumber: 3, offset: 8, type: 'real', writable: false },
  ],
};
