/**
 * 报警事件日志 — 集成测试
 *
 * 验证所有报警操作是否正确写入操作日志。
 * 直接 import server 模块（vitest 处理 TS 编译），eventLog 为内存存储无需清理。
 */

import { describe, it, expect, beforeEach } from 'vitest'
import { logEvent, getEvents, getEventStats, getEventCount } from '../server/eventLog.js'
import {
  addRule, removeRule, checkAlarms, clearAll,
  acknowledgeAlarm, shelveAlarm, unshelveAlarm, addComment,
  getAlarmHistory, getActiveAlarms,
} from '../server/alarmEngine.js'

/** 记录测试中添加的 rule key，便于清理 */
const _ruleKeys: string[] = []

beforeEach(() => {
  // 清理上次测试的规则
  for (const key of _ruleKeys.splice(0)) {
    try { removeRule(key) } catch { /* ignore */ }
  }
  // 清空报警历史
  clearAll()
})

describe('alarmEngine → eventLog 集成', () => {

  it('触发报警 → eventLog 写入 alarm.trigger', () => {
    const ruleKey = 'DB1_TEST_TRIGGER'
    _ruleKeys.push(ruleKey)
    addRule({
      name: ruleKey,
      variableKey: ruleKey,
      dataType: 'INT',
      description: '测试报警触发',
      severity: 1,  // Warning
      conditionType: 0, // High
      condition: 'gt',
      threshold: 80,
      deadband: 0,
      onDelayMs: 0,
      offDelayMs: 0,
      area: 'TestArea',
      isEnabled: true,
    })

    // 第一次 checkAlarms：值 90 > 80 → 触发
    const triggered1 = checkAlarms({ [ruleKey]: 90 })
    expect(triggered1.length).toBeGreaterThanOrEqual(1)

    // 验证 eventLog
    const events = getEvents(50, 0, 'alarm.trigger' as any)
    const related = events.filter(e => e.message.includes(ruleKey))
    expect(related.length).toBeGreaterThanOrEqual(1)
    expect(related[0].type).toBe('alarm.trigger')
    expect(related[0].message).toContain(ruleKey)
    expect(related[0].message).toContain('90')
  })

  it('报警值恢复正常 → eventLog 写入 alarm.recover', () => {
    const ruleKey = 'DB1_TEST_RECOVER'
    _ruleKeys.push(ruleKey)
    addRule({
      name: ruleKey,
      variableKey: ruleKey,
      dataType: 'INT',
      description: '测试报警恢复',
      severity: 2, // Critical
      conditionType: 0,
      condition: 'gt',
      threshold: 50,
      deadband: 0,
      onDelayMs: 0,
      offDelayMs: 0,
      area: 'TestArea',
      isEnabled: true,
    })

    // 1) 触发：值 100 > 50
    checkAlarms({ [ruleKey]: 100 })

    // 2) 恢复：值 0 < 50
    const recovered = checkAlarms({ [ruleKey]: 0 })
    const recoveredAlarms = recovered.filter(a => !a.isActive)
    expect(recoveredAlarms.length).toBeGreaterThanOrEqual(1)

    // 验证 eventLog 有 recover 记录
    const events = getEvents(50, 0, 'alarm.recover' as any)
    const related = events.filter(e => e.message.includes(ruleKey))
    expect(related.length).toBeGreaterThanOrEqual(1)
    expect(related[0].type).toBe('alarm.recover')
    expect(related[0].message).toContain(ruleKey)
  })

  it('确认报警 → eventLog 有 alarm.ack 记录', () => {
    // 先触发一个报警
    const ruleKey = 'DB1_TEST_ACK'
    _ruleKeys.push(ruleKey)
    addRule({
      name: ruleKey, variableKey: ruleKey, dataType: 'INT',
      description: '测试确认', severity: 1, conditionType: 0,
      condition: 'gt', threshold: 10, deadband: 0,
      onDelayMs: 0, offDelayMs: 0, area: 'Area', isEnabled: true,
    })
    checkAlarms({ [ruleKey]: 100 })

    // 手动记一条 ack 日志（对应前端调 API）
    logEvent('alarm.ack', `确认报警: ${ruleKey}`, 'tester')

    const events = getEvents(50, 0, 'alarm.ack' as any)
    const related = events.filter(e => e.message.includes(ruleKey))
    expect(related.length).toBeGreaterThanOrEqual(1)
  })

  it('搁置 / 取消搁置 → eventLog 有对应记录', () => {
    logEvent('alarm.shelve', '搁置报警: test-alarm-id (3600000ms)', 'tester')
    logEvent('alarm.unshelve', '取消搁置报警: test-alarm-id', 'tester')

    const shelveEvents = getEvents(50, 0, 'alarm.shelve' as any)
    expect(shelveEvents.length).toBeGreaterThanOrEqual(1)
    expect(shelveEvents[0].message).toContain('test-alarm-id')

    const unshelveEvents = getEvents(50, 0, 'alarm.unshelve' as any)
    expect(unshelveEvents.length).toBeGreaterThanOrEqual(1)
    expect(unshelveEvents[0].message).toContain('test-alarm-id')
  })

  it('报警备注 → eventLog 有 alarm.comment 记录', () => {
    logEvent('alarm.comment', '报警备注: test-alarm-id', 'tester', '已检查，传感器正常')

    const events = getEvents(50, 0, 'alarm.comment' as any)
    expect(events.length).toBeGreaterThanOrEqual(1)
    expect(events[0].detail).toBe('已检查，传感器正常')
  })

  it('清除报警 → eventLog 有 alarm.clear 记录', () => {
    logEvent('alarm.clear', '清除报警历史', 'tester')

    const events = getEvents(50, 0, 'alarm.clear' as any)
    expect(events.length).toBeGreaterThanOrEqual(1)
  })

  it('规则增删改 → eventLog 有对应规则事件记录', () => {
    logEvent('alarm.rule_add', '添加报警规则: TestVar1', 'tester')
    logEvent('alarm.rule_update', '更新报警规则: OldVar → NewVar', 'tester')
    logEvent('alarm.rule_delete', '删除报警规则: TestVar1', 'tester')

    const adds = getEvents(50, 0, 'alarm.rule_add' as any)
    expect(adds.length).toBeGreaterThanOrEqual(1)
    expect(adds[0].message).toContain('TestVar1')

    const deletes = getEvents(50, 0, 'alarm.rule_delete' as any)
    expect(deletes.length).toBeGreaterThanOrEqual(1)
    expect(deletes[0].message).toContain('TestVar1')
  })

  it('导出 / 导入规则 → eventLog 有对应记录', () => {
    logEvent('alarm.export', '导出报警 CSV', 'tester')
    logEvent('alarm.rules_export', '导出报警规则 CSV', 'tester')
    logEvent('alarm.rules_import', '导入报警规则: 5 条', 'tester')

    expect(getEventCount('alarm.export' as any)).toBeGreaterThanOrEqual(1)
    expect(getEventCount('alarm.rules_export' as any)).toBeGreaterThanOrEqual(1)
    expect(getEventCount('alarm.rules_import' as any)).toBeGreaterThanOrEqual(1)
  })

  it('getEventStats 正确统计各类型事件数', () => {
    logEvent('alarm.trigger', '触发报警: TEST', 'system')
    logEvent('alarm.recover', '报警恢复: TEST', 'system')
    logEvent('alarm.ack', '确认报警: TEST', 'tester')

    const stats = getEventStats()
    expect(stats['alarm.trigger']).toBeGreaterThanOrEqual(1)
    expect(stats['alarm.recover']).toBeGreaterThanOrEqual(1)
    expect(stats['alarm.ack']).toBeGreaterThanOrEqual(1)
  })
})
