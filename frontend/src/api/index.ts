import { createDemoApi } from './demoApi'
import { createHttpApi } from './httpApi'

// Demo mode runs entirely in the browser (used on GitHub Pages where there is no backend).
export const api =
  import.meta.env.VITE_DEMO_MODE === 'true' ? createDemoApi() : createHttpApi(import.meta.env.VITE_API_URL ?? '')

export { ApiError } from './types'
export { setUnauthorizedHandler } from './httpApi'
