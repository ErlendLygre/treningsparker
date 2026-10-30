import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import { api } from './api'
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

function readStored(): AuthResponse | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? JSON.parse(raw) : null
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
