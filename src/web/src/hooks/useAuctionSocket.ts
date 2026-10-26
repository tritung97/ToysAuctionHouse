import { useCallback, useEffect, useMemo, useState } from 'react'
import type { Bid } from '../lib/types'
import { useAuctionHub, type AuctionHubHandlers, type HubStatus } from '../lib/signalr/useAuctionHub'

export interface AuctionSocketState {
  room: import('../lib/types').AuctionRoom | null
  hubStatus: HubStatus
  error: string | null
  extendedToast: boolean
  ended: boolean
  winnerName: string | null
  finalAmount: number | null
  sendBid: (amount: number, idempotencyKey: string) => Promise<void>
}

export function useAuctionSocket(auctionId: string): AuctionSocketState {
  const [room, setRoom] = useState<import('../lib/types').AuctionRoom | null>(null)
  const [extendedToast, setExtendedToast] = useState(false)
  const [ended, setEnded] = useState(false)
  const [winnerName, setWinnerName] = useState<string | null>(null)
  const [finalAmount, setFinalAmount] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)

  const handlers = useMemo<AuctionHubHandlers>(() => {
    return {
      onRoomSnapshot: (raw) => {
        setRoom(raw as import('../lib/types').AuctionRoom)
        setError(null)
      },
      onNewBid: (bidPayload) => {
        setRoom((prev) => {
          if (!prev) return prev
          const bid: Bid = {
            bidderId: bidPayload.bidderId ?? '',
            bidderName: bidPayload.bidderName,
            amount: bidPayload.amount,
            timestamp: bidPayload.timestamp,
          }
          return {
            ...prev,
            currentPrice: Math.max(prev.currentPrice, bidPayload.amount),
            topBids: [bid, ...prev.topBids].slice(0, 20),
          }
        })
      },
      onExtended: (newEndTime) => {
        setRoom((prev) => (prev ? { ...prev, endTime: newEndTime } : prev))
        setExtendedToast(true)
      },
      onEnded: (payload) => {
        setEnded(true)
        setWinnerName(payload.winnerName)
        setFinalAmount(payload.finalAmount)
        setRoom((prev) => (prev ? { ...prev, status: '3', currentPrice: payload.finalAmount } : prev))
      },
      onRejected: (payload) => {
        setError(payload.message ?? 'Bid was rejected.')
      },
      onAccepted: (bid) => {
        setError(null)
        setRoom((prev) => (prev ? { ...prev, currentPrice: bid.currentPrice } : prev))
      },
    }
  }, [])

  const { status: hubStatus, sendBid } = useAuctionHub({ auctionId, handlers })

  // Reset room state when navigating to a different auction (SPA param change)
  // so we never flash another auction's data.
  useEffect(() => {
    setRoom(null)
    setExtendedToast(false)
    setEnded(false)
    setWinnerName(null)
    setFinalAmount(null)
    setError(null)
  }, [auctionId])

  useEffect(() => {
    if (extendedToast) {
      const t = window.setTimeout(() => setExtendedToast(false), 4000)
      return () => window.clearTimeout(t)
    }
  }, [extendedToast])

  const submitBid = useCallback(
    async (amount: number, idempotencyKey: string) => {
      await sendBid(auctionId, amount, idempotencyKey)
    },
    [sendBid, auctionId],
  )

  return {
    room,
    hubStatus,
    error,
    extendedToast,
    ended,
    winnerName,
    finalAmount,
    sendBid: submitBid,
  }
}
