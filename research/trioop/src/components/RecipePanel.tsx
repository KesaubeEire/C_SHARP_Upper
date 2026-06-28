import { useState, useEffect, useCallback } from 'react'
import CollapsibleSection from './CollapsibleSection'
import type { RecipeMeta, RecipeRecord, RecipeGroup, RecipeParameter, RecipeVersionSnapshot } from '../../shared/types'
import { RecipeStatus } from '../../shared/types'

const STATUS_NAMES = ['草稿', '使用中', '已归档']

const DEFAULT_GROUP: RecipeGroup = {
  name: '参数组1', description: '', parameters: [], parameterCount: 0,
}

function emptyParam(idx: number): RecipeParameter {
  return {
    name: `参数${idx + 1}`, value: 0, unit: '', address: idx * 2,
    scale: 1.0, offset: 0, minValue: -Infinity, maxValue: Infinity,
    group: '', plcDataType: 'REAL', dbNumber: 0,
  }
}

export default function RecipePanel() {
  // ─── 配方列表 ──────────────────────────────────────────────
  const [recipes, setRecipes] = useState<RecipeMeta[]>([])
  const [filteredRecipes, setFilteredRecipes] = useState<RecipeMeta[]>([])
  const [selectedMeta, setSelectedMeta] = useState<RecipeMeta | null>(null)
  const [searchText, setSearchText] = useState('')
  const [categoryFilter, setCategoryFilter] = useState('全部')

  // ─── 当前配方 ──────────────────────────────────────────────
  const [currentId, setCurrentId] = useState<string | null>(null)
  const [currentName, setCurrentName] = useState('')
  const [currentDesc, setCurrentDesc] = useState('')
  const [currentProductCode, setCurrentProductCode] = useState('')
  const [currentAuthor, setCurrentAuthor] = useState('')
  const [currentStatus, setCurrentStatus] = useState<RecipeStatus>(RecipeStatus.Draft)
  const [currentCategory, setCurrentCategory] = useState('')
  const [currentTags, setCurrentTags] = useState('')
  const [currentVersion, setCurrentVersion] = useState(0)
  const [defaultDbNumber, setDefaultDbNumber] = useState(1)
  const [hasRecipe, setHasRecipe] = useState(false)

  // ─── 参数组 ────────────────────────────────────────────────
  const [groups, setGroups] = useState<RecipeGroup[]>([{ ...DEFAULT_GROUP, parameters: [] }])
  const [selectedGroupIdx, setSelectedGroupIdx] = useState(0)
  const [paramSearch, setParamSearch] = useState('')

  // ─── 版本历史 ──────────────────────────────────────────────
  const [versionHistory, setVersionHistory] = useState<RecipeVersionSnapshot[]>([])
  const [showVersions, setShowVersions] = useState(false)

  // ─── 状态 ──────────────────────────────────────────────────
  const [statusText, setStatusText] = useState('就绪')
  const [isPlcConnected, setIsPlcConnected] = useState(false)

  // ─── 数据加载 ──────────────────────────────────────────────
  const loadRecipes = useCallback(async () => {
    try {
      const res = await fetch('/api/recipe')
      const data: RecipeMeta[] = await res.json()
      setRecipes(data)
    } catch {}
  }, [])

  useEffect(() => { loadRecipes() }, [loadRecipes])

  // 搜索/过滤
  useEffect(() => {
    let filtered = recipes
    if (searchText.trim()) {
      const s = searchText.toLowerCase()
      filtered = filtered.filter(r =>
        r.name.toLowerCase().includes(s) ||
        r.description.toLowerCase().includes(s) ||
        r.productCode.toLowerCase().includes(s) ||
        r.tags.some(t => t.toLowerCase().includes(s))
      )
    }
    if (categoryFilter !== '全部') {
      filtered = filtered.filter(r => r.category === categoryFilter)
    }
    setFilteredRecipes(filtered)
  }, [recipes, searchText, categoryFilter])

  const categories = ['全部', ...new Set(recipes.map(r => r.category).filter(Boolean))]

  // ─── 配方操作 ──────────────────────────────────────────────
  const loadRecipe = async (meta: RecipeMeta) => {
    setSelectedMeta(meta)
    try {
      const res = await fetch(`/api/recipe/${meta.id}`)
      const recipe: RecipeRecord = await res.json()
      setCurrentId(recipe.id)
      setCurrentName(recipe.name)
      setCurrentDesc(recipe.description)
      setCurrentProductCode(recipe.productCode)
      setCurrentAuthor(recipe.author)
      setCurrentStatus(recipe.status)
      setCurrentCategory(recipe.category)
      setCurrentTags(recipe.tags.join(', '))
      setCurrentVersion(recipe.version)
      setDefaultDbNumber(recipe.defaultDbNumber)
      setGroups(recipe.groups.length > 0 ? recipe.groups : [{ ...DEFAULT_GROUP, parameters: [] }])
      setSelectedGroupIdx(0)
      setHasRecipe(true)
      setStatusText(`已加载「${recipe.name}」`)
    } catch { setStatusText('加载失败') }
  }

  const handleNew = () => {
    setSelectedMeta(null)
    setCurrentId(null)
    setCurrentName(`新配方 ${new Date().toLocaleDateString('zh-CN')}`)
    setCurrentDesc('')
    setCurrentProductCode('')
    setCurrentAuthor('')
    setCurrentStatus(RecipeStatus.Draft)
    setCurrentCategory('')
    setCurrentTags('')
    setCurrentVersion(0)
    setDefaultDbNumber(1)
    setGroups([{ ...DEFAULT_GROUP, parameters: [] }])
    setSelectedGroupIdx(0)
    setHasRecipe(true)
    setStatusText('新建配方')
  }

  const handleSave = async () => {
    const body = {
      id: currentId,
      name: currentName,
      description: currentDesc,
      productCode: currentProductCode,
      author: currentAuthor,
      status: currentStatus,
      category: currentCategory,
      tags: currentTags.split(',').map(t => t.trim()).filter(Boolean),
      defaultDbNumber,
      groups: groups.map(g => ({
        name: g.name,
        description: g.description,
        parameters: g.parameters.map(p => ({
          name: p.name, value: p.value, unit: p.unit, address: p.address,
          scale: p.scale, offset: p.offset, minValue: p.minValue, maxValue: p.maxValue,
          group: p.group, plcDataType: p.plcDataType, dbNumber: p.dbNumber,
        })),
      })),
    }

    try {
      if (currentId) {
        const res = await fetch(`/api/recipe/${currentId}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
        const data = await res.json()
        if (data.recipe) {
          setCurrentVersion(data.recipe.version)
          setStatusText(`配方已保存 (v${data.recipe.version})`)
        }
      } else {
        const res = await fetch('/api/recipe', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
        const data = await res.json()
        if (data.recipe) {
          setCurrentId(data.recipe.id)
          setCurrentVersion(data.recipe.version)
          setStatusText(`配方已保存 (v${data.recipe.version})`)
        }
      }
      loadRecipes()
    } catch {}
  }

  const handleDelete = async (meta: RecipeMeta) => {
    if (!confirm(`删除配方「${meta.name}」？`)) return
    try {
      await fetch(`/api/recipe/${meta.id}`, { method: 'DELETE' })
      if (currentId === meta.id) clearCurrent()
      setStatusText(`已删除「${meta.name}」`)
      loadRecipes()
    } catch {}
  }

  const handleCopy = async (meta: RecipeMeta) => {
    try {
      const res = await fetch(`/api/recipe/${meta.id}/copy`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name: `${meta.name} (副本)` }) })
      const data = await res.json()
      if (data.recipe) { setStatusText(`已复制为「${data.recipe.name}」`); loadRecipes() }
    } catch {}
  }

  const clearCurrent = () => {
    setSelectedMeta(null); setCurrentId(null); setCurrentName(''); setCurrentDesc('')
    setCurrentProductCode(''); setCurrentAuthor(''); setCurrentStatus(RecipeStatus.Draft)
    setCurrentCategory(''); setCurrentTags(''); setCurrentVersion(0); setDefaultDbNumber(1)
    setGroups([{ ...DEFAULT_GROUP, parameters: [] }]); setSelectedGroupIdx(0); setHasRecipe(false)
    setShowVersions(false)
  }

  // ─── 参数组操作 ────────────────────────────────────────────
  const currentGroup = groups[selectedGroupIdx]
  const filteredParams = currentGroup?.parameters.filter(p => {
    if (!paramSearch.trim()) return true
    const s = paramSearch.toLowerCase()
    return p.name.toLowerCase().includes(s) || p.group.toLowerCase().includes(s)
  }) ?? []

  const handleAddGroup = () => {
    const ng: RecipeGroup = { name: `组${groups.length + 1}`, description: '', parameters: [], parameterCount: 0 }
    setGroups([...groups, ng])
    setSelectedGroupIdx(groups.length)
  }

  const handleRemoveGroup = () => {
    if (groups.length <= 1) return
    const newGroups = groups.filter((_, i) => i !== selectedGroupIdx)
    setGroups(newGroups)
    setSelectedGroupIdx(Math.min(selectedGroupIdx, newGroups.length - 1))
  }

  const handleAddParam = () => {
    if (!currentGroup) return
    const idx = currentGroup.parameters.length
    const updated = [...groups]
    updated[selectedGroupIdx] = {
      ...currentGroup,
      parameters: [...currentGroup.parameters, emptyParam(idx)],
      parameterCount: idx + 1,
    }
    setGroups(updated)
  }

  const handleRemoveParam = () => {
    if (!currentGroup || currentGroup.parameters.length === 0) return
    const updated = [...groups]
    updated[selectedGroupIdx] = {
      ...currentGroup,
      parameters: currentGroup.parameters.slice(0, -1),
      parameterCount: currentGroup.parameters.length - 1,
    }
    setGroups(updated)
  }

  const handleUpdateParam = (paramIdx: number, field: keyof RecipeParameter, value: any) => {
    const updated = [...groups]
    const params = [...updated[selectedGroupIdx].parameters]
    params[paramIdx] = { ...params[paramIdx], [field]: field === 'value' || field === 'address' || field === 'scale' || field === 'offset' || field === 'dbNumber' || field === 'minValue' || field === 'maxValue' ? Number(value) : value }
    updated[selectedGroupIdx] = { ...updated[selectedGroupIdx], parameters: params }
    setGroups(updated)
  }

  // ─── 版本历史 ──────────────────────────────────────────────
  const handleToggleVersions = async () => {
    if (!currentId) return
    if (showVersions) { setShowVersions(false); return }
    try {
      const res = await fetch(`/api/recipe/${currentId}/versions`)
      setVersionHistory(await res.json())
      setShowVersions(true)
    } catch {}
  }

  const handleRestoreVersion = async (snap: RecipeVersionSnapshot) => {
    try {
      const res = await fetch(`/api/recipe/${currentId}/restore/${snap.version}`, { method: 'POST' })
      const data = await res.json()
      if (data.recipe) {
        setStatusText(`已恢复至 v${snap.version}`)
        setShowVersions(false)
        if (currentId) loadRecipe({ id: currentId, name: data.recipe.name, parameterCount: 0, ...data.recipe } as RecipeMeta)
      }
    } catch {}
  }

  // ─── CSV ───────────────────────────────────────────────────
  const handleExportCsv = async () => {
    if (!currentId) return
    try {
      const res = await fetch(`/api/recipe/${currentId}/export-csv`)
      const blob = await res.blob()
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a'); a.href = url; a.download = `${currentName}.csv`
      a.click(); URL.revokeObjectURL(url)
      setStatusText('已导出 CSV')
    } catch {}
  }

  const handleImportCsv = () => {
    const input = document.createElement('input'); input.type = 'file'; input.accept = '.csv'
    input.onchange = async () => {
      const file = input.files?.[0]
      if (!file) return
      try {
        const text = await file.text()
        const res = await fetch(`/api/recipe/${currentId}/import-csv`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ csv: text }) })
        const data = await res.json()
        if (data.parameters && data.parameters.length > 0) {
          const updated = [...groups]
          updated[selectedGroupIdx] = {
            ...currentGroup,
            parameters: [...currentGroup.parameters, ...data.parameters],
            parameterCount: currentGroup.parameters.length + data.parameters.length,
          }
          setGroups(updated)
          setStatusText(`已导入 ${data.parameters.length} 个参数`)
        } else {
          setStatusText('CSV 导入失败：未找到有效参数')
        }
      } catch {}
    }
    input.click()
  }

  // ─── PLC ───────────────────────────────────────────────────
  const refreshPlcStatus = useCallback(async () => {
    try {
      const res = await fetch('/api/plc/status')
      const data = await res.json()
      setIsPlcConnected(data.connected)
    } catch {}
  }, [])

  useEffect(() => { refreshPlcStatus(); const t = setInterval(refreshPlcStatus, 5000); return () => clearInterval(t) }, [refreshPlcStatus])

  const handleApply = async () => {
    if (!currentId) return
    setStatusText('正在下载到 PLC...')
    try {
      const res = await fetch(`/api/recipe/${currentId}/apply`, { method: 'POST' })
      const data = await res.json()
      const ok = data.results?.filter((r: any) => r.success).length ?? 0
      const fail = data.results?.filter((r: any) => !r.success).length ?? 0
      setStatusText(`已下载 ${ok} 个参数${fail ? `，${fail} 个失败` : ''}`)
    } catch {}
  }

  // ─── 渲染 ──────────────────────────────────────────────────
  return (
    <CollapsibleSection title="📋 配方管理" storageKey="recipe-manager">
      <div className="recipe-layout" style={{ display: 'grid', gridTemplateColumns: '300px 1fr', gap: 12 }}>
        {/* ─── 左栏 ──────────────────────────────────────── */}
        <div className="recipe-sidebar">
          <div className="recipe-sidebar__header">
            <div className="recipe-sidebar__accent" />
            <span className="recipe-sidebar__title">配方列表</span>
          </div>

          <div className="recipe-sidebar__search">
            <input className="alarm-filterbar__input" style={{ width: '100%' }} placeholder="搜索配方..." value={searchText} onChange={e => setSearchText(e.target.value)} />
          </div>

          <div className="recipe-sidebar__filter">
            <select className="alarm-filterbar__select" style={{ width: '100%' }} value={categoryFilter} onChange={e => setCategoryFilter(e.target.value)}>
              {categories.map(c => <option key={c} value={c}>{c}</option>)}
            </select>
          </div>

          <div className="recipe-sidebar__new">
            <button className="btn btn--sm btn--primary" style={{ width: '100%' }} onClick={handleNew}>+ 新建配方</button>
          </div>

          <div className="recipe-list">
            {filteredRecipes.length === 0 ? (
              <div className="recipe-empty">暂无配方</div>
            ) : filteredRecipes.map(r => (
              <div key={r.id}
                className={`recipe-list-item ${selectedMeta?.id === r.id ? 'recipe-list-item--selected' : ''}`}
                onClick={() => loadRecipe(r)}>
                <div className="recipe-list-item__top">
                  <span className="recipe-list-item__name">{r.name}</span>
                  {r.category && <span className="recipe-list-item__cat">{r.category}</span>}
                  <span className="recipe-list-item__version">v{r.version}</span>
                </div>
                <div className="recipe-list-item__meta">
                  {r.productCode && <span>{r.productCode}</span>}
                  {r.author && <span>{r.author}</span>}
                  {r.parameterCount > 0 && <span>{r.parameterCount} 参数</span>}
                  <span className={`status-pill ${r.status === RecipeStatus.Active ? 'status-pill--acknowledged' : r.status === RecipeStatus.Archived ? 'status-pill--shelved' : 'status-pill--recovered'}`} style={{ fontSize: 10 }}>
                    {STATUS_NAMES[r.status]}
                  </span>
                </div>
                <div className="recipe-list-item__time">{new Date(r.modifiedAt).toLocaleString()}</div>
              </div>
            ))}
          </div>

          <div className="recipe-sidebar__footer">
            <button className="btn btn--sm btn--secondary" disabled={!selectedMeta} onClick={() => selectedMeta && handleCopy(selectedMeta)} title="复制配方">📋 复制</button>
            <button className="btn btn--sm btn--secondary" onClick={loadRecipes} title="刷新">🔄</button>
            <span style={{ flex: 1, fontSize: 11, color: 'var(--muted-foreground)' }}>{filteredRecipes.length} 个配方</span>
          </div>

          {/* 版本历史 */}
          <div className="recipe-versions">
            <button className="recipe-versions__toggle" onClick={handleToggleVersions} disabled={!currentId}>
              <span>📜</span>
              <span>版本历史</span>
              {showVersions && <span style={{ marginLeft: 'auto', fontSize: 11 }}>▲</span>}
            </button>
            {showVersions && (
              <div className="recipe-versions__list">
                {versionHistory.length === 0 ? (
                  <div style={{ fontSize: 12, color: 'var(--muted-foreground)', padding: '8px 0' }}>暂无版本</div>
                ) : (
                  <>
                    {versionHistory.map(v => (
                      <div key={v.version} className="recipe-versions__item">
                        <span className="recipe-versions__v">v{v.version}</span>
                        <span className="recipe-versions__time">{new Date(v.snapshotAt).toLocaleString()}</span>
                        <button className="btn btn--sm btn--primary" style={{ marginLeft: 'auto', height: 20, fontSize: 10 }} onClick={() => handleRestoreVersion(v)}>恢复</button>
                      </div>
                    ))}
                  </>
                )}
              </div>
            )}
          </div>
        </div>

        {/* ─── 右栏 ──────────────────────────────────────── */}
        <div className={`recipe-editor ${!hasRecipe ? 'recipe-editor--disabled' : ''}`}>
          {/* Header */}
          <div className="recipe-editor__header">
            <span className="recipe-editor__title">{hasRecipe ? currentName : '配方编辑器'}</span>
            {currentVersion > 0 && <span className="recipe-editor__version">v{currentVersion}</span>}
            <button className="btn btn--sm btn--danger" disabled={!selectedMeta} onClick={() => selectedMeta && handleDelete(selectedMeta)}>🗑</button>
          </div>

          {/* 元数据 */}
          <div className="recipe-editor__meta">
            <div className="recipe-editor__field">
              <span className="recipe-editor__label">配方名称</span>
              <input className="recipe-editor__input" value={currentName} onChange={e => setCurrentName(e.target.value)} placeholder="输入配方名称" />
            </div>
            <div />
            <div className="recipe-editor__field">
              <span className="recipe-editor__label">产品代码</span>
              <input className="recipe-editor__input" value={currentProductCode} onChange={e => setCurrentProductCode(e.target.value)} placeholder="PC-001" />
            </div>

            <div className="recipe-editor__meta-row2" style={{ gridColumn: '1 / -1', display: 'grid', gridTemplateColumns: '1fr 12px 140px 12px 100px', gap: 0, marginTop: 8 }}>
              <div className="recipe-editor__field">
                <span className="recipe-editor__label">操作人</span>
                <input className="recipe-editor__input" value={currentAuthor} onChange={e => setCurrentAuthor(e.target.value)} placeholder="操作人/工程师" />
              </div>
              <div />
              <div className="recipe-editor__field">
                <span className="recipe-editor__label">配方状态</span>
                <select className="recipe-editor__select" value={currentStatus} onChange={e => setCurrentStatus(Number(e.target.value))}>
                  {STATUS_NAMES.map((n, i) => <option key={n} value={i}>{n}</option>)}
                </select>
              </div>
              <div />
              <div className="recipe-editor__field">
                <span className="recipe-editor__label">默认 DB</span>
                <input className="recipe-editor__input" type="number" min="1" value={defaultDbNumber} onChange={e => setDefaultDbNumber(Number(e.target.value))} />
              </div>
            </div>

            <div className="recipe-editor__meta-row3" style={{ gridColumn: '1 / -1', display: 'grid', gridTemplateColumns: '2fr 12px 1fr', gap: 0, marginTop: 8 }}>
              <div className="recipe-editor__field">
                <span className="recipe-editor__label">描述</span>
                <input className="recipe-editor__input" value={currentDesc} onChange={e => setCurrentDesc(e.target.value)} placeholder="配方说明（可选）" />
              </div>
              <div />
              <div className="recipe-editor__field">
                <span className="recipe-editor__label">分类 / 标签（逗号分隔）</span>
                <input className="recipe-editor__input" value={currentTags} onChange={e => setCurrentTags(e.target.value)} placeholder="温度, 加热, 标准" />
              </div>
            </div>
          </div>

          {/* 参数组 Tab 栏 */}
          <div className="recipe-groups-bar">
            <div className="recipe-groups-bar__tabs">
              {groups.map((g, i) => (
                <button key={i} className={`recipe-group-tab ${i === selectedGroupIdx ? 'recipe-group-tab--active' : ''}`}
                  onClick={() => setSelectedGroupIdx(i)}>
                  {g.name}
                  <span className="recipe-group-tab__count">{g.parameters.length}</span>
                </button>
              ))}
            </div>
            <button className="btn btn--sm btn--secondary" onClick={handleAddGroup} title="添加参数组">+</button>
            <button className="btn btn--sm btn--secondary" onClick={handleRemoveGroup} title="删除参数组" disabled={groups.length <= 1}>−</button>
          </div>

          {/* 参数工具栏 */}
          <div className="recipe-param-toolbar">
            <div className="recipe-param-toolbar__search">
              <input className="alarm-filterbar__input" style={{ width: '100%' }} placeholder="搜索参数..." value={paramSearch} onChange={e => setParamSearch(e.target.value)} />
            </div>
            <button className="btn btn--sm btn--success" onClick={handleApply} title="下载到 PLC" disabled={!currentId}>⬇ PLC</button>
            <button className="btn btn--sm btn--primary" onClick={() => {}} title="从 PLC 上传">⬆ PLC</button>
            <button className="btn btn--sm btn--secondary" onClick={handleAddParam} title="添加参数">+ 参数</button>
            <button className="btn btn--sm btn--secondary" onClick={handleRemoveParam} title="删除参数">− 参数</button>
            <button className="btn btn--sm btn--secondary" onClick={handleImportCsv} title="从 CSV 导入">📂 CSV</button>
            <button className="btn btn--sm btn--secondary" onClick={handleExportCsv} title="导出 CSV">💾 CSV</button>
          </div>

          {/* 参数表 */}
          <div className="recipe-param-table-wrap">
            <table className="recipe-param-table">
              <thead>
                <tr>
                  <th style={{ width: 40 }}>#</th>
                  <th style={{ width: 120 }}>参数名</th>
                  <th style={{ width: '100%' }}>值</th>
                  <th style={{ width: 60 }}>单位</th>
                  <th style={{ width: 70 }}>地址</th>
                  <th style={{ width: 70 }}>缩放</th>
                  <th style={{ width: 70 }}>偏移</th>
                  <th style={{ width: 90 }}>数据类型</th>
                  <th style={{ width: 50 }}>DB</th>
                </tr>
              </thead>
              <tbody>
                {filteredParams.map((p, i) => {
                  // 在 groups 中找到这个参数的原始索引
                  const actualIdx = currentGroup.parameters.indexOf(p)
                  const rowNum = actualIdx >= 0 ? actualIdx : i
                  return (
                    <tr key={rowNum}>
                      <td className="recipe-param-table__num">{rowNum + 1}</td>
                      <td><input className="recipe-param-table__input" value={p.name} onChange={e => handleUpdateParam(actualIdx, 'name', e.target.value)} /></td>
                      <td><input className="recipe-param-table__input" type="number" step="any" value={p.value} onChange={e => handleUpdateParam(actualIdx, 'value', e.target.value)} /></td>
                      <td><input className="recipe-param-table__input" value={p.unit} onChange={e => handleUpdateParam(actualIdx, 'unit', e.target.value)} /></td>
                      <td><input className="recipe-param-table__input" type="number" value={p.address} onChange={e => handleUpdateParam(actualIdx, 'address', e.target.value)} /></td>
                      <td><input className="recipe-param-table__input" type="number" step="any" value={p.scale} onChange={e => handleUpdateParam(actualIdx, 'scale', e.target.value)} /></td>
                      <td><input className="recipe-param-table__input" type="number" step="any" value={p.offset} onChange={e => handleUpdateParam(actualIdx, 'offset', e.target.value)} /></td>
                      <td>
                        <select className="recipe-editor__select" style={{ width: '100%', height: 26, fontSize: 11 }} value={p.plcDataType} onChange={e => handleUpdateParam(actualIdx, 'plcDataType', e.target.value)}>
                          {['REAL', 'INT', 'DINT', 'UINT', 'UDINT', 'WORD', 'DWORD', 'BYTE', 'USINT', 'SINT', 'BOOL'].map(t => <option key={t} value={t}>{t}</option>)}
                        </select>
                      </td>
                      <td><input className="recipe-param-table__input" type="number" value={p.dbNumber} onChange={e => handleUpdateParam(actualIdx, 'dbNumber', e.target.value)} /></td>
                    </tr>
                  )
                })}
                {(!currentGroup || currentGroup.parameters.length === 0) && (
                  <tr><td colSpan={9} style={{ textAlign: 'center', padding: 24, color: 'var(--muted-foreground)' }}>暂无参数，点击「+ 参数」添加</td></tr>
                )}
              </tbody>
            </table>
          </div>

          {/* Footer */}
          <div className="recipe-editor__footer">
            <div className="status-dot" style={{ background: isPlcConnected ? 'var(--vt-color-active)' : 'var(--vt-color-neutral)' }} />
            <span style={{ fontSize: 11, color: 'var(--muted-foreground)' }}>{isPlcConnected ? '已连接' : '未连接'}</span>
            <span className="recipe-editor__status">{statusText}</span>
            <button className="btn btn--sm btn--success" onClick={handleSave}>
              💾 保存配方
            </button>
          </div>
        </div>
      </div>
    </CollapsibleSection>
  )
}
