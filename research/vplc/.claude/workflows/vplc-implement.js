export const meta = {
  name: 'vplc-features',
  description: '实现 vplc 全部 P0/P1 功能: RUN/STOP、S7 错误码、RTC、诊断缓冲区、OB 周期、LED',
  phases: [
    { title: '分析现有代码', detail: '读取 vplc.ts 结构，识别切入点和接口' },
    { title: '实现 RUN/STOP 状态机', detail: 'plcState + API + S7 状态码' },
    { title: '实现 S7 错误码系统', detail: '标准 Siemens 错误码返回' },
    { title: '实现系统时钟 RTC', detail: '读取/设置 PLC 系统时间' },
    { title: '实现诊断缓冲区', detail: '事件记录/查询 API' },
    { title: '实现 OB 扫描周期', detail: '扫描计数器 + 看门狗' },
    { title: '实现 LED 状态指示', detail: 'API 暴露 + 前端显示' },
    { title: '验证全部功能', detail: 'tsc 编译 + 启动 + API 测试' },
  ],
}

var basePath = 'C:/KesaData/Projects/Claude_msi2020/C_SHARP_Upper/research/vplc'

phase('分析现有代码')

var analysis = await agent('阅读 ' + basePath + '/vplc.ts 的全部代码，分析:\n1. 当前 S7 协议实现的入口在哪里\n2. 当前 PLC 状态管理在哪里(memory 对象)\n3. HTTP API 的路由在哪里\n4. 配置文件在哪里加载\n5. 需要新增哪些内部状态变量\n\n输出结构化分析结果，包括每段代码的行号范围。', {label: '分析 vplc.ts'})

var frontAnalysis = await agent('阅读 ' + basePath + '/frontend/src/App.tsx 的全部代码，分析:\n1. MonitorTab 的渲染结构\n2. API 调用方式\n3. 状态管理方式\n4. 需要增加哪些新的监视状态\n\n输出分析结果。', {label: '分析前端代码'})

log('分析完成，开始实现功能')

// Phase 2: RUN/STOP
phase('实现 RUN/STOP 状态机')

var runStop = await agent('在 ' + basePath + '/vplc.ts 中实现 RUN/STOP 状态机:\n\n## 要求\n\n### 新增状态变量\n- plcState: 值为 \'RUN\' | \'STOP\' | \'STARTUP\', 初始为 \'RUN\'\n- stateChangedAt: number — 状态变更时间戳\n\n### HTTP API\n- 在 _parsed 里添加: _parsed.state = { mode: plcState, since: stateChangedAt }\n- POST /api/vplc/state — body: { state: \'RUN\' | \'STOP\' }\n  - 切换到对应状态\n  - 在诊断缓冲区中记录事件\n  - STOP 时停止 simulate()，RUN 时重新启用\n  - 返回当前状态\n\n### 启动横幅\n- 横幅中显示 RUN/STOP 状态\n\n请直接修改 vplc.ts 文件实现上述功能。修改后确保能正常启动。\n\n现有代码的 import、memory 结构、S7 handler 等请保持兼容，不要破坏已有功能。', {isolation: 'worktree', label: '实现 RUN/STOP'})

log('RUN/STOP: ' + (runStop ? 'OK' : 'FAILED'))

// Phase 3: S7 错误码
phase('实现 S7 错误码系统')

var errCode = await agent('在 ' + basePath + '/vplc.ts 中实现 S7 标准错误码:\n\n### 错误码常量\nS7_ERR_OK=0xFF, S7_ERR_RESOURCE=0x01, S7_ERR_RANGE=0x05, S7_ERR_INVALID=0x06, S7_ERR_LOCKED=0x07\n\n### S7 Read 响应改进\n- 地址越界时返回 0x05 错误码(不是空 buffer)\n- s7ReadResponse 函数需支持返回错误码\n\n### S7 Write 响应改进\n- s7WriteResponse 函数需接受错误码参数\n- 地址越界 → 0x05\n- 写不存在的区域 → 0x06\n- STOP 状态 → 0x07\n- 成功 → 0xFF\n\n请直接修改 vplc.ts 文件。', {isolation: 'worktree', label: '实现 S7 错误码'})

log('S7 错误码: ' + (errCode ? 'OK' : 'FAILED'))

// Phase 4: RTC
phase('实现系统时钟 RTC')

var rtc = await agent('在 ' + basePath + '/vplc.ts 中实现 RTC(系统时钟)功能:\n\n### 内部时钟变量\n- rtcOffset: number = 0 — 与系统时间的偏移(毫秒)\n\n### 获取当前 PLC 时间\n- 实际时间 = Date.now() + rtcOffset\n- 转换为 ISO 字符串返回\n\n### HTTP API\n- GET /api/vplc/rtc — 返回 PLC 时间(ISO 字符串)\n- POST /api/vplc/rtc — body: { iso: string } 设置 PLC 时间\n  - 计算 offset = new Date(iso).getTime() - Date.now()\n  - body: { offset: number } 直接设置偏移量\n  - 返回设置后的 PLC 时间\n\n### memorySnapshot\n- 添加 _parsed.rtc = { iso: string, local: string }\n\n请直接修改 vplc.ts 文件。', {isolation: 'worktree', label: '实现 RTC'})

log('RTC: ' + (rtc ? 'OK' : 'FAILED'))

// Phase 5: 诊断缓冲区
phase('实现诊断缓冲区')

var diag = await agent('在 ' + basePath + '/vplc.ts 中实现诊断缓冲区:\n\n### 数据结构\n- diagBuffer: 数组，每项 { id, timestamp, category: \'info\'|\'warn\'|\'error\', source, message, detail }\n- 最多保留 200 条\n\n### 记录事件\n在以下操作时记录:\n- PLC 状态切换: info, \'STATE\', \'PLC 状态: X → Y\'\n- S7 Write 失败: warn, \'S7_WRITE\', \'写入失败: 原因\'\n- RTC 设置: info, \'RTC\', \'PLC 时间设置为 ...\'\n- 启动完成: info, \'SYSTEM\', \'VPLC 启动完成\'\n\n### HTTP API\n- GET /api/vplc/diag — 返回 diagBuffer 全部(倒序)\n- GET /api/vplc/diag?limit=20&category=warn — 支持过滤\n- DELETE /api/vplc/diag — 清空\n\n### 辅助函数\naddDiag(category, source, message, detail)\n\n请直接修改 vplc.ts 文件。', {isolation: 'worktree', label: '实现诊断缓冲区'})

log('诊断缓冲区: ' + (diag ? 'OK' : 'FAILED'))

// Phase 6: OB 扫描周期
phase('实现 OB 扫描周期')

var ob = await agent('在 ' + basePath + '/vplc.ts 中实现 OB 扫描周期:\n\n### obCycles 数组\n[\n  { obNumber: 1,  interval: 0, lastExec: 0, execCount: 0, maxCycleTime: 0, minCycleTime: 999999 },\n  { obNumber: 100,interval: 0, lastExec: 0, execCount: 0, maxCycleTime: 0, minCycleTime: 999999 },\n  { obNumber: 35, interval: 500, lastExec: 0, execCount: 0, maxCycleTime: 0, minCycleTime: 999999 },\n]\n\n### 执行逻辑\n- OB1: 每次 simulate() 调用时执行\n- OB100: 仅在 STARTUP → RUN 时执行一次\n- OB35: 每 500ms 独立定时器执行\n\n### HTTP API\n- GET /api/vplc/ob — 返回 obCycles 统计\n- POST /api/vplc/ob/:obNumber/reset — 重置计数器\n\n### memorySnapshot _parsed\n- _parsed.ob = obCycles 数组\n\n请直接修改 vplc.ts 文件。', {isolation: 'worktree', label: '实现 OB 周期'})

log('OB 周期: ' + (ob ? 'OK' : 'FAILED'))

// Phase 7: LED
phase('实现 LED 状态指示')

var led = await agent('在 ' + basePath + '/vplc.ts 中实现 S7-1200 LED 状态:\n\n### LED 状态\nplcLEDs = {\n  RUN: { color: \'green\', state: \'off\'|\'on\'|\'blink\' },\n  STOP: { color: \'orange\',... },\n  ERROR: { color: \'red\',... },\n  MAINT: { color: \'yellow\',... },\n}\n\n### LED 逻辑\n- RUN 状态: RUN=on, STOP=off, ERROR=off, MAINT=off\n- STOP 状态: RUN=off, STOP=on, ERROR=off, MAINT=off\n- STARTUP: RUN=blink, STOP=off, ERROR=off, MAINT=off\n\n### HTTP API\n- GET /api/vplc/leds — 返回 LED 状态\n\n### memorySnapshot _parsed\n- _parsed.leds = { run: {color,state}, stop: {color,state}, error: {color,state}, maint: {color,state} }\n\n请直接修改 vplc.ts 文件。', {isolation: 'worktree', label: '实现 LED'})

log('LED: ' + (led ? 'OK' : 'FAILED'))

// Phase 8: 验证
phase('验证全部功能')

var verify = await agent('验证 vplc 全部新功能:\n\n### 1. 前端编译检查\ncd ' + basePath + '/frontend && npx tsc --noEmit\n\n### 2. 启动后端验证 API\n用正确的 Node: "C:/Users/admin/AppData/Local/nvs/default/node.exe" node_modules/.pnpm/tsx@4.22.4/node_modules/tsx/dist/cli.mjs vplc/vplc.ts\n\n验证以下 API 端点:\n- GET http://localhost:1201/api/vplc → 检查 _parsed.state, _parsed.rtc, _parsed.leds, _parsed.ob\n- POST http://localhost:1201/api/vplc/state {state:\'STOP\'} → 验证切换\n- POST http://localhost:1201/api/vplc/state {state:\'RUN\'} → 验证切换\n- GET http://localhost:1201/api/vplc/rtc → 验证时间\n- POST http://localhost:1201/api/vplc/rtc {iso:\'2026-07-03T12:00:00Z\'} → 设置时间\n- GET http://localhost:1201/api/vplc/diag → 验证诊断事件\n- GET http://localhost:1201/api/vplc/ob → 验证 OB 统计\n- GET http://localhost:1201/api/vplc/leds → 验证 LED 状态\n\n输出每个测试的完整结果。失败的必须包含错误详情。', {phase: '验证全部功能', schema: {properties: {allPassed: {type: 'boolean'}, details: {items: {properties: {name: {type: 'string'}, passed: {type: 'boolean'}, output: {type: 'string'}}, required: ['name', 'passed', 'output'], type: 'object'}, type: 'array'}, summary: {type: 'string'}}, required: ['allPassed', 'details', 'summary'], type: 'object'}})

if (verify.allPassed) {
  log('全部验证通过! ' + verify.summary)
} else {
  log('部分失败: ' + verify.summary)
  for (var i = 0; i < verify.details.length; i++) {
    var d = verify.details[i]
    if (!d.passed) log('  FAIL: ' + d.name + ' - ' + d.output)
  }
}

return verify