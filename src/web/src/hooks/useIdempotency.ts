import { useCallback, useRef, useState } from 'react'
import { uuid } from '../lib/authStore'

/**
 * Generates one idempotency key per bid attempt. A prior key is consumed on each
 * call so a single tap → a single bid → a single, distinct key.
 */
export function useIdempotency() {
  const keyRef = useRef<string | null>(null)
  const [inFlight, setInFlight] = useState(false)

  const nextKey = useCallback(() => {
    const key = uuid()
    keyRef.current = key
    return key
  }, [])

  const currentKey = useCallback(() => keyRef.current, [])

  const beginFlight = useCallback(() => setInFlight(true), [])
  const endFlight = useCallback(() => {
    setInFlight(false)
    keyRef.current = null
  }, [])

  return { nextKey, currentKey, inFlight, beginFlight, endFlight }
}
