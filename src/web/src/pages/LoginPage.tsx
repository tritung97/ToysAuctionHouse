import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'
import { ApiError } from '../lib/api'

interface GsiCredential {
  credential: string
}

export default function LoginPage() {
  const { login, loginWithGoogle } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [googleBusy, setGoogleBusy] = useState(false)
  const clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID as string | undefined
  const googlePromiseRef = useRef<{ resolve: (c: string) => void } | null>(null)

  // Load Google Identity Services once; wire up the one-tap credential callback.
  useEffect(() => {
    if (!clientId) return
    const existing = document.getElementById('gsi-client')
    if (existing) return
    const script = document.createElement('script')
    script.id = 'gsi-client'
    script.src = 'https://accounts.google.com/gsi/client'
    script.async = true
    script.defer = true
    script.onload = () => {
      const gsi = (window as unknown as { google?: { accounts?: { id?: { initialize: (o: unknown) => void } } } })
        .google?.accounts?.id
      gsi?.initialize({
        client_id: clientId,
        callback: (response: GsiCredential) => {
          const p = googlePromiseRef.current
          if (p) {
            p.resolve(response.credential)
            googlePromiseRef.current = null
          }
        },
      })
    }
    document.head.appendChild(script)
  }, [clientId])

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await login(email.trim(), password)
      navigate('/')
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Login failed.')
    } finally {
      setBusy(false)
    }
  }

  async function handleGoogle() {
    if (!clientId) {
      setError('Google sign-in is not configured. Set VITE_GOOGLE_CLIENT_ID and restart.')
      return
    }
    setGoogleBusy(true)
    setError(null)
    try {
      const gsi = (window as unknown as { google?: { accounts?: { id?: { prompt: () => void } } } })
        .google?.accounts?.id
      if (!gsi) {
        throw new Error('Google Identity Services failed to load.')
      }
      const credential = await new Promise<string>((resolve) => {
        googlePromiseRef.current = { resolve }
        gsi.prompt()
        // Fallback timeout in case the popup is dismissed silently.
        window.setTimeout(() => {
          if (googlePromiseRef.current) {
            googlePromiseRef.current = null
            resolve('')
          }
        }, 120000)
      })
      if (!credential) {
        setGoogleBusy(false)
        return
      }
      await loginWithGoogle(credential)
      navigate('/')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Google sign-in failed.')
    } finally {
      setGoogleBusy(false)
    }
  }

  return (
    <div className="mx-auto max-w-md">
      <div className="rounded-2xl border border-white/10 bg-white/5 p-6">
        <h1 className="text-2xl font-bold text-white">Sign in</h1>
        <p className="mt-1 text-sm text-slate-400">Welcome back to the house.</p>

        <form onSubmit={handleSubmit} className="mt-5 space-y-4">
          <div>
            <label className="mb-1 block text-xs text-slate-400" htmlFor="email">
              Email
            </label>
            <input
              id="email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="w-full rounded-lg border border-white/10 bg-white/5 px-3 py-2 text-sm text-white focus:border-indigo-400 focus:outline-none"
              required
            />
          </div>
          <div>
            <label className="mb-1 block text-xs text-slate-400" htmlFor="password">
              Password
            </label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full rounded-lg border border-white/10 bg-white/5 px-3 py-2 text-sm text-white focus:border-indigo-400 focus:outline-none"
              required
            />
          </div>
          {error && <p className="text-sm text-rose-400">{error}</p>}
          <button
            type="submit"
            disabled={busy}
            className="w-full rounded-lg bg-indigo-500 px-4 py-2 font-medium text-white hover:bg-indigo-400 disabled:opacity-50"
          >
            {busy ? 'Signing in…' : 'Sign in'}
          </button>
        </form>

        <div className="my-4 flex items-center gap-3 text-xs text-slate-500">
          <span className="h-px flex-1 bg-white/10" /> or <span className="h-px flex-1 bg-white/10" />
        </div>

        <button
          type="button"
          onClick={handleGoogle}
          disabled={googleBusy}
          className="w-full rounded-lg border border-white/10 bg-white/5 px-4 py-2 font-medium text-white hover:bg-white/10 disabled:opacity-50"
        >
          {googleBusy ? 'Opening Google…' : 'Continue with Google'}
        </button>

        <p className="mt-4 text-center text-sm text-slate-400">
          No account?{' '}
          <Link to="/register" className="text-indigo-300 hover:underline">
            Register
          </Link>
        </p>
      </div>
    </div>
  )
}
