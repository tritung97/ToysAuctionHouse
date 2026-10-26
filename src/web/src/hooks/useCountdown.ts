import { useEffect, useState } from 'react'

export interface Countdown {
  totalMs: number
  days: number
  hours: number
  minutes: number
  seconds: number
  isOver: boolean
}

const zero: Countdown = {
  totalMs: 0,
  days: 0,
  hours: 0,
  minutes: 0,
  seconds: 0,
  isOver: true,
}

export function useCountdown(endTime: string | Date | null | undefined): Countdown {
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    const t = window.setInterval(() => setNow(Date.now()), 500)
    return () => window.clearInterval(t)
  }, [])

  if (!endTime) return zero
  const target = Date.parse(typeof endTime === 'string' ? endTime : endTime.toISOString())
  if (Number.isNaN(target)) return zero

  const totalMs = target - now
  if (totalMs <= 0) return zero

  const totalSeconds = Math.floor(totalMs / 1000)
  const days = Math.floor(totalSeconds / 86400)
  const hours = Math.floor((totalSeconds % 86400) / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60

  return { totalMs, days, hours, minutes, seconds, isOver: false }
}
