import { useActionState } from 'react'
import { Link, useNavigate } from 'react-router'
import { useAuth } from '../auth'

interface LoginState {
  error: string | null
  email: string
}

export default function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()

  const [state, formAction, pending] = useActionState(
    async (_previous: LoginState, form: FormData): Promise<LoginState> => {
      const email = String(form.get('email') ?? '')
      try {
        await login(email, String(form.get('password') ?? ''))
        navigate('/')
        return { error: null, email: '' }
      } catch (err) {
        // React resets the form after the action; returning the email puts it back as the default value.
        return { error: err instanceof Error ? err.message : String(err), email }
      }
    },
    { error: null, email: '' },
  )

  return (
    <div className="page narrow">
      <form className="card form" action={formAction}>
        <h1>Logg inn</h1>
        <label>
          E-post
          <input name="email" type="email" required autoComplete="email" defaultValue={state.email} />
        </label>
        <label>
          Passord
          <input name="password" type="password" required autoComplete="current-password" />
        </label>
        {state.error && <p className="error">{state.error}</p>}
        <button className="btn btn-primary" disabled={pending}>{pending ? 'Logger inn…' : 'Logg inn'}</button>
        <p className="small muted">
          Ny bruker? <Link to="/registrer">Opprett konto</Link>
        </p>
      </form>
    </div>
  )
}
