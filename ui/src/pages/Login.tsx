import { useState, type FormEvent } from 'react'
import { useAuth } from '../auth/AuthContext'
import './Login.css'

export default function Login() {
  const { login } = useAuth()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      await login(username.trim(), password)
    } catch (err) {
      setError(err instanceof Error ? err.message : '登录失败')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="login-page">
      <form className="login-card" onSubmit={handleSubmit}>
        <h1>登录</h1>
        <p className="login-subtitle">欢迎回来，请输入账号信息</p>

        <label className="login-field">
          <span>用户名</span>
          <input
            type="text"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            autoComplete="username"
            placeholder="admin"
            required
          />
        </label>

        <label className="login-field">
          <span>密码</span>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            placeholder="••••••••"
            required
          />
        </label>

        {error && <p className="login-error">{error}</p>}

        <button type="submit" className="login-submit" disabled={loading}>
          {loading ? '登录中…' : '登录'}
        </button>

        <p className="login-hint">演示账号：admin / admin123 或 user / user123</p>
      </form>
    </div>
  )
}
