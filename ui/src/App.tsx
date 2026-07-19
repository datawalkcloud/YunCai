import { useState } from 'react'
import reactLogo from './assets/react.svg'
import viteLogo from './assets/vite.svg'
import heroImg from './assets/hero.png'
import { useAuth } from './auth/AuthContext'
import Login from './pages/login/Login'

function App() {
  const { isAuthenticated, user, logout } = useAuth()
  const [count, setCount] = useState(0)

  return <Login />
}

export default App
