import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import { AuthProvider } from './hooks/useAuth'
import { ThemeProvider, useTheme } from './hooks/useTheme'
import { Toaster } from 'sonner'
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
          <App />
          <Toaster position="top-right" richColors closeButton />
        </AltaraThemeWrapper>
      </ThemeProvider>
    </AuthProvider>
  </StrictMode>,
)
