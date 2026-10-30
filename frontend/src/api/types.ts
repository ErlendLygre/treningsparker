import type { AuthResponse, NewPark, Park, ParkStatus } from '../types'

export class ApiError extends Error {
  constructor(message: string, public status: number) {
    super(message)
  }
}

/** Everything the UI needs from a backend. Implemented by the .NET API client and the in-browser demo. */
export interface Api {
  readonly isDemo: boolean
  login(email: string, password: string): Promise<AuthResponse>
  register(email: string, password: string, displayName: string): Promise<AuthResponse>
  getParks(): Promise<Park[]>
  getMyParks(token: string): Promise<Park[]>
  createPark(token: string, park: NewPark): Promise<Park>
  getAdminParks(token: string, status?: ParkStatus): Promise<Park[]>
  approvePark(token: string, id: number): Promise<Park>
  rejectPark(token: string, id: number, reason: string): Promise<Park>
  deletePark(token: string, id: number): Promise<void>
}
