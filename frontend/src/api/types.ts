import type { AuthResponse, NewPark, Park, ParkStatus } from '../types'

export class ApiError extends Error {
  constructor(message: string, public status: number) {
    super(message)
  }
}

/** Everything the UI needs from the .NET API. */
export interface Api {
  login(email: string, password: string): Promise<AuthResponse>
  register(email: string, password: string, displayName: string): Promise<AuthResponse>
  getParks(signal?: AbortSignal): Promise<Park[]>
  getMyParks(token: string, signal?: AbortSignal): Promise<Park[]>
  createPark(token: string, park: NewPark): Promise<Park>
  getAdminParks(token: string, status?: ParkStatus, signal?: AbortSignal): Promise<Park[]>
  approvePark(token: string, id: number): Promise<Park>
  rejectPark(token: string, id: number, reason: string): Promise<Park>
  deletePark(token: string, id: number): Promise<void>
}
