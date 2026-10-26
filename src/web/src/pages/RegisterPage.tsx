import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'
import { ApiError } from '../lib/api'

export default function RegisterPage() {
  const { register } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({
    username: '',
    email: '',
    fullName: '',
    phoneNumber: '',
    password: '',
    confirm: '',
  })
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  function set<K extends keyof typeof form>(key: K, value: string) {
    setForm((prev) => ({ ...prev, [key]: value }))
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (form.password !== form.confirm) {
      setError('Passwords do not match.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      await register({
        username: form.username.trim(),
        email: form.email.trim(),
        fullName: form.fullName.trim(),
        password: form.password,
        phoneNumber: form.phoneNumber.trim() || null,
      })
      navigate('/')
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Registration failed.')
    } finally {
      setBusy(false)
    }
  }

  const field =
    'w-full rounded-lg border border-white/10 bg-white/5 px-3 py-2 text-sm text-white focus:border-indigo-400 focus:outline-none'
  const label = 'mb-1 block text-xs text-slate-400'

  return (
    <div className="mx-auto max-w-md">
      <div className="rounded-2xl border border-white/10 bg-white/5 p-6">
        <h1 className="text-2xl font-bold text-white">Create account</h1>
        <p className="mt-1 text-sm text-slate-400">Join the auction house.</p>

        <form onSubmit={handleSubmit} className="mt-5 space-y-4">
          <div>
            <label className={label} htmlFor="username">
              Username
            </label>
            <input id="username" value={form.username} onChange={(e) => set('username', e.target.value)} className={field} required />
          </div>
          <div>
            <label className={label} htmlFor="email">
              Email
            </label>
            <input id="email" type="email" value={form.email} onChange={(e) => set('email', e.target.value)} className={field} required />
          </div>
          <div>
            <label className={label} htmlFor="fullName">
              Full name
            </label>
            <input id="fullName" value={form.fullName} onChange={(e) => set('fullName', e.target.value)} className={field} required />
          </div>
          <div>
            <label className={label} htmlFor="phone">
              Phone number (optional)
            </label>
            <input id="phone" value={form.phoneNumber} onChange={(e) => set('phoneNumber', e.target.value)} className={field} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className={label} htmlFor="password">
                Password
              </label>
              <input id="password" type="password" value={form.password} onChange={(e) => set('password', e.target.value)} className={field} required />
            </div>
            <div>
              <label className={label} htmlFor="confirm">
                Confirm
              </label>
              <input id="confirm" type="password" value={form.confirm} onChange={(e) => set('confirm', e.target.value)} className={field} required />
            </div>
          </div>
          {error && <p className="text-sm text-rose-400">{error}</p>}
          <button type="submit" disabled={busy} className="w-full rounded-lg bg-indigo-500 px-4 py-2 font-medium text-white hover:bg-indigo-400 disabled:opacity-50">
            {busy ? 'Creating…' : 'Register'}
          </button>
        </form>

        <p className="mt-4 text-center text-sm text-slate-400">
          Already registered?{' '}
          <Link to="/login" className="text-indigo-300 hover:underline">
            Sign in
          </Link>
        </p>
      </div>
    </div>
  )
}
