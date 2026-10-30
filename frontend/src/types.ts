export type ParkStatus = 'Pending' | 'Approved' | 'Rejected'

export const CITIES = ['Stavanger', 'Sandnes'] as const
export const PARK_TYPES = ['Tuftepark', 'Calisthenicspark', 'Treningspark', 'Hinderløype'] as const
export const EQUIPMENT = [
  'Pull-up-stang',
  'Dips-stativ',
  'Parallell-barrer',
  'Ribbevegg',
  'Benk for sit-ups',
  'Push-up-håndtak',
  'Monkey bars',
  'Strikkhopp-stativ',
] as const

export interface Park {
  id: number
  name: string
  description: string
  city: string
  area: string
  latitude: number
  longitude: number
  parkType: string
  equipment: string[]
  hasLighting: boolean
  status: ParkStatus
  rejectionReason: string | null
  submittedBy: string | null
  createdAt: string
  reviewedAt: string | null
}

export interface NewPark {
  name: string
  description: string
  city: string
  area: string
  latitude: number
  longitude: number
  parkType: string
  equipment: string[]
  hasLighting: boolean
}

export interface User {
  id: number
  email: string
  displayName: string
  isAdmin: boolean
}

export interface AuthResponse {
  token: string
  user: User
}

export const STATUS_LABEL: Record<ParkStatus, string> = {
  Pending: 'Venter på godkjenning',
  Approved: 'Godkjent',
  Rejected: 'Avvist',
}
