/**
 * OPC UA 通信模块
 *
 * 通过 OPC UA Subscription（订阅）实时接收 S7-1200 DB 变量变更，
 * 变量变了 PLC 主动推，不轮询。
 */

import { OPCUAClient, AttributeIds } from 'node-opcua'
import type { NodeIdLike, ClientSubscription, ClientMonitoredItem } from 'node-opcua'

const DEFAULT_PORT = 4840
const DEFAULT_TIMEOUT = 10000

let client: OPCUAClient | null = null
let session: any = null
let _connected = false

// ─── 订阅相关 ─────────────────────────────────────────────
let _subscription: ClientSubscription | null = null
const _monitoredItems: Map<string, { item: ClientMonitoredItem; name: string }> = new Map()

export function isConnected(): boolean {
  return _connected
}

export function hasActiveSubscription(): boolean {
  return _subscription !== null && !_subscription.isTerminated()
}

/** 连接 OPC UA 服务器 */
export async function connect(ip: string, port?: number, username?: string, password?: string): Promise<void> {
  const endpoint = `opc.tcp://${ip}:${port ?? DEFAULT_PORT}`

  client = OPCUAClient.create({
    endpoint_must_exist: false,
    connectionStrategy: {
      initialDelay: 1000,
      maxRetry: 1,
    },
    timeout: DEFAULT_TIMEOUT,
  })

  try {
    await client.connect(endpoint)

    if (username && password) {
      session = await client.createSession({ userName: username, password })
    } else {
      session = await client.createSession()
    }

    _connected = true
  } catch (err) {
    _connected = false
    throw new Error(`OPC UA 连接失败: ${(err as Error).message}`)
  }
}

/** 断开连接（自动清理订阅） */
export async function disconnect(): Promise<void> {
  await unsubscribeAll()
  try {
    if (session) await session.close()
  } catch {}
  try {
    if (client) await client.disconnect()
  } catch {}
  client = null
  session = null
  _connected = false
  _monitoredItems.clear()
}

/** 浏览节点的子节点 */
export async function browse(nodeId: NodeIdLike): Promise<{ nodeId: string; browseName: string; displayName: string; nodeClass: string }[]> {
  if (!session) throw new Error('OPC UA 未连接')

  const result = await session.browse(nodeId)
  const nodes: any[] = []
  const ncMap: Record<number, string> = { 1: 'Object', 2: 'Variable', 4: 'Method', 8: 'ObjectType', 16: 'VariableType', 32: 'ReferenceType', 64: 'DataType', 128: 'View' }
  for (const ref of result.references ?? []) {
    const rawClass = typeof ref.nodeClass === 'number' ? ref.nodeClass : Number(ref.nodeClass)
    nodes.push({
      nodeId: ref.nodeId.toString(),
      browseName: ref.browseName?.toString() ?? '',
      displayName: ref.displayName?.text ?? '',
      nodeClass: ncMap[rawClass] ?? String(rawClass),
    })
  }
  return nodes
}

/** 读取节点值 */
export async function readNode(nodeId: NodeIdLike): Promise<{ value: any; dataType: string }> {
  if (!session) throw new Error('OPC UA 未连接')

  const dataValue = await session.read({
    nodeId,
    attributeId: AttributeIds.Value,
  })

  return {
    value: dataValue.value.value,
    dataType: dataValue.value.dataType?.toString() ?? 'Unknown',
  }
}

/** 批量读取节点值 */
export async function readNodes(nodeIds: NodeIdLike[]): Promise<Record<string, any>> {
  if (!session) throw new Error('OPC UA 未连接')

  const results = await session.read(nodeIds.map(id => ({
    nodeId: id,
    attributeId: AttributeIds.Value,
  })))

  const data: Record<string, any> = {}
  for (let i = 0; i < nodeIds.length; i++) {
    const dv = results[i]
    if (dv.statusCode.isGood()) {
      data[nodeIds[i].toString()] = dv.value.value
    }
  }

  return data
}

/** 写入节点值 */
export async function writeNode(nodeId: NodeIdLike, value: any): Promise<void> {
  if (!session) throw new Error('OPC UA 未连接')

  await session.write({
    nodeId,
    attributeId: AttributeIds.Value,
    value: { value },
  })
}

/** 浏览 PLC 的 DB 块结构 */
export async function browsePLC(): Promise<any> {
  if (!session) throw new Error('OPC UA 未连接')

  const objectsNode = 'i=85'  // Objects folder
  const objects = await browse(objectsNode)
  const plcNode = objects.find(n => n.displayName === 'PLC' || n.displayName === 'Controller')
  if (!plcNode) return objects

  const plcChildren = await browse(plcNode.nodeId)
  return { plc: plcNode, children: plcChildren }
}

/**
 * 按变量名列表自动搜索 OPC UA 地址空间，找到匹配的 nodeId
 *
 * 从 Objects 开始递归浏览，收集所有 Variable 节点，与 names 匹配
 */
export async function findVariablesByName(names: string[]): Promise<{ name: string; nodeId: string }[]> {
  if (!session) throw new Error('OPC UA 未连接')
  if (names.length === 0) return []

  const allVars: { displayName: string; nodeId: string }[] = []

  async function walk(nodeId: string, depth: number) {
    if (depth > 6) return  // 限制深度
    try {
      const nodes = await browse(nodeId)
      for (const n of nodes) {
        if (n.nodeClass === 'Variable') {
          allVars.push({ displayName: n.displayName, nodeId: n.nodeId })
        }
        if (n.nodeClass === 'Object' || n.nodeClass === 'Variable') {
          await walk(n.nodeId, depth + 1)
        }
      }
    } catch { /* 跳过无权限节点 */ }
  }

  await walk('i=85', 0)

  // 精确匹配 → 后缀匹配 → 忽略大小写包含
  const results: { name: string; nodeId: string }[] = []
  for (const name of names) {
    let found = allVars.find(v => v.displayName === name)
    if (!found) found = allVars.find(v => v.displayName.endsWith(name))
    if (!found) found = allVars.find(v => v.displayName.toLowerCase().includes(name.toLowerCase()))
    if (found) results.push({ name, nodeId: found.nodeId })
  }
  return results
}

/**
 * 返回 OPC UA 地址空间中所有 Variable 节点的 flat 列表
 * 用于调试和手动配映射
 */
export async function getAllVariables(): Promise<{ displayName: string; nodeId: string }[]> {
  if (!session) throw new Error('OPC UA 未连接')
  const allVars: { displayName: string; nodeId: string }[] = []

  async function walk(nodeId: string, depth: number) {
    if (depth > 6) return
    try {
      const nodes = await browse(nodeId)
      for (const n of nodes) {
        if (n.nodeClass === 'Variable') {
          allVars.push({ displayName: n.displayName, nodeId: n.nodeId })
        }
        if (n.nodeClass === 'Object' || n.nodeClass === 'Variable') {
          await walk(n.nodeId, depth + 1)
        }
      }
    } catch {}
  }

  await walk('i=85', 0)
  return allVars
}

// ─── Subscription 订阅（这才是 OPC UA 的正确用法） ──────

/**
 * 创建 Subscription，订阅一组节点
 *
 * @param items  [{ nodeId: string, name: string }]   nodeId 和变量名
 * @param onChange  (name, value) => void  值变了就回调，name 是你传入的 name
 * @param publishingInterval  发布间隔(ms)，默认 200ms
 */
export async function subscribe(
  items: { nodeId: string; name: string }[],
  onChange: (name: string, value: any) => void,
  publishingInterval: number = 200,
): Promise<void> {
  if (!session) throw new Error('OPC UA 未连接')
  if (items.length === 0) return

  // 如果已有订阅，追加到现有订阅里
  if (_subscription && !_subscription.isTerminated()) {
    await _addMonitoredItems(items, onChange)
    return
  }

  // 创建新订阅
  _subscription = await session.createSubscription({
    requestedPublishingInterval: publishingInterval,
    requestedLifetimeCount: 1000,
    requestedMaxKeepAliveCount: 20,
    maxNotificationsPerPublish: 1000,
    publishingEnabled: true,
    priority: 10,
  })

  _subscription.on('terminated', () => {
    _monitoredItems.clear()
    _subscription = null
  })

  await _addMonitoredItems(items, onChange)
}

/**
 * 往现有订阅里追加监控项
 */
async function _addMonitoredItems(
  items: { nodeId: string; name: string }[],
  onChange: (name: string, value: any) => void,
): Promise<void> {
  if (!_subscription || _subscription.isTerminated()) return

  for (const { nodeId, name } of items) {
    // 跳过已在监控的
    if (_monitoredItems.has(nodeId)) continue

    const item = await _subscription.monitor({
      nodeId,
      attributeId: AttributeIds.Value,
    }, {
      samplingInterval: 100,    // 采样间隔 100ms
      discardOldest: true,
      queueSize: 10,
    })

    item.on('changed', (dataValue: any) => {
      const val = dataValue?.value?.value
      if (val !== undefined && val !== null) {
        onChange(name, val)
      }
    })

    _monitoredItems.set(nodeId, { item, name })
  }
}

/** 取消全部订阅 */
export async function unsubscribeAll(): Promise<void> {
  if (_subscription) {
    try {
      await _subscription.terminate()
    } catch {}
    _subscription = null
    _monitoredItems.clear()
  }
}

/** 当前订阅的变量信息 */
export function getSubscribedItems(): { nodeId: string; name: string }[] {
  return Array.from(_monitoredItems.entries()).map(([nodeId, { name }]) => ({ nodeId, name }))
}

/** 节点值缓存（最近一次变更的值） */
const _valueCache: Record<string, any> = {}

export function getValueCache(): Record<string, any> {
  return _valueCache
}

/** 订阅 + 自动缓存（供 index.ts 用） */
export async function subscribeWithCache(
  items: { nodeId: string; name: string }[],
  publishingInterval?: number,
): Promise<void> {
  await subscribe(items, (name, value) => {
    _valueCache[name] = value
  }, publishingInterval)
}
