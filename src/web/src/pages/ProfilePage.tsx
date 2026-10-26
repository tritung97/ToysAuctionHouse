import { useState } from 'react'
import type { FormEvent } from 'react'
import { useAuth } from '../context/AuthContext'
import { ApiError } from '../lib/api'

export default function ProfilePage() {
  const { user, updatePhone } = useAuth()
  const [phone, setPhone] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)
  const [busy, setBusy] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    setSaved(false)
    try {
      await updatePhone(phone.trim())
      setSaved(true)
      setPhone('')
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to update phone number.')
    } finally {
      setBusy(false)
    }
  }

  if (!user) {
    return <p className="text-slate-400">Loading profile…</p>
  }

  return (
    <div className="mx-auto max-w-md space-y-6">
      <div className="rounded-2xl border border-white/10 bg-white/5 p-6">
        <h1 className="text-2xl font-bold text-white">{user.fullName}</h1>
        <p className="text-sm text-slate-400">@{user.username}</p>

        <dl className="mt-4 space-y-2 text-sm">
          <div className="flex justify-between gap-4">
            <dt className="text-slate-400">Email</dt>
            <dd className="text-slate-200">{user.email}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-slate-400">Phone</dt>
            <dd className={user.phoneNumber ? 'text-slate-200' : 'text-amber-300'}>
              {user.phoneNumber ?? 'Not set'}
            </dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-slate-400">Auth provider</dt>
            <dd className="text-slate-200">{user.authProvider === 2 ? 'Google' : 'Local'}</dd>
          </div>
        </dl>
      </div>

      <div className="rounded-2xl border border-white/10 bg-white/5 p-6">
        <h2 className="text-lg font-semibold text-white">Update phone number</h2>
        <p className="mt-1 text-sm text-slate-400">Required before you can bid or list an auction.</p>
        <form onSubmit={handleSubmit} className="mt-4 space-y-3">
          <input
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
            placeholder="+1 555 000 1234"
            className="w-full rounded-lg border border-white/10 bg-white/5 px-3 py-2 text-sm text-white placeholder:text-slate-500 focus:border-indigo-400 focus:outline-none"
          />
          {error && <p className="text-sm text-rose-400">{error}</p>}
          {saved && <p className="text-sm text-emerald-300">Phone number updated.</p>}
          <button
            type="submit"
            disabled={busy}
            className="w-full rounded-lg bg-indigo-500 px-4 py-2 font-medium text-white hover:bg-indigo-400 disabled:opacity-50"
          >
            {busy ? 'Saving…' : 'Save'}
          </button>
        </form>
      </div>
    </div>
  )
}
