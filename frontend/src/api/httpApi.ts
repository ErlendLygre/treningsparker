import type { AuthResponse, NewPark, Park, ParkStatus } from '../types'
import { ApiError, type Api } from './types'

let onUnauthorized: (() => void) | null = null

/** Called when the API rejects a sent token (expired or revoked), so the app can log the user out. */
export function setUnauthorizedHandler(handler: (() => void) | null) {
  onUnauthorized = handler
}

async function request<T>(baseUrl: string, path: string, init: RequestInit = {}, token?: string): Promise<T> {
  const headers = new Headers(init.headers)
  if (init.body) headers.set('Content-Type', 'application/json')
  if (token) headers.set('Authorization', `Bearer ${token}`)

  const res = await fetch(`${baseUrl}${path}`, { ...init, headers })
  if (!res.ok) {
    let message = `Noe gikk galt (${res.status})`
    try {
      const body = await res.json()
      if (body.errors) message = Object.values(body.errors as Record<string, string[]>).flat().join(' ')
      else if (body.message) message = body.message
    } catch {
      /* no json body */
    }
    if (res.status === 401 && token) {
      message = 'Du må logge inn på nytt.'
      onUnauthorized?.()
    }
    if (res.status === 429 && message.startsWith('Noe gikk galt')) message = 'For mange forsøk. Vent litt og prøv igjen.'
    if (res.status === 403) message = 'Du har ikke tilgang til dette.'
    throw new ApiError(message, res.status)
  }
  return res.status === 204 ? (undefined as T) : res.json()
}

export function createHttpApi(baseUrl: string): Api {
  const post = (body: unknown): RequestInit => ({ method: 'POST', body: JSON.stringify(body) })
  return {
    isDemo: false,
    login: (email, password) => request<AuthResponse>(baseUrl, '/api/auth/login', post({ email, password })),
    register: (email, password, displayName) =>
      request<AuthResponse>(baseUrl, '/api/auth/register', post({ email, password, displayName })),
    getParks: () => request<Park[]>(baseUrl, '/api/parks'),
    getMyParks: (token) => request<Park[]>(baseUrl, '/api/parks/mine', {}, token),
    createPark: (token, park: NewPark) => request<Park>(baseUrl, '/api/parks', post(park), token),
    getAdminParks: (token, status?: ParkStatus) =>
      request<Park[]>(baseUrl, `/api/admin/parks${status ? `?status=${status}` : ''}`, {}, token),
    approvePark: (token, id) => request<Park>(baseUrl, `/api/admin/parks/${id}/approve`, { method: 'POST' }, token),
    rejectPark: (token, id, reason) => request<Park>(baseUrl, `/api/admin/parks/${id}/reject`, post({ reason }), token),
    deletePark: (token, id) => request<void>(baseUrl, `/api/admin/parks/${id}`, { method: 'DELETE' }, token),
  }
}
