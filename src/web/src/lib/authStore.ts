import type { AuthResponse } from './types'

const ACCESS_KEY = 'ToysAuction.accessToken'
const REFRESH_KEY = 'ToysAuction.refreshToken'
const EXPIRES_KEY = 'ToysAuction.expiresAt'

// Small pub/sub so the api layer can notify AuthContext when a refresh occurs.
type Listener = (tokens: AuthResponse | null) => void
const listeners = new Set<Listener>()

function notify(tokens: AuthResponse | null) {
  for (const listener of listeners) listener(tokens)
}

export const authStore = {
  subscribe(listener: Listener) {
    listeners.add(listener)
    return () => {
      listeners.delete(listener)
    }
  },
  getAccessToken(): string | null {
    return localStorage.getItem(ACCESS_KEY)
  },
  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_KEY)
  },
  set(tokens: AuthResponse) {
    localStorage.setItem(ACCESS_KEY, tokens.accessToken)
    localStorage.setItem(REFRESH_KEY, tokens.refreshToken)
    localStorage.setItem(EXPIRES_KEY, tokens.expiresAt)
    notify(tokens)
  },
  isExpired(): boolean {
    const expires = localStorage.getItem(EXPIRES_KEY)
    if (!expires) return true
    const millis = Date.parse(expires)
    if (Number.isNaN(millis)) return true
    return Date.now() >= millis - 15_000
  },
  clear() {
    localStorage.removeItem(ACCESS_KEY)
    localStorage.removeItem(REFRESH_KEY)
    localStorage.removeItem(EXPIRES_KEY)
    notify(null)
  },
}

export function uuid(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID().replace(/-/g, '')
  }
  // Fallback for non-secure contexts.
  return 'xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx'.replace(/x/g, () =>
    Math.floor(Math.random() * 16).toString(16),
  )
}
