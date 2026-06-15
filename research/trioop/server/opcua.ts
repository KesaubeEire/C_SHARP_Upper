/**
 * OPC UA 通信模块
 * 通过 OPC UA 协议读取 S7-1200 DB 块数据
 */

import { OPCUAClient, AttributeIds, makeBrowsePath, DataValue } from 'node-opcua'
import type { BrowseResult, NodeIdLike } from 'node-opcua'

const DEFAULT_PORT = 4840
const DEFAULT_TIMEOUT = 10000

let client: OPCUAClient | null = null
let session: any = null
let _connected = false

export function isConnected(): boolean {
  return _connected
}

/** OPC UA 节点值缓存 */
let _nodeCache: Record<string, any> = {}

export function getNodeCache(): Record<string, any> {
  return _nodeCache
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

    // 创建会话
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

/** 断开连接 */
export async function disconnect(): Promise<void> {
  try {
    if (session) await session.close()
  } catch {}
  try {
    if (client) await client.disconnect()
  } catch {}
  client = null
  session = null
  _connected = false
  _nodeCache = {}
}

/** 浏览节点的子节点 */
export async function browse(nodeId: NodeIdLike): Promise<{ nodeId: string; browseName: string; displayName: string; nodeClass: string }[]> {
  if (!session) throw new Error('OPC UA 未连接')

  const result = await session.browse(nodeId)
  const nodes: any[] = []
  for (const ref of result.references ?? []) {
    nodes.push({
      nodeId: ref.nodeId.toString(),
      browseName: ref.browseName?.toString() ?? '',
      displayName: ref.displayName?.text ?? '',
      nodeClass: ref.nodeClass?.toString() ?? '',
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
      const key = nodeIds[i].toString()
      data[key] = dv.value.value
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
    value: {
      value,
    },
  })
}

/** 订阅节点变更（简化版：轮询方式） */
export async function pollNodes(nodeIds: NodeIdLike[]): Promise<Record<string, any>> {
  const data = await readNodes(nodeIds)
  for (const [key, val] of Object.entries(data)) {
    _nodeCache[key] = val
  }
  return data
}

/** 浏览 PLC 的 DB 块结构 */
export async function browsePLC(): Promise<any> {
  if (!session) throw new Error('OPC UA 未连接')

  // S7-1200 OPC UA 地址空间通常为: Objects → PLC → DB blocks
  const objectsNode = 'i=85'  // Objects folder

  // 浏览 Objects 下的子节点，找 PLC 相关的
  const objects = await browse(objectsNode)
  const plcNode = objects.find(n => n.displayName === 'PLC' || n.displayName === 'Controller')
  if (!plcNode) return objects  // 返回 Objects 下的所有节点

  // 浏览 PLC 节点下找 DB 块
  const plcChildren = await browse(plcNode.nodeId)
  return {
    plc: plcNode,
    children: plcChildren,
  }
}
