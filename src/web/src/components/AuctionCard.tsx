import { Link } from 'react-router-dom'
import { AuctionStatusName, ConditionNames, type ProductCard } from '../lib/types'
import CountdownTimer from './CountdownTimer'

const StatusColor: Record<string, string> = {
  '1': 'bg-amber-500/15 text-amber-300',
  '2': 'bg-emerald-500/15 text-emerald-300',
  '3': 'bg-slate-500/15 text-slate-300',
  '4': 'bg-rose-500/15 text-rose-300',
}

export default function AuctionCard({ item }: { item: ProductCard }) {
  const statusLabel = AuctionStatusName[String(item.status)] ?? String(item.status)

  return (
    <Link
      to={`/auction/${item.id}`}
      className="group overflow-hidden rounded-2xl border border-white/10 bg-white/5 transition hover:border-indigo-400/40"
    >
      <div className="aspect-[4/3] w-full overflow-hidden bg-slate-900">
        {item.imageUrl ? (
          <img
            src={item.imageUrl}
            alt={item.name}
            className="h-full w-full object-cover transition group-hover:scale-105"
          />
        ) : (
          <div className="flex h-full items-center justify-center text-slate-500">No image</div>
        )}
      </div>
      <div className="space-y-2 p-4">
        <div className="flex items-start justify-between gap-2">
          <h3 className="font-medium leading-tight group-hover:text-indigo-300">{item.name}</h3>
          <span
            className={`shrink-0 rounded-full px-2 py-0.5 text-xs ${StatusColor[String(item.status)] ?? StatusColor['1']}`}
          >
            {statusLabel}
          </span>
        </div>
        <p className="text-xs text-slate-400">
          {item.brand} · {ConditionNames[item.condition] ?? `Condition ${item.condition}`}
        </p>
        <div className="flex items-center justify-between pt-2">
          <div>
            <p className="text-xs text-slate-400">Current price</p>
            <p className="text-lg font-semibold text-emerald-300">
              ${item.currentPrice.toLocaleString()}
            </p>
          </div>
          {item.endTime && <CountdownTimer endTime={item.endTime} compact />}
        </div>
      </div>
    </Link>
  )
}
