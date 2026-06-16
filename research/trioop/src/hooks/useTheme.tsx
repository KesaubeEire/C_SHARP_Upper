import { useState, useEffect, createContext, useContext, useCallback } from 'react'

type Theme = 'dark' | 'light'
const KEY = 'trioop_theme'

const ThemeCtx = createContext<{ theme: Theme; toggle: () => void }>({ theme: 'dark', toggle: () => {} })

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const [theme, setTheme] = useState<Theme>(() => {
    try { return (localStorage.getItem(KEY) as Theme) || 'dark' } catch { return 'dark' }
  })

  useEffect(() => {
    localStorage.setItem(KEY, theme)
    document.documentElement.setAttribute('data-theme', theme)
  }, [theme])

  const toggle = useCallback(() => {
    setTheme(t => t === 'dark' ? 'light' : 'dark')
  }, [])

  return <ThemeCtx.Provider value={{ theme, toggle }}>{children}</ThemeCtx.Provider>
}

export const useTheme = () => useContext(ThemeCtx)
