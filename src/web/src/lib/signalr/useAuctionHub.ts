import { useCallback, useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import { authStore } from '../authStore'
import type {
  AuctionEndedPayload,
  AuctionStartedPayload,
  BidRejectedPayload,
  BidResult,
  ReceiveNewBidPayload,
} from '../types'

export type HubStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

export interface AuctionHubHandlers {
  onRoomSnapshot?: (room: unknown) => void
  onNewBid?: (bid: ReceiveNewBidPayload) => void
  onExtended?: (newEndTime: string) => void
  onEnded?: (payload: AuctionEndedPayload) => void
  onStarted?: (payload: AuctionStartedPayload) => void
  onAccepted?: (bid: BidResult) => void
  onRejected?: (payload: BidRejectedPayload) => void
}

interface UseAuctionHubOptions {
  auctionId?: string
  handlers?: AuctionHubHandlers
}

export function useAuctionHub({ auctionId, handlers }: UseAuctionHubOptions) {
  const [status, setStatus] = useState<HubStatus>('disconnected')
  const connectionRef = useRef<signalR.HubConnection | null>(null)
  const joinedRef = useRef<string | null>(null)
  const latest = useRef<{ handlers: AuctionHubHandlers; auctionId?: string }>({
    handlers: handlers ?? {},
    auctionId,
  })

  useEffect(() => {
    latest.current = { handlers: handlers ?? {}, auctionId }
  }, [handlers, auctionId])

  const join = useCallback(async (id: string) => {
    const connection = connectionRef.current
    if (!connection || connection.state !== signalR.HubConnectionState.Connected || !id) return
    const previous = joinedRef.current
    if (previous !== null && previous !== id) {
      await connection.invoke('LeaveAuctionRoom', previous).catch(() => undefined)
    }
    await connection.invoke('JoinAuctionRoom', id)
    joinedRef.current = id
  }, [])

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/auction', {
        accessTokenFactory: () => authStore.getAccessToken() ?? '',
      })
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
      .build()

    connection.on('RoomSnapshot', (room) => latest.current.handlers.onRoomSnapshot?.(room))
    connection.on('ReceiveNewBid', (bid) => latest.current.handlers.onNewBid?.(bid))
    connection.on('AuctionExtended', (newEndTime) => latest.current.handlers.onExtended?.(newEndTime))
    connection.on('AuctionEnded', (payload) => latest.current.handlers.onEnded?.(payload))
    connection.on('AuctionStarted', (payload) => latest.current.handlers.onStarted?.(payload))
    connection.on('BidAccepted', (bid) => latest.current.handlers.onAccepted?.(bid))
    connection.on('BidRejected', (payload) => latest.current.handlers.onRejected?.(payload))

    connection.onreconnecting(() => setStatus('reconnecting'))
    connection.onreconnected(() => setStatus('connected'))
    connection.onclose(() => setStatus('disconnected'))

    connectionRef.current = connection

    let cancelled = false
    async function start() {
      setStatus('connecting')
      try {
        await connection.start()
        if (cancelled) return
        setStatus('connected')
      } catch {
        if (!cancelled) setStatus('disconnected')
      }
    }

    void start()

    return () => {
      cancelled = true
      connectionRef.current = null
      void connection.stop()
    }
    // join is stable (useCallback []); eslint exhaustive-deps not enabled in oxlint by default.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Re-join whenever the auctionId changes while connected (e.g. /auction/1 → /auction/2).
  useEffect(() => {
    if (status === 'connected' && auctionId !== undefined) {
      void join(auctionId)
    } else if (auctionId !== undefined) {
      joinedRef.current = null
    }
  }, [auctionId, status, join])

  const sendBid = useCallback(
    async (auctionIdArg: string, amount: number, idempotencyKey: string) => {
      const connection = connectionRef.current
      if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
        throw new Error('Not connected.')
      }
      await connection.invoke('SendBid', {
        auctionId: auctionIdArg,
        amount,
        idempotencyKey,
      })
    },
    [],
  )

  return { status, sendBid }
}
