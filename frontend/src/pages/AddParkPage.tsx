import { useActionState, useState } from 'react'
import { MapContainer, Marker, TileLayer, useMapEvents } from 'react-leaflet'
import { Link } from 'react-router'
import { api } from '../api'
import { useAuth } from '../auth'
import { newParkIcon, REGION_CENTER, TILE_ATTRIBUTION, TILE_URL } from '../components/markers'
import { CITIES, EQUIPMENT, PARK_TYPES, type Park } from '../types'

type LatLng = { lat: number; lng: number }

function LocationPicker({ value, onChange }: { value: LatLng | null; onChange: (v: LatLng) => void }) {
  useMapEvents({ click: (e) => onChange(e.latlng) })
  return value ? (
    <Marker
      position={value}
      icon={newParkIcon}
      draggable
      eventHandlers={{ dragend: (e) => onChange(e.target.getLatLng()) }}
    />
  ) : null
}

export default function AddParkPage() {
  const { token } = useAuth()
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [city, setCity] = useState<string>(CITIES[0])
  const [area, setArea] = useState('')
  const [parkType, setParkType] = useState<string>(PARK_TYPES[0])
  const [equipment, setEquipment] = useState<string[]>([])
  const [hasLighting, setHasLighting] = useState(false)
  const [location, setLocation] = useState<LatLng | null>(null)

  const toggleEquipment = (item: string) =>
    setEquipment((list) => (list.includes(item) ? list.filter((e) => e !== item) : [...list, item]))

  // The fields are controlled (the map picker and checkboxes need state), so the action reads them from state.
  const [{ error, created }, submit, pending] = useActionState(
    async (): Promise<{ error: string | null; created: Park | null }> => {
      if (!location) return { error: 'Klikk i kartet for å velge hvor parken ligger.', created: null }
      try {
        const park = await api.createPark(token!, {
          name,
          description,
          city,
          area,
          parkType,
          equipment,
          hasLighting,
          latitude: Number(location.lat.toFixed(6)),
          longitude: Number(location.lng.toFixed(6)),
        })
        return { error: null, created: park }
      } catch (err) {
        return { error: err instanceof Error ? err.message : String(err), created: null }
      }
    },
    { error: null, created: null },
  )

  if (created) {
    return (
      <div className="page narrow">
        <div className="card">
          <h1>Takk! 🙌</h1>
          <p>
            <strong>{created.name}</strong> er sendt inn og venter på godkjenning fra en administrator. Den vises på
            kartet så snart den er godkjent.
          </p>
          <div className="actions">
            <Link to="/mine-forslag" className="btn btn-primary">Se mine forslag</Link>
            <Link to="/" className="btn btn-ghost">Tilbake til kartet</Link>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="page">
      <h1>Foreslå en ny treningspark</h1>
      <p className="muted">Forslaget blir synlig på kartet etter at en administrator har godkjent det.</p>
      <form className="add-park" action={submit}>
        <div className="card form">
          <label>
            Navn på parken
            <input required maxLength={150} value={name} onChange={(e) => setName(e.target.value)} placeholder="F.eks. Mosvatnet Tuftepark" />
          </label>
          <div className="filter-row">
            <label>
              By
              <select value={city} onChange={(e) => setCity(e.target.value)}>
                {CITIES.map((c) => <option key={c}>{c}</option>)}
              </select>
            </label>
            <label>
              Område / bydel
              <input value={area} maxLength={100} onChange={(e) => setArea(e.target.value)} placeholder="F.eks. Madla" />
            </label>
          </div>
          <label>
            Type
            <select value={parkType} onChange={(e) => setParkType(e.target.value)}>
              {PARK_TYPES.map((t) => <option key={t}>{t}</option>)}
            </select>
          </label>
          <fieldset>
            <legend>Apparater</legend>
            <div className="checkbox-grid">
              {EQUIPMENT.map((item) => (
                <label key={item} className="checkbox">
                  <input type="checkbox" checked={equipment.includes(item)} onChange={() => toggleEquipment(item)} />
                  {item}
                </label>
              ))}
            </div>
          </fieldset>
          <label className="checkbox">
            <input type="checkbox" checked={hasLighting} onChange={(e) => setHasLighting(e.target.checked)} />
            Parken har belysning
          </label>
          <label>
            Beskrivelse
            <textarea rows={3} maxLength={2000} value={description} onChange={(e) => setDescription(e.target.value)} />
          </label>
          {error && <p className="error">{error}</p>}
          <button className="btn btn-primary" disabled={pending}>{pending ? 'Sender…' : 'Send inn forslag'}</button>
        </div>
        <div className="card picker">
          <p className="small">
            <strong>Plassering:</strong>{' '}
            {location ? `${location.lat.toFixed(5)}, ${location.lng.toFixed(5)} (dra markøren for å justere)` : 'klikk i kartet'}
          </p>
          <MapContainer center={REGION_CENTER} zoom={11} className="map picker-map">
            <TileLayer url={TILE_URL} attribution={TILE_ATTRIBUTION} />
            <LocationPicker value={location} onChange={setLocation} />
          </MapContainer>
        </div>
      </form>
    </div>
  )
}
