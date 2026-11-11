import { Link } from 'react-router'
import { api } from '../api'
import { useApiData } from '../api/useApiData'
import { useAuth } from '../auth'
import StatusBadge from '../components/StatusBadge'

export default function MyParksPage() {
  const { token } = useAuth()
  const { data: parks, error } = useApiData(`mine:${token}`, (signal) => api.getMyParks(token!, signal))

  return (
    <div className="page">
      <div className="page-head">
        <h1>Mine forslag</h1>
        <Link to="/ny-park" className="btn btn-primary">+ Foreslå park</Link>
      </div>
      {error && <p className="error">{error}</p>}
      {parks?.length === 0 && <p className="muted">Du har ikke foreslått noen parker ennå.</p>}
      <div className="cards">
        {parks?.map((p) => (
          <article key={p.id} className="card">
            <div className="card-head">
              <h3>{p.name}</h3>
              <StatusBadge status={p.status} />
            </div>
            <p className="muted small">
              {p.parkType} · {p.area ? `${p.area}, ` : ''}{p.city} · sendt {new Date(p.createdAt).toLocaleDateString('nb-NO')}
            </p>
            {p.status === 'Rejected' && p.rejectionReason && <p className="small">Begrunnelse: {p.rejectionReason}</p>}
          </article>
        ))}
      </div>
    </div>
  )
}
