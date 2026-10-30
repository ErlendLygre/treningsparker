import L from 'leaflet'

export const TYPE_COLORS: Record<string, string> = {
  Tuftepark: '#0f8a5f',
  Calisthenicspark: '#2563eb',
  Treningspark: '#d97706',
  Hinderløype: '#be185d',
}

const cache = new Map<string, L.DivIcon>()

export function parkIcon(parkType: string, highlighted = false) {
  const key = `${parkType}-${highlighted}`
  let icon = cache.get(key)
  if (!icon) {
    const color = TYPE_COLORS[parkType] ?? '#475569'
    icon = L.divIcon({
      className: 'park-marker',
      html: `<span class="pin${highlighted ? ' pin-active' : ''}" style="--pin:${color}"></span>`,
      iconSize: [28, 36],
      iconAnchor: [14, 34],
      popupAnchor: [0, -30],
    })
    cache.set(key, icon)
  }
  return icon
}

export const newParkIcon = L.divIcon({
  className: 'park-marker',
  html: '<span class="pin pin-new" style="--pin:#111827"></span>',
  iconSize: [28, 36],
  iconAnchor: [14, 34],
})

// Centre between Stavanger and Sandnes.
export const REGION_CENTER: L.LatLngTuple = [58.905, 5.74]
export const TILE_URL = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png'
export const TILE_ATTRIBUTION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>-bidragsytere'
