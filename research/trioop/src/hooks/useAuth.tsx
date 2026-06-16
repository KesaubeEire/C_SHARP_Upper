import { useState, useEffect, createContext, useContext, useCallback } from 'react'

interface AuthState {
  token: string | null
  username: string | null
  role: string | null
  login: (user: string, pass: string) => Promise<string | null>
  logout: () => void
}

const AuthCtx = createContext<AuthState>({
  token: null, username: null, role: null,
  login: async () => null,
  logout: () => {},
})

const TOKEN_KEY = 'trioop_token'

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [token, setToken] = useState<string | null>(() => localStorage.getItem(TOKEN_KEY))
  const [username, setUsername] = useState<string | null>(null)
  const [role, setRole] = useState<string | null>(null)
  const [ready, setReady] = useState(false)

  // 启动时验证已有 token
  useEffect(() => {
    if (!token) { setReady(true); return }
    fetch('/api/auth/me', { headers: { Authorization: `Bearer ${token}` } })
      .then(r => r.json())
      .then(d => {
        if (d.username) { setUsername(d.username); setRole(d.role) }
        else { localStorage.removeItem(TOKEN_KEY); setToken(null) }
        setReady(true)
      })
      .catch(() => { localStorage.removeItem(TOKEN_KEY); setToken(null); setReady(true) })
  }, [])

  const login = useCallback(async (user: string, pass: string): Promise<string | null> => {
    try {
      const res = await fetch('/api/auth/login', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username: user, password: pass }),
      })
      const data = await res.json()
      if (!data.success) return data.error || '登录失败'
      setToken(data.token)
      setUsername(data.username)
      setRole(data.role)
      localStorage.setItem(TOKEN_KEY, data.token)
      return null
    } catch { return '网络错误' }
  }, [])

  const logout = useCallback(() => {
    if (token) fetch('/api/auth/logout', { method: 'POST', headers: { Authorization: `Bearer ${token}` } }).catch(() => {})
    setToken(null); setUsername(null); setRole(null)
    localStorage.removeItem(TOKEN_KEY)
  }, [token])

  if (!ready) return (
    <div className="login-overlay">
      <div className="login-form" style={{ alignItems: 'center' }}>
        <h1 className="login-title">🔌 Trioop HMI</h1>
        <div style={{ color: '#666', fontSize: 14 }}>加载中...</div>
      </div>
    </div>
  )

  return (
    <AuthCtx.Provider value={{ token, username, role, login, logout }}>
      {token ? children : <LoginScreen onLogin={login} />}
    </AuthCtx.Provider>
  )
}

function LoginScreen({ onLogin }: { onLogin: (u: string, p: string) => Promise<string | null> }) {
  const [user, setUser] = useState('')
  const [pass, setPass] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!user || !pass) return
    setBusy(true); setError('')
    const err = await onLogin(user, pass)
    if (err) setError(err)
    setBusy(false)
  }

  return (
    <div className="login-overlay">
      <form className="login-form" onSubmit={handleSubmit}>
        <h1 className="login-title">🔌 Trioop HMI</h1>
        <input className="login-input" type="text" placeholder="用户名" value={user} onChange={e => setUser(e.target.value)} autoFocus />
        <input className="login-input" type="password" placeholder="密码" value={pass} onChange={e => setPass(e.target.value)} />
        {error && <div className="login-error">{error}</div>}
        <button className="btn btn--primary login-btn" type="submit" disabled={busy}>{busy ? '登录中...' : '登录'}</button>
        <div className="login-hint">默认: admin/admin 或 operator/1234</div>
      </form>
    </div>
  )
}

export const useAuth = () => useContext(AuthCtx)
