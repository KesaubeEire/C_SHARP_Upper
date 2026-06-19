import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import { AuthProvider } from './hooks/useAuth'
import { ToastProvider } from './hooks/useToast'
import { ThemeProvider, useTheme } from './hooks/useTheme'
import { AltaraProvider } from './components/altara/core/AltaraProvider'
import './App.css'
import './components/altara/core/tokens/tokens.css'

function AltaraThemeWrapper({ children }: { children: React.ReactNode }) {
  const { theme } = useTheme()
  return <AltaraProvider theme={theme}>{children}</AltaraProvider>
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AuthProvider>
      <ThemeProvider>
        <AltaraThemeWrapper>
          <ToastProvider>
          <App />
        </ToastProvider>
        </AltaraThemeWrapper>
      </ThemeProvider>
    </AuthProvider>
  </StrictMode>,
)
