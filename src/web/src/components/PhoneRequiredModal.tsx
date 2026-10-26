import { useState } from 'react'
import type { FormEvent } from 'react'
import { useAuth } from '../context/AuthContext'
import { ApiError } from '../lib/api'

export default function PhoneRequiredModal({ onFinished }: { onFinished: () => void }) {
  const { updatePhone, user } = useAuth()
  const [phone, setPhone] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!phone.trim()) {
      setError('A phone number is required to take part in auctions.')
      return
    }
    setSaving(true)
    setError(null)
    try {
      await updatePhone(phone.trim())
      onFinished()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to save phone number.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4">
      <div className="w-full max-w-md rounded-2xl border border-white/10 bg-slate-900 p-6 shadow-2xl">
        <h2 className="text-xl font-semibold text-white">Phone number required</h2>
        <p className="mt-2 text-sm text-slate-300">
          To place bids or list auctions you need to add a phone number to your profile
          {user ? ` (${user.username})` : ''}. This helps buyers and sellers coordinate delivery.
        </p>
        <form onSubmit={handleSubmit} className="mt-5 space-y-4">
          <div>
            <label className="mb-1 block text-xs text-slate-400" htmlFor="phone">
              Phone number
            </label>
            <input
              id="phone"
              autoFocus
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
              placeholder="+1 555 000 1234"
              className="w-full rounded-lg border border-white/10 bg-white/5 px-3 py-2 text-sm text-white placeholder:text-slate-500 focus:border-indigo-400 focus:outline-none"
            />
          </div>
          {error && <p className="text-sm text-rose-400">{error}</p>}
          <div className="flex items-center justify-end gap-2 pt-2">
            <button
              type="submit"
              disabled={saving}
              className="rounded-lg bg-indigo-500 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-400 disabled:opacity-50"
            >
              {saving ? 'Saving…' : 'Save'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
