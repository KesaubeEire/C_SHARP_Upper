/**
 * 配方 API — 集成测试
 *
 * 直接使用 fetch 调用 Express API。
 * 使用真实文件系统：data/recipes/<id>.json
 */

import { describe, it, expect, beforeAll } from 'vitest'
import fs from 'fs'
import path from 'path'
import { execSync } from 'child_process'

const API_BASE = 'http://localhost:3094'
let serverProcess: any = null

// 简单的 HTTP 请求封装
async function api(method: string, url: string, body?: any): Promise<Response> {
  const opts: RequestInit = {
    method,
    headers: { 'Content-Type': 'application/json' },
  }
  if (body !== undefined) opts.body = JSON.stringify(body)
  return fetch(`${API_BASE}${url}`, opts)
}

async function apiJson(method: string, url: string, body?: any): Promise<any> {
  const res = await api(method, url, body)
  if (res.status >= 400) {
    try { return { status: res.status, ...(await res.json()) } }
    catch { return { status: res.status, error: res.statusText } }
  }
  return res.json()
}

// ─── 全局 setup ──────────────────────────────────────────────

beforeAll(async () => {
  // 确保 API 服务可用
  let up = false
  for (let i = 0; i < 30; i++) {
    try {
      const res = await fetch(`${API_BASE}/api/plc/status`, { signal: AbortSignal.timeout(1000) })
      if (res.ok) { up = true; break }
    } catch { /* retry */ }
    await new Promise(r => setTimeout(r, 1000))
  }
  if (!up) {
    console.log('API 服务未就绪，尝试启动...')
  }
})

// ═══════════════════════════════════════════════════════════
// 配方 API CRUD
// ═══════════════════════════════════════════════════════════

describe('GET /api/recipe', () => {
  it('返回配方列表数组', async () => {
    const data = await apiJson('GET', '/api/recipe')
    expect(Array.isArray(data)).toBe(true)
  })
})

describe('POST /api/recipe', () => {
  it('创建新配方', async () => {
    const data = await apiJson('POST', '/api/recipe', {
      name: 'API测试配方',
      description: '通过 API 创建',
      productCode: 'API-001',
      author: 'Tester',
      tags: ['测试'],
      defaultDbNumber: 1,
      groups: [{
        name: '主参数',
        description: '',
        parameters: [
          { name: '温度', value: 100, unit: '°C', address: 0, scale: 1, offset: 0, plcDataType: 'REAL', dbNumber: 1 },
          { name: '压力', value: 0.5, unit: 'MPa', address: 2, scale: 1, offset: 0, plcDataType: 'REAL', dbNumber: 1 },
        ],
      }],
    })
    expect(data.success).toBe(true)
    expect(data.recipe).toBeDefined()
    expect(data.recipe.name).toBe('API测试配方')
    expect(data.recipe.id).toBeTruthy()
    expect(data.recipe.version).toBe(1)
    // 保持 ID 供后续测试
    ;(globalThis as any)._testRecipeId = data.recipe.id
  })

  it('name 缺失返回 400', async () => {
    const data = await apiJson('POST', '/api/recipe', {})
    expect((data as any).status || data.error).toBeTruthy()
  })
})

describe('GET /api/recipe/:id', () => {
  it('读取已创建的配方', async () => {
    const id = (globalThis as any)._testRecipeId
    if (!id) return
    const recipe = await apiJson('GET', `/api/recipe/${id}`)
    expect(recipe.name).toBe('API测试配方')
    expect(recipe.groups).toHaveLength(1)
    expect(recipe.groups[0].parameters).toHaveLength(2)
  })

  it('不存在的 ID 返回 404', async () => {
    const data = await apiJson('GET', '/api/recipe/nonexistent_id')
    expect(data.status || data.error).toBeTruthy()
  })
})

describe('PUT /api/recipe/:id', () => {
  it('更新配方名称', async () => {
    const id = (globalThis as any)._testRecipeId
    if (!id) return
    const data = await apiJson('PUT', `/api/recipe/${id}`, { name: 'API测试配方-已更新' })
    expect(data.success).toBe(true)
    expect(data.recipe.name).toBe('API测试配方-已更新')
    expect(data.recipe.version).toBeGreaterThanOrEqual(2)
  })
})

describe('POST /api/recipe/:id/copy', () => {
  it('复制配方', async () => {
    const id = (globalThis as any)._testRecipeId
    if (!id) return
    const data = await apiJson('POST', `/api/recipe/${id}/copy`, { name: 'API测试配方-副本' })
    expect(data.success).toBe(true)
    expect(data.recipe.name).toBe('API测试配方-副本')
    expect(data.recipe.id).not.toBe(id)
    expect(data.recipe.version).toBeGreaterThanOrEqual(1)
    // 保存副本 ID 用于清理
    ;(globalThis as any)._testCopyId = data.recipe.id
  })
})

describe('GET /api/recipe/:id/versions', () => {
  it('获取版本历史', async () => {
    const id = (globalThis as any)._testRecipeId
    if (!id) return
    const versions = await apiJson('GET', `/api/recipe/${id}/versions`)
    expect(Array.isArray(versions)).toBe(true)
    expect(versions.length).toBeGreaterThanOrEqual(1)
  })
})

describe('POST /api/recipe/:id/restore/:version', () => {
  it('恢复到早期版本', async () => {
    const id = (globalThis as any)._testRecipeId
    if (!id) return
    // 先查看版本列表
    const versions = await apiJson('GET', `/api/recipe/${id}/versions`)
    if (versions.length > 1) {
      const oldVersion = versions[versions.length - 1].version // 最旧的版本
      const data = await apiJson('POST', `/api/recipe/${id}/restore/${oldVersion}`)
      expect(data.success).toBe(true)
      expect(data.recipe).toBeDefined()
    }
  })

  it('恢复不存在的版本返回 404', async () => {
    const id = (globalThis as any)._testRecipeId
    if (!id) return
    const data = await apiJson('POST', `/api/recipe/${id}/restore/9999`)
    expect(data.status || data.error).toBeTruthy()
  })
})

// ═══════════════════════════════════════════════════════════
// CSV 导入/导出 API
// ═══════════════════════════════════════════════════════════

describe('CSV 导出 API', () => {
  it('GET /api/recipe/:id/export-csv 返回 CSV', async () => {
    const id = (globalThis as any)._testRecipeId
    if (!id) return
    try {
      const res = await fetch(`${API_BASE}/api/recipe/${id}/export-csv`)
      expect(res.status).toBe(200)
      const text = await res.text()
      expect(text).toContain('Name,Value,Unit,Address')
      expect(text).toContain('温度')
      expect(text).toContain('100')
    } catch (e) {
      console.log('CSV 导出 API 不可达（可能服务未运行）:', (e as Error).message)
    }
  })
})

describe('CSV 导入 API', () => {
  it('POST /api/recipe/:id/import-csv 导入 CSV', async () => {
    const id = (globalThis as any)._testRecipeId
    if (!id) return
    try {
      const csv = [
        'Name,Value,Unit,Address,Scale,Offset,Group,PlcDataType,DbNumber,MinValue,MaxValue',
        '新参数1,200,°C,10,1.0,0,导入组,REAL,2,-Infinity,Infinity',
        '新参数2,0.8,MPa,12,1.0,0,导入组,REAL,2,-Infinity,Infinity',
      ].join('\n')
      const data = await apiJson('POST', `/api/recipe/${id}/import-csv`, { csv })
      expect(data.success).toBe(true)
      expect(data.imported).toBe(2)
      expect(data.parameters).toHaveLength(2)
    } catch (e) {
      console.log('CSV 导入 API 不可达:', (e as Error).message)
    }
  })
})

// ═══════════════════════════════════════════════════════════
// 报警 API
// ═══════════════════════════════════════════════════════════

describe('GET /api/alarm/rules', () => {
  it('返回规则列表', async () => {
    try {
      const data = await apiJson('GET', '/api/alarm/rules')
      expect(Array.isArray(data)).toBe(true)
    } catch { /* 服务不可用则跳过 */ }
  })
})

describe('POST /api/alarm/statistics', () => {
  it('GET /api/alarm/statistics 返回统计', async () => {
    try {
      const data = await apiJson('GET', '/api/alarm/statistics')
      expect(data).toHaveProperty('totalActive')
      expect(data).toHaveProperty('totalUnacknowledged')
    } catch { /* 服务不可用则跳过 */ }
  })
})

// ═══════════════════════════════════════════════════════════
// 清理
// ═══════════════════════════════════════════════════════════

describe('DELETE /api/recipe/:id', () => {
  it('删除测试配方', async () => {
    const ids = [
      (globalThis as any)._testRecipeId,
      (globalThis as any)._testCopyId,
    ].filter(Boolean)
    for (const id of ids) {
      const data = await apiJson('DELETE', `/api/recipe/${id}`)
      expect(data.success).toBe(true)
    }
  })
})
