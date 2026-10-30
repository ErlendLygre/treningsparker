import { useCallback, useEffect, useState } from 'react'
import { MapContainer, Marker, TileLayer } from 'react-leaflet'
import { api } from '../api'
import { resetDemoData } from '../api/demoApi'
import { useAuth } from '../auth'
import ParkDetails from '../components/ParkDetails'
import StatusBadge from '../components/StatusBadge'
import { parkIcon, TILE_ATTRIBUTION, TILE_URL } from '../components/markers'
import type { Park, ParkStatus } from '../types'

const TABS: { status: ParkStatus | undefined; label: string }[] = [
  { status: 'Pending', label: 'Venter' },
  { status: 'Approved', label: 'Godkjent' },
  { status: 'Rejected', label: 'Avvist' },
  { status: undefined, label: 'Alle' },
]

export default function AdminPage() {
  const { token, logout } = useAuth()
  const [tab, setTab] = useState<ParkStatus | undefined>('Pending')
  const [parks, setParks] = useState<Park[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(() => {
    setError(null)
    api.getAdminParks(token!, tab).then(setParks).catch((e: Error) => setError(e.message))
  }, [token, tab])

  useEffect(load, [load])

  const run = async (action: () => Promise<unknown>) => {
    try {
      await action()
      load()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const reject = (p: Park) => {
    const reason = window.prompt(`Begrunnelse for å avvise «${p.name}» (valgfritt):`)
    if (reason !== null) run(() => api.rejectPark(token!, p.id, reason))
  }

  const remove = (p: Park) => {
    if (window.confirm(`Slette «${p.name}» permanent?`)) run(() => api.deletePark(token!, p.id))
  }

  return (
    <div className="page">
      <div className="page-head">
        <h1>Administrer parker</h1>
        {api.isDemo && (
          <button
            className="btn btn-ghost"
            onClick={() => {
              if (window.confirm('Tilbakestill all demodata i denne nettleseren?')) {
                resetDemoData()
                logout()
                window.location.reload()
              }
            }}
          >
            Tilbakestill demodata
          </button>
        )}
      </div>
      <div className="tabs" role="tablist">
        {TABS.map((t) => (
          <button key={t.label} role="tab" aria-selected={tab === t.status} className={tab === t.status ? 'active' : ''} onClick={() => setTab(t.status)}>
            {t.label}
          </button>
        ))}
      </div>
      {error && <p className="error">{error}</p>}
      {parks?.length === 0 && <p className="muted">Ingen parker her.</p>}
      <div className="cards">
        {parks?.map((p) => (
          <article key={p.id} className="card admin-card">
            <div>
              <div className="card-head">
                <StatusBadge status={p.status} />
                <span className="muted small">{new Date(p.createdAt).toLocaleString('nb-NO')}</span>
              </div>
              <ParkDetails park={p} />
              {p.rejectionReason && <p className="small">Avvist: {p.rejectionReason}</p>}
              <div className="actions">
                {p.status !== 'Approved' && (
                  <button className="btn btn-primary" onClick={() => run(() => api.approvePark(token!, p.id))}>Godkjenn</button>
                )}
                {p.status !== 'Rejected' && <button className="btn btn-ghost" onClick={() => reject(p)}>Avvis</button>}
                <button className="btn btn-danger" onClick={() => remove(p)}>Slett</button>
              </div>
            </div>
            <MapContainer center={[p.latitude, p.longitude]} zoom={15} className="mini-map" scrollWheelZoom={false} attributionControl={false}>
              <TileLayer url={TILE_URL} attribution={TILE_ATTRIBUTION} />
              <Marker position={[p.latitude, p.longitude]} icon={parkIcon(p.parkType)} />
            </MapContainer>
          </article>
        ))}
      </div>
    </div>
  )
}
