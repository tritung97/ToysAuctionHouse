import { useCountdown } from '../hooks/useCountdown'

export default function CountdownTimer({
  endTime,
  compact = false,
}: {
  endTime: string | Date | null
  compact?: boolean
}) {
  const { days, hours, minutes, seconds, isOver } = useCountdown(endTime)

  if (isOver) {
    return <span className="text-xs font-medium text-rose-300">Ended</span>
  }

  const pad = (n: number) => String(n).padStart(2, '0')

  if (compact) {
    const label =
      days > 0
        ? `${days}d ${pad(hours)}h ${pad(minutes)}m`
        : `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`
    return <span className="font-mono text-xs text-indigo-300">{label}</span>
  }

  const segments =
    days > 0
      ? [
          { value: String(days), label: 'days' },
          { value: pad(hours), label: 'hrs' },
          { value: pad(minutes), label: 'min' },
          { value: pad(seconds), label: 'sec' },
        ]
      : [
          { value: pad(hours), label: 'hrs' },
          { value: pad(minutes), label: 'min' },
          { value: pad(seconds), label: 'sec' },
        ]

  return (
    <div className="flex items-center gap-2">
      {segments.map((seg) => (
        <div
          key={seg.label}
          className="rounded-xl border border-white/10 bg-white/5 px-3 py-2 text-center"
        >
          <div className="font-mono text-xl font-semibold tabular-nums">{seg.value}</div>
          <div className="text-[10px] uppercase tracking-wider text-slate-400">{seg.label}</div>
        </div>
      ))}
    </div>
  )
}
