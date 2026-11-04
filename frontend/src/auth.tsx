import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
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

  const store = useCallback((value: AuthResponse | null) => {
    setAuth(value)
    try {
      if (value) localStorage.setItem(STORAGE_KEY, JSON.stringify(value))
      else localStorage.removeItem(STORAGE_KEY)
    } catch {
      /* storage unavailable */
    }
  }, [])

  // Log out when the API rejects the token, and when it expires while the page is open.
  useEffect(() => {
    setUnauthorizedHandler(() => store(null))
    return () => setUnauthorizedHandler(null)
  }, [store])

  useEffect(() => {
    const expiry = auth && tokenExpiry(auth.token)
    if (!expiry) return
    const timer = setTimeout(() => store(null), Math.max(0, expiry - Date.now()))
    return () => clearTimeout(timer)
  }, [auth, store])

  const value = useMemo<AuthState>(
    () => ({
      user: auth?.user ?? null,
      token: auth?.token ?? null,
      login: async (email, password) => store(await api.login(email, password)),
      register: async (email, password, displayName) => store(await api.register(email, password, displayName)),
      logout: () => store(null),
    }),
    [auth, store],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}
