import seed from '../../../data/parks.seed.json'
import type { AuthResponse, NewPark, Park, ParkStatus, User } from '../types'
import { ApiError, type Api } from './types'

/**
 * In-browser stand-in for the .NET API, used for the static GitHub Pages build.
 * Data lives in localStorage, so it is per browser and NOT secure — demo only.
 */

interface StoredUser extends User {
  passwordHash: string
}

interface DemoDb {
  users: StoredUser[]
  parks: (Park & { createdByUserId: number | null })[]
  nextUserId: number
  nextParkId: number
}

const KEY = 'treningsparker-demo-db-v1'
export const DEMO_ADMIN = { email: 'admin@treningsparker.no', password: 'Admin123!' }

async function hash(text: string) {
  const bytes = new TextEncoder().encode(`treningsparker:${text}`)
  const digest = await crypto.subtle.digest('SHA-256', bytes)
  return Array.from(new Uint8Array(digest), (b) => b.toString(16).padStart(2, '0')).join('')
}

async function load(): Promise<DemoDb> {
  try {
    const raw = localStorage.getItem(KEY)
    if (raw) return JSON.parse(raw)
  } catch {
    /* fall through and reseed */
  }
  const now = new Date().toISOString()
  const db: DemoDb = {
    users: [
      {
        id: 1,
        email: DEMO_ADMIN.email,
        displayName: 'Administrator',
        isAdmin: true,
        passwordHash: await hash(DEMO_ADMIN.password),
      },
    ],
    parks: seed.map((p, i) => ({
      ...p,
      id: i + 1,
      status: 'Approved' as const,
      rejectionReason: null,
      submittedBy: null,
      createdByUserId: null,
      createdAt: now,
      reviewedAt: now,
    })),
    nextUserId: 2,
    nextParkId: seed.length + 1,
  }
  save(db)
  return db
}

function save(db: DemoDb) {
  try {
    localStorage.setItem(KEY, JSON.stringify(db))
  } catch {
    /* storage unavailable: data lives for this page load only */
  }
}

// The "token" in demo mode is just the user id.
function userFromToken(db: DemoDb, token: string, requireAdmin = false) {
  const user = db.users.find((u) => String(u.id) === token)
  if (!user) throw new ApiError('Du må logge inn på nytt.', 401)
  if (requireAdmin && !user.isAdmin) throw new ApiError('Du har ikke tilgang til dette.', 403)
  return user
}

type StoredPark = DemoDb['parks'][number]

function strip(p: StoredPark): Park {
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  const { createdByUserId, ...park } = p
  return park
}

function auth(user: StoredUser): AuthResponse {
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  const { passwordHash, ...publicUser } = user
  return { token: String(user.id), user: publicUser }
}

export function createDemoApi(): Api {
  const findPark = (db: DemoDb, id: number) => {
    const park = db.parks.find((p) => p.id === id)
    if (!park) throw new ApiError('Fant ikke parken.', 404)
    return park
  }
  const review = async (token: string, id: number, status: ParkStatus, reason: string | null) => {
    const db = await load()
    userFromToken(db, token, true)
    const park = findPark(db, id)
    park.status = status
    park.rejectionReason = reason
    park.reviewedAt = new Date().toISOString()
    save(db)
    return strip(park)
  }

  return {
    isDemo: true,
    async login(email, password) {
      const db = await load()
      const user = db.users.find((u) => u.email === email.trim().toLowerCase())
      if (!user || user.passwordHash !== (await hash(password))) throw new ApiError('Feil e-post eller passord.', 401)
      return auth(user)
    },
    async register(email, password, displayName) {
      const db = await load()
      email = email.trim().toLowerCase()
      if (!email.includes('@')) throw new ApiError('Ugyldig e-postadresse.', 400)
      if (password.length < 8) throw new ApiError('Passordet må være minst 8 tegn.', 400)
      if (!displayName.trim()) throw new ApiError('Navn er påkrevd.', 400)
      if (db.users.some((u) => u.email === email))
        throw new ApiError('Det finnes allerede en bruker med denne e-postadressen.', 409)
      const user: StoredUser = {
        id: db.nextUserId++,
        email,
        displayName: displayName.trim(),
        isAdmin: false,
        passwordHash: await hash(password),
      }
      db.users.push(user)
      save(db)
      return auth(user)
    },
    async getParks() {
      const db = await load()
      return db.parks.filter((p) => p.status === 'Approved').map(strip)
    },
    async getMyParks(token) {
      const db = await load()
      const user = userFromToken(db, token)
      return db.parks.filter((p) => p.createdByUserId === user.id).reverse().map(strip)
    },
    async createPark(token, input: NewPark) {
      const db = await load()
      const user = userFromToken(db, token)
      const park = {
        ...input,
        id: db.nextParkId++,
        status: 'Pending' as const,
        rejectionReason: null,
        submittedBy: user.displayName,
        createdByUserId: user.id,
        createdAt: new Date().toISOString(),
        reviewedAt: null,
      }
      db.parks.push(park)
      save(db)
      return strip(park)
    },
    async getAdminParks(token, status) {
      const db = await load()
      userFromToken(db, token, true)
      return db.parks
        .filter((p) => !status || p.status === status)
        .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
        .map(strip)
    },
    approvePark: (token, id) => review(token, id, 'Approved', null),
    rejectPark: (token, id, reason) => review(token, id, 'Rejected', reason || null),
    async deletePark(token, id) {
      const db = await load()
      userFromToken(db, token, true)
      findPark(db, id)
      db.parks = db.parks.filter((p) => p.id !== id)
      save(db)
    },
  }
}

export function resetDemoData() {
  try {
    localStorage.removeItem(KEY)
  } catch {
    /* ignore */
  }
}
