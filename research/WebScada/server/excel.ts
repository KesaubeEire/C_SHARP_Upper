/**
 * Excel 导入导出工具层
 * 基于 exceljs，封装建簿/解析两大函数，统一全项目使用
 */

import ExcelJS from 'exceljs'

export interface ColumnDef {
  header: string
  key: string
  width?: number
}

/**
 * 按列定义和数据行生成 xlsx 文件 Buffer
 * 表头自动加粗、列宽自适应
 */
export function buildXlsx(
  sheetName: string,
  columns: ColumnDef[],
  rows: Record<string, any>[],
): Buffer {
  const wb = new ExcelJS.Workbook()
  wb.creator = 'WebScada'
  wb.created = new Date()

  const ws = wb.addWorksheet(sheetName)

  // 列定义
  ws.columns = columns.map(c => ({
    header: c.header,
    key: c.key,
    width: c.width ?? Math.max(c.header.length * 2, 12),
  }))

  // 表头样式：加粗
  const headerRow = ws.getRow(1)
  headerRow.font = { bold: true }
  headerRow.height = 20

  // 写入数据行
  rows.forEach(r => ws.addRow(r))

  // 冻结首行
  ws.views = [{ state: 'frozen', ySplit: 1 }]

  return wb.writeBuffer() as unknown as Buffer
}

/**
 * 解析 xlsx Buffer，返回按首行 header 映射的键值对数组
 */
export function parseXlsx(buffer: Buffer): Record<string, string>[] {
  const wb = new ExcelJS.Workbook()
  return wb.xlsx.load(buffer).then(w => {
    const ws = w.worksheets[0]
    if (!ws) return []

    const rows = ws.getRows(1, ws.rowCount) ?? []
    if (rows.length < 2) return []

    const headers = rows[0].values as string[]
    headers.shift() // 去掉 1-indexed 空位

    const result: Record<string, string>[] = []
    for (let i = 1; i < rows.length; i++) {
      const row = rows[i]
      const values = row.values as (string | undefined)[]
      values.shift()
      const obj: Record<string, string> = {}
      headers.forEach((h, idx) => {
        obj[h] = values[idx] !== undefined ? String(values[idx]) : ''
      })
      result.push(obj)
    }
    return result
  }) as unknown as Record<string, string>[]
}
