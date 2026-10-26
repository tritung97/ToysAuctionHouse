import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { api } from '../lib/api'
import { authStore } from '../lib/authStore'
import type { AuthResponse, UserProfile } from '../lib/types'

interface AuthContextValue {
  user: UserProfile | null
  loading: boolean
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<void>
  register: (payload: {
    username: string
    email: string
    password: string
    fullName: string
    phoneNumber?: string | null
  }) => Promise<void>
  loginWithGoogle: (idToken: string) => Promise<void>
  logout: () => void
  setTokens: (tokens: AuthResponse) => void
  refreshProfile: () => Promise<void>
  updatePhone: (phoneNumber: string) => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

function applyTokens(tokens: AuthResponse) {
  authStore.set(tokens)
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserProfile | null>(null)
  // Start loading only when we actually have a token to refresh a profile for.
  const [loading, setLoading] = useState(() => !!authStore.getAccessToken())

  const loadProfile = useCallback(async () => {
    if (!authStore.getAccessToken()) {
      setUser(null)
      setLoading(false)
      return
    }
    try {
      const profile = await api.me()
      setUser(profile)
    } catch {
      setUser(null)
      authStore.clear()
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (!authStore.getAccessToken()) return
    loadProfile()
  }, [loadProfile])

  useEffect(() => {
    const unsubscribe = authStore.subscribe((tokens) => {
      if (tokens === null) {
        setLoading(false)
      }
    })
    return unsubscribe
  }, [])

  const finishAuth = useCallback(async (tokens: AuthResponse) => {
    applyTokens(tokens)
    await loadProfile()
  }, [loadProfile])

  const login = useCallback(
    async (email: string, password: string) => {
      const tokens = await api.login(email, password)
      await finishAuth(tokens)
    },
    [finishAuth],
  )

  const register = useCallback(
    async (payload: {
      username: string
      email: string
      password: string
      fullName: string
      phoneNumber?: string | null
    }) => {
      const tokens = await api.register(payload)
      await finishAuth(tokens)
    },
    [finishAuth],
  )

  const loginWithGoogle = useCallback(
    async (idToken: string) => {
      const tokens = await api.loginWithGoogle(idToken)
      await finishAuth(tokens)
    },
    [finishAuth],
  )

  const logout = useCallback(() => {
    authStore.clear()
    setUser(null)
  }, [])

  const setTokens = useCallback((tokens: AuthResponse) => {
    applyTokens(tokens)
  }, [])

  const updatePhone = useCallback(
    async (phoneNumber: string) => {
      const profile = await api.updatePhone(phoneNumber)
      setUser(profile)
    },
    [],
  )

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      loading,
      isAuthenticated: !!user,
      login,
      register,
      loginWithGoogle,
      logout,
      setTokens,
      refreshProfile: loadProfile,
      updatePhone,
    }),
    [user, loading, login, register, loginWithGoogle, logout, setTokens, loadProfile, updatePhone],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider')
  return ctx
}
