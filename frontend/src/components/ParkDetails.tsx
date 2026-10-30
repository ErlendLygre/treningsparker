import type { Park } from '../types'

export default function ParkDetails({ park }: { park: Park }) {
  return (
    <div className="park-details">
      <h3>{park.name}</h3>
      <p className="muted">
        {park.parkType} · {park.area ? `${park.area}, ` : ''}{park.city}
        {park.hasLighting && ' · 💡 Lys'}
      </p>
      {park.description && <p>{park.description}</p>}
      {park.equipment.length > 0 && (
        <ul className="chips">
          {park.equipment.map((e) => <li key={e}>{e}</li>)}
        </ul>
      )}
      {park.submittedBy && <p className="muted small">Foreslått av {park.submittedBy}</p>}
      <a
        className="small"
        href={`https://www.google.com/maps/dir/?api=1&destination=${park.latitude},${park.longitude}`}
        target="_blank"
        rel="noreferrer"
      >
        Veibeskrivelse →
      </a>
    </div>
  )
}
