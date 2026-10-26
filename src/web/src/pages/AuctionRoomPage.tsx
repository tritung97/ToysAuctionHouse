import { useMemo, useRef, useState } from 'react'
import { useParams } from 'react-router-dom'
import BidButton from '../components/BidButton'
import BidFeed from '../components/BidFeed'
import CountdownTimer from '../components/CountdownTimer'
import PhoneRequiredModal from '../components/PhoneRequiredModal'
import { useAuth } from '../context/AuthContext'
import { useAuctionSocket } from '../hooks/useAuctionSocket'
import { useIdempotency } from '../hooks/useIdempotency'
import { AuctionStatusName, ConditionNames } from '../lib/types'

export default function AuctionRoomPage() {
  const { id } = useParams<{ id: string }>()
  const auctionId = id ?? ''
  const { user } = useAuth()
  const { room, hubStatus, error, extendedToast, ended, winnerName, finalAmount, sendBid } =
    useAuctionSocket(auctionId)
  const { nextKey, inFlight, beginFlight, endFlight } = useIdempotency()
  const [phoneGateOpen, setPhoneGateOpen] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)
  const submittingRef = useRef(false)

  const statusName = room ? AuctionStatusName[room.status] ?? room.status : 'Loading'
  const isLive = room?.status === '2'

  const minAmount = useMemo(() => {
    if (!room) return 0
    return Math.max(room.startPrice, room.currentPrice + room.stepPrice)
  }, [room])

  async function handleBid(amount: number) {
    setSubmitError(null)
    if (!user) {
      setSubmitError('You need to sign in to place a bid.')
      return
    }
    if (!user.phoneNumber) {
      setPhoneGateOpen(true)
      return
    }
    if (!room || isNaN(amount) || amount < minAmount) {
      setSubmitError(`Your bid must be at least $${minAmount.toLocaleString()}.`)
      return
    }
    if (submittingRef.current) return
    submittingRef.current = true
    beginFlight()
    try {
      const key = nextKey()
      await sendBid(amount, key)
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : 'Failed to place bid.')
    } finally {
      submittingRef.current = false
      endFlight()
    }
  }

  return (
    <div className="space-y-6">
      {phoneGateOpen && <PhoneRequiredModal onFinished={() => setPhoneGateOpen(false)} />}

      {extendedToast && (
        <div className="rounded-xl border border-amber-500/30 bg-amber-500/10 px-4 py-3 text-sm text-amber-300">
          ⏱ Auction extended — anti-sniping window triggered!
        </div>
      )}

      {room && (
        <div className="grid gap-6 lg:grid-cols-3">
          <div className="lg:col-span-2 space-y-4">
            <div className="overflow-hidden rounded-2xl border border-white/10 bg-white/5">
              <div className="aspect-[16/9] bg-slate-900">
                {room.imageUrls[0] ? (
                  <img src={room.imageUrls[0]} alt={room.productName} className="h-full w-full object-cover" />
                ) : (
                  <div className="flex h-full items-center justify-center text-slate-500">No image</div>
                )}
              </div>
              <div className="space-y-2 p-5">
                <div className="flex items-center gap-2">
                  <h1 className="text-2xl font-bold text-white">{room.productName}</h1>
                  <span className="rounded-full bg-emerald-500/15 px-2 py-0.5 text-xs text-emerald-300">
                    {statusName}
                  </span>
                </div>
                <p className="text-sm text-slate-400">
                  {room.brand} · {room.category} · {ConditionNames[room.condition] ?? `Condition ${room.condition}`}
                  {room.scale ? ` · ${room.scale}` : ''}
                </p>
                <p className="text-sm text-slate-300">{room.description}</p>
                <div className="pt-2 text-xs text-slate-500">
                  Seller: <span className="text-slate-300">{room.sellerName}</span>
                </div>
              </div>
            </div>

            <div className="rounded-2xl border border-white/10 bg-white/5 p-5">
              <h2 className="mb-3 text-sm font-semibold uppercase tracking-wider text-slate-400">
                Bid history
              </h2>
              <BidFeed bids={room.topBids} />
            </div>
          </div>

          <div className="space-y-4">
            <div className="rounded-2xl border border-white/10 bg-white/5 p-5 text-center">
              <p className="text-xs uppercase tracking-wider text-slate-400">Current price</p>
              <p className="mt-1 text-4xl font-bold text-emerald-300">
                ${room.currentPrice.toLocaleString()}
              </p>
              <p className="mt-1 text-sm text-slate-500">
                Step: ${room.stepPrice.toLocaleString()} · Start: ${room.startPrice.toLocaleString()}
              </p>
            </div>

            <div className="rounded-2xl border border-white/10 bg-white/5 p-5">
              <p className="mb-2 text-xs uppercase tracking-wider text-slate-400">Time remaining</p>
              <CountdownTimer endTime={room.endTime} />
            </div>

            {ended && (
              <div className="rounded-2xl border border-emerald-500/30 bg-emerald-500/10 p-5">
                <h2 className="text-lg font-semibold text-emerald-300">Auction ended 🏆</h2>
                <p className="mt-1 text-sm text-white">
                  {winnerName ?? 'No winner'}{' '}
                  {finalAmount !== null ? `won for $${finalAmount.toLocaleString()}` : ''}
                </p>
              </div>
            )}

            {!ended && (
              <BidButton
                disabled={!isLive || hubStatus !== 'connected'}
                minAmount={minAmount}
                step={room.stepPrice}
                currentPrice={room.currentPrice}
                busy={inFlight}
                onSubmit={handleBid}
              />
            )}

            {isLive && <p className="text-center text-xs text-slate-500">Connection: {hubStatus}</p>}
            {(error || submitError) && (
              <p className="text-center text-sm text-rose-400">{submitError ?? error}</p>
            )}
          </div>
        </div>
      )}

      {!room && (
        <div className="py-16 text-center">
          {error || submitError ? (
            <p className="text-rose-400">
              {submitError ?? error ?? 'Auction unavailable.'}
              <span className="block text-sm text-slate-500">Try picking a different auction from the catalog.</span>
            </p>
          ) : (
            <p className="text-slate-400">
              {hubStatus === 'connected' ? 'Loading auction…' : 'Connecting to auction room…'}
            </p>
          )}
        </div>
      )}
    </div>
  )
}
