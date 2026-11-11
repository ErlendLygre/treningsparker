import { useActionState } from 'react'
import { Link, useNavigate } from 'react-router'
import { useAuth } from '../auth'

interface RegisterState {
  error: string | null
  displayName: string
  email: string
}

export default function RegisterPage() {
  const { register } = useAuth()
  const navigate = useNavigate()

  const [state, formAction, pending] = useActionState(
    async (_previous: RegisterState, form: FormData): Promise<RegisterState> => {
      const displayName = String(form.get('displayName') ?? '')
      const email = String(form.get('email') ?? '')
      try {
        await register(email, String(form.get('password') ?? ''), displayName)
        navigate('/ny-park')
        return { error: null, displayName: '', email: '' }
      } catch (err) {
        return { error: err instanceof Error ? err.message : String(err), displayName, email }
      }
    },
    { error: null, displayName: '', email: '' },
  )

  return (
    <div className="page narrow">
      <form className="card form" action={formAction}>
        <h1>Opprett konto</h1>
        <label>
          Navn
          <input name="displayName" required maxLength={100} autoComplete="name" defaultValue={state.displayName} />
        </label>
        <label>
          E-post
          <input name="email" type="email" required maxLength={256} autoComplete="email" defaultValue={state.email} />
        </label>
        <label>
          Passord <span className="muted small">(minst 8 tegn)</span>
          <input name="password" type="password" required minLength={8} maxLength={128} autoComplete="new-password" />
        </label>
        {state.error && <p className="error">{state.error}</p>}
        <button className="btn btn-primary" disabled={pending}>{pending ? 'Oppretter…' : 'Opprett konto'}</button>
        <p className="small muted">
          Har du allerede konto? <Link to="/logg-inn">Logg inn</Link>
        </p>
      </form>
    </div>
  )
}
