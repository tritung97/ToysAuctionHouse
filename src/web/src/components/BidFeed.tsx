import type { Bid } from '../lib/types'

export default function BidFeed({ bids, compact = false }: { bids: Bid[]; compact?: boolean }) {
  if (bids.length === 0) {
    return <p className="text-sm text-slate-400">No bids yet. Be the first!</p>
  }

  const sorted = [...bids].sort(
    (a, b) => Date.parse(b.timestamp) - Date.parse(a.timestamp),
  )

  return (
    <ul className="space-y-2">
      {sorted.map((bid, index) => (
        <li
          key={`${bid.bidderId}-${bid.timestamp}-${index}`}
          className="flex items-center justify-between rounded-xl border border-white/10 bg-white/5 px-3 py-2"
        >
          <div className="flex items-center gap-2">
            <span className="flex h-7 w-7 items-center justify-center rounded-full bg-indigo-500/20 text-xs font-semibold text-indigo-300">
              {bid.bidderName.charAt(0).toUpperCase()}
            </span>
            <span className="text-sm text-slate-200">{bid.bidderName}</span>
          </div>
          <div className="text-right">
            <div className="font-mono text-sm font-semibold text-emerald-300">
              ${bid.amount.toLocaleString()}
            </div>
            {!compact && (
              <div className="text-[10px] text-slate-500">
                {new Date(bid.timestamp).toLocaleTimeString()}
              </div>
            )}
          </div>
        </li>
      ))}
    </ul>
  )
}
