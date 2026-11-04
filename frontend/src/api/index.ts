import { createHttpApi } from './httpApi'

// Same origin: nginx (or the Vite dev server) forwards /api to the .NET API.
export const api = createHttpApi('')

export { ApiError } from './types'
export { setUnauthorizedHandler } from './httpApi'
