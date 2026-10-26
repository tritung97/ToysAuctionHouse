import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import AuctionCard from '../components/AuctionCard'
import { api } from '../lib/api'
import { ConditionNames } from '../lib/types'

const Categories = ['Gunpla', 'SH.Figuarts', 'Hot Toys', 'Custom Kit']
const Conditions = Object.entries(ConditionNames).map(([value, label]) => ({ value: Number(value), label }))

export default function CatalogPage() {
  const [category, setCategory] = useState<string>('')
  const [condition, setCondition] = useState<number | ''>('')
  const [status, setStatus] = useState<number | ''>('')
  const [page, setPage] = useState(1)

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['catalog', category, condition, status, page],
    queryFn: () =>
      api.browse({
        category: category || undefined,
        condition: condition === '' ? undefined : condition,
        status: status === '' ? undefined : status,
        pageNumber: page,
        pageSize: 12,
      }),
  })

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / 12)) : 1

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-white">Collectible Toys</h1>
          <p className="text-sm text-slate-400">Live and scheduled auctions.</p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <select
            value={category}
            onChange={(e) => {
              setCategory(e.target.value)
              setPage(1)
            }}
            className="rounded-lg border border-white/10 bg-slate-900 px-3 py-2 text-sm text-white focus:border-indigo-400 focus:outline-none"
          >
            <option value="">All categories</option>
            {Categories.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </select>
          <select
            value={condition}
            onChange={(e) => {
              setCondition(e.target.value === '' ? '' : Number(e.target.value))
              setPage(1)
            }}
            className="rounded-lg border border-white/10 bg-slate-900 px-3 py-2 text-sm text-white focus:border-indigo-400 focus:outline-none"
          >
            <option value="">Any condition</option>
            {Conditions.map((c) => (
              <option key={c.value} value={c.value}>
                {c.label}
              </option>
            ))}
          </select>
          <select
            value={status}
            onChange={(e) => {
              setStatus(e.target.value === '' ? '' : Number(e.target.value))
              setPage(1)
            }}
            className="rounded-lg border border-white/10 bg-slate-900 px-3 py-2 text-sm text-white focus:border-indigo-400 focus:outline-none"
          >
            <option value="">Any status</option>
            <option value={2}>Live</option>
            <option value={1}>Scheduled</option>
            <option value={3}>Ended</option>
          </select>
        </div>
      </div>

      {isLoading && <p className="text-slate-400">Loading catalog…</p>}
      {isError && (
        <p className="text-rose-400">
          Failed to load catalog: {error instanceof Error ? error.message : 'Unknown error'}
        </p>
      )}

      {data && (
        <>
          {data.items.length === 0 ? (
            <p className="text-slate-400">No auctions match these filters.</p>
          ) : (
            <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
              {data.items.map((item) => (
                <AuctionCard key={item.id} item={item} />
              ))}
            </div>
          )}

          {totalPages > 1 && (
            <div className="flex items-center justify-center gap-3 pt-4">
              <button
                disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
                className="rounded-lg border border-white/10 px-3 py-1.5 text-sm hover:bg-white/5 disabled:opacity-40"
              >
                Previous
              </button>
              <span className="text-sm text-slate-400">
                Page {page} of {totalPages}
              </span>
              <button
                disabled={page >= totalPages}
                onClick={() => setPage((p) => p + 1)}
                className="rounded-lg border border-white/10 px-3 py-1.5 text-sm hover:bg-white/5 disabled:opacity-40"
              >
                Next
              </button>
            </div>
          )}
        </>
      )}
    </div>
  )
}
