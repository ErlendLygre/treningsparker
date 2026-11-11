import { useEffect, useRef, useState } from 'react'
import { MapContainer, Marker, Popup, TileLayer, useMap } from 'react-leaflet'
import type { Marker as LeafletMarker } from 'leaflet'
import { Link } from 'react-router'
import { api } from '../api'
import { useApiData } from '../api/useApiData'
import { useAuth } from '../auth'
import ParkDetails from '../components/ParkDetails'
import { parkIcon, REGION_CENTER, TILE_ATTRIBUTION, TILE_URL, TYPE_COLORS } from '../components/markers'
import { CITIES, PARK_TYPES, type Park } from '../types'

function FlyTo({ park }: { park: Park | null }) {
  const map = useMap()
  useEffect(() => {
    if (park) map.flyTo([park.latitude, park.longitude], Math.max(map.getZoom(), 15), { duration: 0.6 })
  }, [park, map])
  return null
}

export default function MapPage() {
  const { user } = useAuth()
  const { data: parks = [], error, loading } = useApiData('parks', (signal) => api.getParks(signal))
  const [search, setSearch] = useState('')
  const [city, setCity] = useState('')
  const [type, setType] = useState('')
  const [lightOnly, setLightOnly] = useState(false)
  const [selected, setSelected] = useState<Park | null>(null)
  const markers = useRef(new Map<number, LeafletMarker>())

  const q = search.trim().toLowerCase()
  const filtered = parks.filter(
    (p) =>
      (!city || p.city === city) &&
      (!type || p.parkType === type) &&
      (!lightOnly || p.hasLighting) &&
      (!q || `${p.name} ${p.area} ${p.equipment.join(' ')}`.toLowerCase().includes(q)),
  )

  const select = (park: Park) => {
    setSelected(park)
    // Wait for the fly animation to start before opening the popup.
    setTimeout(() => markers.current.get(park.id)?.openPopup(), 650)
  }

  return (
    <div className="map-layout">
      <aside className="sidebar">
        <div className="filters">
          <input
            type="search"
            placeholder="Søk etter park, område eller apparat…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            aria-label="Søk"
          />
          <div className="filter-row">
            <select value={city} onChange={(e) => setCity(e.target.value)} aria-label="By">
              <option value="">Alle byer</option>
              {CITIES.map((c) => <option key={c}>{c}</option>)}
            </select>
            <select value={type} onChange={(e) => setType(e.target.value)} aria-label="Type">
              <option value="">Alle typer</option>
              {PARK_TYPES.map((t) => <option key={t}>{t}</option>)}
            </select>
          </div>
          <label className="checkbox">
            <input type="checkbox" checked={lightOnly} onChange={(e) => setLightOnly(e.target.checked)} />
            Kun parker med lys
          </label>
        </div>

        <p className="muted small count">
          {loading ? 'Laster parker…' : `${filtered.length} av ${parks.length} parker`}
        </p>
        {error && <p className="error">{error}</p>}

        <ul className="park-list">
          {filtered.map((p) => (
            <li key={p.id}>
              <button className={selected?.id === p.id ? 'active' : ''} onClick={() => select(p)}>
                <span className="dot" style={{ background: TYPE_COLORS[p.parkType] }} />
                <span>
                  <strong>{p.name}</strong>
                  <span className="muted small">
                    {p.parkType} · {p.city}
                    {p.hasLighting && ' · 💡'}
                  </span>
                </span>
              </button>
            </li>
          ))}
        </ul>

        <div className="sidebar-cta">
          {user ? (
            <Link to="/ny-park" className="btn btn-primary block">+ Foreslå en ny park</Link>
          ) : (
            <p className="small muted">
              Mangler det en park? <Link to="/logg-inn">Logg inn</Link> for å foreslå den.
            </p>
          )}
        </div>
      </aside>

      <section className="map-wrap">
        <MapContainer center={REGION_CENTER} zoom={11} className="map" scrollWheelZoom>
          <TileLayer url={TILE_URL} attribution={TILE_ATTRIBUTION} />
          <FlyTo park={selected} />
          {filtered.map((p) => (
            <Marker
              key={p.id}
              position={[p.latitude, p.longitude]}
              icon={parkIcon(p.parkType, selected?.id === p.id)}
              ref={(m) => {
                if (!m) return
                markers.current.set(p.id, m)
                return () => {
                  markers.current.delete(p.id)
                }
              }}
              eventHandlers={{ click: () => setSelected(p) }}
            >
              <Popup>
                <ParkDetails park={p} />
              </Popup>
            </Marker>
          ))}
        </MapContainer>
        <div className="legend">
          {PARK_TYPES.map((t) => (
            <span key={t}>
              <span className="dot" style={{ background: TYPE_COLORS[t] }} /> {t}
            </span>
          ))}
        </div>
      </section>
    </div>
  )
}
