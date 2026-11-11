import { createContext, use, useEffect, useEffectEvent, useState, type ReactNode } from 'react'
import { api, setUnauthorizedHandler } from './api'
import type { AuthResponse, User } from './types'

interface AuthState {
  user: User | null
  token: string | null
  login(email: string, password: string): Promise<void>
  register(email: string, password: string, displayName: string): Promise<void>
  logout(): void
}

const STORAGE_KEY = 'treningsparker-auth'
const AuthContext = createContext<AuthState | null>(null)

/** Expiry time (ms) of a JWT, or null if it can't be read. */
function tokenExpiry(token: string): number | null {
  try {
    const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')))
    return typeof payload.exp === 'number' ? payload.exp * 1000 : null
  } catch {
    return null
  }
}

function readStored(): AuthResponse | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    const stored: AuthResponse | null = raw ? JSON.parse(raw) : null
    const expiry = stored && tokenExpiry(stored.token)
    return expiry && expiry <= Date.now() ? null : stored
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [auth, setAuth] = useState<AuthResponse | null>(readStored)

  const store = (value: AuthResponse | null) => {
    setAuth(value)
    try {
      if (value) localStorage.setItem(STORAGE_KEY, JSON.stringify(value))
      else localStorage.removeItem(STORAGE_KEY)
    } catch {
      /* storage unavailable */
    }
  }

  // Log out when the API rejects the token, and when it expires while the page is open.
  const logOut = useEffectEvent(() => store(null))

  useEffect(() => {
    setUnauthorizedHandler(() => logOut())
    return () => setUnauthorizedHandler(null)
  }, [])

  const token = auth?.token
  useEffect(() => {
    const expiry = token && tokenExpiry(token)
    if (!expiry) return
    const timer = setTimeout(() => logOut(), Math.max(0, expiry - Date.now()))
    return () => clearTimeout(timer)
  }, [token])

  const value: AuthState = {
    user: auth?.user ?? null,
    token: auth?.token ?? null,
    login: async (email, password) => store(await api.login(email, password)),
    register: async (email, password, displayName) => store(await api.register(email, password, displayName)),
    logout: () => store(null),
  }

  return <AuthContext value={value}>{children}</AuthContext>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const ctx = use(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}
