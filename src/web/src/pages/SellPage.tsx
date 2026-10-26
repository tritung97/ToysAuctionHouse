import { useState } from 'react'
import type { FormEvent } from 'react'
import { useMutation } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import PhoneRequiredModal from '../components/PhoneRequiredModal'
import { useAuth } from '../context/AuthContext'
import { api, ApiError } from '../lib/api'
import { ConditionNames } from '../lib/types'

const Categories = ['Gunpla', 'SH.Figuarts', 'Hot Toys', 'Custom Kit']
const Conditions = Object.entries(ConditionNames).map(([value, label]) => ({ value: Number(value), label }))
const imageFields = 4

export default function SellPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [phoneGateOpen, setPhoneGateOpen] = useState(false)
  const [form, setForm] = useState({
    name: '',
    category: '',
    brand: '',
    scale: '',
    condition: 1,
    description: '',
    startPrice: 0,
    stepPrice: 0,
    startTime: '',
    endTime: '',
    isAntiSnipingEnabled: true,
    imageUrls: Array.from({ length: imageFields }, () => ''),
  })

  const createAuction = useMutation({
    mutationFn: () =>
      api.createAuction({
        product: {
          name: form.name,
          category: form.category,
          brand: form.brand,
          scale: form.scale || null,
          condition: form.condition,
          description: form.description,
          imageUrls: form.imageUrls.filter((url) => url.trim() !== ''),
        },
        auction: {
          startPrice: form.startPrice,
          stepPrice: form.stepPrice,
          startTime: new Date(form.startTime).toISOString(),
          endTime: new Date(form.endTime).toISOString(),
          isAntiSnipingEnabled: form.isAntiSnipingEnabled,
        },
      }),
    onSuccess: (id) => navigate(`/auction/${id}`),
  })

  const [error, setError] = useState<string | null>(null)

  function set<K extends keyof typeof form>(key: K, value: unknown) {
    setForm((prev) => ({ ...prev, [key]: value }))
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    if (!user?.phoneNumber) {
      setPhoneGateOpen(true)
      return
    }
    try {
      await createAuction.mutateAsync()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to create listing.')
    }
  }

  const field =
    'w-full rounded-lg border border-white/10 bg-white/5 px-3 py-2 text-sm text-white focus:border-indigo-400 focus:outline-none'
  const label = 'mb-1 block text-xs text-slate-400'

  return (
    <div className="mx-auto max-w-2xl">
      {phoneGateOpen && <PhoneRequiredModal onFinished={() => setPhoneGateOpen(false)} />}

      <div className="rounded-2xl border border-white/10 bg-white/5 p-6">
        <h1 className="text-2xl font-bold text-white">List an item</h1>
        <p className="mt-1 text-sm text-slate-400">Create a product and schedule an auction.</p>

        <form onSubmit={handleSubmit} className="mt-5 space-y-5">
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label className={label} htmlFor="name">Item name</label>
              <input id="name" value={form.name} onChange={(e) => set('name', e.target.value)} className={field} required />
            </div>
            <div>
              <label className={label} htmlFor="brand">Brand</label>
              <input id="brand" value={form.brand} onChange={(e) => set('brand', e.target.value)} className={field} required />
            </div>
            <div>
              <label className={label} htmlFor="category">Category</label>
              <select id="category" value={form.category} onChange={(e) => set('category', e.target.value)} className={field} required>
                <option value="">Select…</option>
                {Categories.map((c) => <option key={c} value={c}>{c}</option>)}
              </select>
            </div>
            <div>
              <label className={label} htmlFor="scale">Scale / Size</label>
              <input id="scale" placeholder="e.g. 1/100" value={form.scale} onChange={(e) => set('scale', e.target.value)} className={field} />
            </div>
            <div>
              <label className={label} htmlFor="condition">Condition</label>
              <select id="condition" value={form.condition} onChange={(e) => set('condition', Number(e.target.value))} className={field}>
                {Conditions.map((c) => <option key={c.value} value={c.value}>{c.label}</option>)}
              </select>
            </div>
          </div>

          <div>
            <label className={label} htmlFor="description">Description</label>
            <textarea id="description" rows={3} value={form.description} onChange={(e) => set('description', e.target.value)} className={field} required />
          </div>

          <div>
            <span className={label}>Image URLs (optional)</span>
            <div className="space-y-2">
              {form.imageUrls.map((url, i) => (
                <input
                  key={i}
                  value={url}
                  onChange={(e) => {
                    const next = [...form.imageUrls]
                    next[i] = e.target.value
                    set('imageUrls', next)
                  }}
                  placeholder={`Image URL ${i + 1}`}
                  className={field}
                />
              ))}
            </div>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label className={label} htmlFor="startPrice">Start price ($)</label>
              <input id="startPrice" type="number" min={1} step={1} value={form.startPrice} onChange={(e) => set('startPrice', Number(e.target.value))} className={field} required />
            </div>
            <div>
              <label className={label} htmlFor="stepPrice">Min bid step ($)</label>
              <input id="stepPrice" type="number" min={1} step={1} value={form.stepPrice} onChange={(e) => set('stepPrice', Number(e.target.value))} className={field} required />
            </div>
            <div>
              <label className={label} htmlFor="startTime">Start time</label>
              <input id="startTime" type="datetime-local" value={form.startTime} onChange={(e) => set('startTime', e.target.value)} className={field} required />
            </div>
            <div>
              <label className={label} htmlFor="endTime">End time</label>
              <input id="endTime" type="datetime-local" value={form.endTime} onChange={(e) => set('endTime', e.target.value)} className={field} required />
            </div>
          </div>

          <label className="flex items-center gap-2 text-sm text-slate-300">
            <input
              type="checkbox"
              checked={form.isAntiSnipingEnabled}
              onChange={(e) => set('isAntiSnipingEnabled', e.target.checked)}
              className="h-4 w-4"
            />
            Enable anti-sniping (extend end time when bids land in the final window)
          </label>

          {error && <p className="text-sm text-rose-400">{error}</p>}

          <button
            type="submit"
            disabled={createAuction.isPending}
            className="w-full rounded-lg bg-indigo-500 px-4 py-2.5 font-medium text-white hover:bg-indigo-400 disabled:opacity-50"
          >
            {createAuction.isPending ? 'Creating auction…' : 'Create auction'}
          </button>
        </form>
      </div>
    </div>
  )
}
