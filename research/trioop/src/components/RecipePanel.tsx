import { useState, useEffect, useCallback } from 'react'

interface Recipe { name: string; description?: string; values: Record<string, number>; createdAt: number; updatedAt: number }

export default function RecipePanel({ liveData }: { liveData?: Record<string, { value: number | boolean }> }) {
  const [recipes, setRecipes] = useState<Recipe[]>([])
  const [showNew, setShowNew] = useState(false)
  const [name, setName] = useState('')
  const [desc, setDesc] = useState('')
  const [applyResult, setApplyResult] = useState<string>('')

  const load = useCallback(async () => {
    try { setRecipes(await (await fetch('/api/recipe')).json()) } catch {}
  }, [])

  useEffect(() => { load() }, [load])

  const handleCreate = async () => {
    if (!name.trim()) return
    await fetch('/api/recipe', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: name.trim(), description: desc.trim(), values: {} }),
    })
    setShowNew(false); setName(''); setDesc(''); load()
  }

  const handleSnapshot = async (recipeName: string) => {
    const res = await fetch(`/api/recipe/${encodeURIComponent(recipeName)}/snapshot`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: recipeName }),
    })
    const data = await res.json()
    if (data.success) load()
  }

  const handleApply = async (recipeName: string) => {
    setApplyResult('')
    const res = await fetch(`/api/recipe/${encodeURIComponent(recipeName)}/apply`, { method: 'POST' })
    const data = await res.json()
    if (data.results) {
      const ok = data.results.filter((r: any) => r.success).length
      const fail = data.results.filter((r: any) => !r.success).length
      setApplyResult(`✅ ${ok} 成功${fail ? `, ❌ ${fail} 失败` : ''}`)
      setTimeout(() => setApplyResult(''), 3000)
    }
  }

  const handleDelete = async (recipeName: string) => {
    await fetch(`/api/recipe/${encodeURIComponent(recipeName)}`, { method: 'DELETE' })
    load()
  }

  const handleUpdateValues = async (recipeName: string) => {
    if (!liveData) return
    const values: Record<string, number> = {}
    for (const [n, pt] of Object.entries(liveData)) {
      if (pt.value !== undefined && pt.value !== null) {
        values[n] = typeof pt.value === 'number' ? pt.value : (pt.value ? 1 : 0)
      }
    }
    await fetch(`/api/recipe/${encodeURIComponent(recipeName)}`, {
      method: 'PUT', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ values }),
    })
    load()
  }

  return (
    <section className="section">
      <h2 className="section__title">📋 配方管理</h2>

      {applyResult && <div className="recipe-result">{applyResult}</div>}

      <div className="dashboard-bar">
        <button className="btn btn--sm btn--primary" onClick={() => setShowNew(!showNew)}>
          {showNew ? '取消' : '+ 新建配方'}
        </button>
      </div>

      {showNew && (
        <div className="dashboard-form">
          <input placeholder="配方名称" value={name} onChange={e => setName(e.target.value)} />
          <input placeholder="描述（可选）" value={desc} onChange={e => setDesc(e.target.value)} />
          <button className="btn btn--primary btn--sm" onClick={handleCreate}>创建</button>
        </div>
      )}

      {recipes.length === 0 ? (
        <div className="db-empty">暂无配方</div>
      ) : (
        <div className="recipe-list">
          {recipes.map(r => (
            <div key={r.name} className="recipe-card">
              <div className="recipe-card__header">
                <span className="recipe-card__name">{r.name}</span>
                {r.description && <span className="recipe-card__desc">{r.description}</span>}
                <span className="recipe-card__count">{Object.keys(r.values).length} 个变量</span>
              </div>
              <div className="recipe-card__actions">
                <button className="btn btn--success btn--sm" onClick={() => handleApply(r.name)}>▶ 下发</button>
                <button className="btn btn--primary btn--sm" onClick={() => handleUpdateValues(r.name)}>📸 拍照</button>
                <button className="btn btn--primary btn--sm" onClick={() => handleSnapshot(r.name)}>🔄 刷新</button>
                <button className="btn btn--danger btn--sm" onClick={() => handleDelete(r.name)}>✕</button>
              </div>
            </div>
          ))}
        </div>
      )}
    </section>
  )
}
