import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import { AuthProvider } from './auth/AuthContext.tsx'
import { ThemeProvider } from './components/theme/theme-provider.tsx'
import router from './router/index.tsx'
import { RouterProvider } from "react-router/dom";

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </ThemeProvider>
  </StrictMode>,
)
