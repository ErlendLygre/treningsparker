import { NavLink, useNavigate } from 'react-router-dom'
import { api } from '../api'
import { useAuth } from '../auth'

export default function Header() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()

  return (
    <header className="header">
      <NavLink to="/" className="brand">
        <span className="brand-mark" aria-hidden>💪</span>
        <span>
          Treningsparker
          <small>Stavanger &amp; Sandnes</small>
        </span>
      </NavLink>
      {api.isDemo && <span className="demo-badge" title="Data lagres kun i nettleseren din">Demo</span>}
      <nav className="nav">
        <NavLink to="/" end>Kart</NavLink>
        {user && <NavLink to="/ny-park">Foreslå park</NavLink>}
        {user && <NavLink to="/mine-forslag">Mine forslag</NavLink>}
        {user?.isAdmin && <NavLink to="/admin">Admin</NavLink>}
        {user ? (
          <button
            className="btn btn-ghost"
            onClick={() => {
              logout()
              navigate('/')
            }}
          >
            Logg ut ({user.displayName})
          </button>
        ) : (
          <NavLink to="/logg-inn" className="btn btn-primary">Logg inn</NavLink>
        )}
      </nav>
    </header>
  )
}
