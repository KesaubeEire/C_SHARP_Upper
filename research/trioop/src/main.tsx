import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import { AuthProvider } from './hooks/useAuth'
import { ToastProvider } from './hooks/useToast'
import { ThemeProvider } from './hooks/useTheme'
import { AltaraProvider } from '@altara/core'
import './App.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AuthProvider>
      <ThemeProvider>
        <AltaraProvider>
          <ToastProvider>
          <App />
        </ToastProvider>
        </AltaraProvider>
      </ThemeProvider>
    </AuthProvider>
  </StrictMode>,
)
