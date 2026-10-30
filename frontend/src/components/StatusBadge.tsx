import { STATUS_LABEL, type ParkStatus } from '../types'

export default function StatusBadge({ status }: { status: ParkStatus }) {
  return <span className={`badge badge-${status.toLowerCase()}`}>{STATUS_LABEL[status]}</span>
}
