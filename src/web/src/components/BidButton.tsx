import { useEffect, useState } from 'react'

interface BidButtonProps {
  disabled?: boolean
  minAmount: number
  step: number
  currentPrice: number
  busy?: boolean
  onSubmit: (amount: number) => void
}

export default function BidButton({
  disabled = false,
  minAmount,
  step,
  currentPrice,
  busy = false,
  onSubmit,
}: BidButtonProps) {
  const [amount, setAmount] = useState(currentPrice + step)
  const next = Math.max(minAmount, currentPrice + step)

  // Keep the input in sync with the live price (a competing bid may have raised it).
  useEffect(() => {
    setAmount(next)
  }, [next])

  function handleAmount(value: string) {
    const parsed = Number(value)
    setAmount(value === '' ? 0 : parsed)
  }

  function submit() {
    // Clamp to the current minimum so we never send a stale/too-low bid.
    onSubmit(Math.max(next, amount))
  }

  return (
    <div className="space-y-3 rounded-2xl border border-white/10 bg-white/5 p-4">
      <div>
        <label className="mb-1 block text-xs text-slate-400" htmlFor="bid-amount">
          Your bid
        </label>
        <div className="flex items-center gap-2">
          <span className="text-slate-400">$</span>
          <input
            id="bid-amount"
            type="number"
            step={step}
            min={next}
            value={amount}
            onChange={(e) => handleAmount(e.target.value)}
            disabled={disabled || busy}
            className="w-full rounded-lg border border-white/10 bg-white/5 px-3 py-2 font-mono text-lg text-white focus:border-emerald-400 focus:outline-none disabled:opacity-50"
          />
        </div>
        <p className="mt-1 text-xs text-slate-500">Minimum {next.toLocaleString()}</p>
      </div>
      <button
        type="button"
        disabled={disabled || busy || amount < next}
        onClick={submit}
        className="w-full rounded-lg bg-emerald-500 px-4 py-3 font-semibold text-white transition hover:bg-emerald-400 disabled:cursor-not-allowed disabled:opacity-50"
      >
        {busy ? 'Placing bid…' : 'Place bid'}
      </button>
    </div>
  )
}
