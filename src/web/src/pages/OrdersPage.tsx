import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../lib/api'
import { OrderStatusNames, type Order } from '../lib/types'

export default function OrdersPage() {
  const queryClient = useQueryClient()
  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['orders'],
    queryFn: api.orders,
  })

  const updateStatus = useMutation({
    mutationFn: ({ id, status }: { id: string; status: number }) => api.updateOrderStatus(id, status),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['orders'] }),
  })

  function rowTitle(order: Order) {
    return `#${order.id} · $${order.finalAmount.toLocaleString()} · ${OrderStatusNames[order.status] ?? order.status}`
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-white">Orders</h1>
        <p className="text-sm text-slate-400">Auctions you bought or sold.</p>
      </div>

      {isLoading && <p className="text-slate-400">Loading orders…</p>}
      {isError && (
        <p className="text-rose-400">
          Failed to load orders: {error instanceof Error ? error.message : 'Unknown error'}
        </p>
      )}

      {data && data.length === 0 && <p className="text-slate-400">No orders yet.</p>}

      {data && data.length > 0 && (
        <div className="space-y-3">
          {data.map((order) => (
            <div key={order.id} className="rounded-2xl border border-white/10 bg-white/5 p-4">
              <div className="flex items-center justify-between gap-4">
                <div className="min-w-0">
                  <p className="font-medium text-white">{rowTitle(order)}</p>
                  <p className="mt-1 text-xs text-slate-400">
                    You {order.sellerName === order.buyerName ? 'both' : 'are involved with'}: buyer{' '}
                    <span className="text-slate-200">{order.buyerName}</span> ({order.buyerPhone}) · seller{' '}
                    <span className="text-slate-200">{order.sellerName}</span> ({order.sellerPhone})
                  </p>
                </div>
                {order.status === 1 && (
                  <div className="flex shrink-0 gap-2">
                    <button
                      onClick={() => updateStatus.mutate({ id: order.id, status: 2 })}
                      className="rounded-lg bg-emerald-500 px-3 py-1.5 text-xs font-medium text-white hover:bg-emerald-400"
                    >
                      Complete
                    </button>
                    <button
                      onClick={() => updateStatus.mutate({ id: order.id, status: 3 })}
                      className="rounded-lg border border-white/10 px-3 py-1.5 text-xs hover:bg-white/5"
                    >
                      Cancel
                    </button>
                  </div>
                )}
              </div>
              <p className="mt-2 text-xs text-slate-500">Created {new Date(order.createdAt).toLocaleString()}</p>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
