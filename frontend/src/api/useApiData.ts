import { useEffect, useEffectEvent, useState } from 'react'

interface Loaded<T> {
  key: string
  data?: T
  error?: string
}

/**
 * Loads data for `key` and loads again whenever the key changes. A request that is no longer needed
 * (key changed, component unmounted) is aborted, so a slow old response can never replace newer data.
 */
export function useApiData<T>(key: string, fetcher: (signal: AbortSignal) => Promise<T>) {
  const [loaded, setLoaded] = useState<Loaded<T> | null>(null)
  const [version, setVersion] = useState(0)
  // Always calls the latest fetcher without making the effect depend on it.
  const fetchLatest = useEffectEvent(fetcher)

  useEffect(() => {
    const controller = new AbortController()
    const load = async () => {
      try {
        const data = await fetchLatest(controller.signal)
        setLoaded({ key, data })
      } catch (err) {
        if (!controller.signal.aborted) setLoaded({ key, error: err instanceof Error ? err.message : String(err) })
      }
    }
    void load()
    return () => controller.abort()
  }, [key, version])

  // Results for an earlier key are stale; while reloading the same key, the previous data stays visible.
  const current = loaded?.key === key ? loaded : null
  return {
    data: current?.data,
    error: current?.error ?? null,
    loading: current === null,
    reload: () => setVersion((v) => v + 1),
  }
}
