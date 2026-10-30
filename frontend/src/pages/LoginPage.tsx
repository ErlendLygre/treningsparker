import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api } from '../api'
import { DEMO_ADMIN } from '../api/demoApi'
import { useAuth } from '../auth'

export default function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await login(email, password)
      navigate('/')
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="page narrow">
      <form className="card form" onSubmit={submit}>
        <h1>Logg inn</h1>
        <label>
          E-post
          <input type="email" required autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} />
        </label>
        <label>
          Passord
          <input type="password" required autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} />
        </label>
        {error && <p className="error">{error}</p>}
        <button className="btn btn-primary" disabled={busy}>{busy ? 'Logger inn…' : 'Logg inn'}</button>
        <p className="small muted">
          Ny bruker? <Link to="/registrer">Opprett konto</Link>
        </p>
        {api.isDemo && (
          <div className="notice small">
            <strong>Demo:</strong> logg inn som admin med <code>{DEMO_ADMIN.email}</code> / <code>{DEMO_ADMIN.password}</code>{' '}
            <button
              type="button"
              className="link"
              onClick={() => {
                setEmail(DEMO_ADMIN.email)
                setPassword(DEMO_ADMIN.password)
              }}
            >
              Fyll inn
            </button>
          </div>
        )}
      </form>
    </div>
  )
}
