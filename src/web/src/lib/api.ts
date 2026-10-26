import { authStore } from './authStore'
import type {
  ApiEnvelope,
  AuthResponse,
  AuctionRoom,
  CatalogFilter,
  CreateAuctionRequest,
  Order,
  PagedResult,
  ProductCard,
  RegisterRequest,
  UserProfile,
} from './types'

const API_BASE = '/api'

class ApiError extends Error {
  errorCode: string
  status: number

  constructor(errorCode: string, message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.errorCode = errorCode
    this.status = status
  }
}

let refreshInFlight: Promise<boolean> | null = null

async function refreshTokens(): Promise<boolean> {
  if (refreshInFlight) return refreshInFlight

  refreshInFlight = (async () => {
    const refreshToken = authStore.getRefreshToken()
    if (!refreshToken) return false

    try {
      const res = await fetch(`${API_BASE}/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      })
      if (!res.ok) return false
      const body = (await res.json()) as ApiEnvelope<AuthResponse>
      if (!body.success || !body.data) return false
      authStore.set(body.data)
      return true
    } catch {
      return false
    } finally {
      refreshInFlight = null
    }
  })()

  return refreshInFlight
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  if (init.body !== undefined && !(init.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json')
  }

  const access = authStore.getAccessToken()
  if (access) headers.set('Authorization', `Bearer ${access}`)

  let res = await fetch(`${API_BASE}${path}`, { ...init, headers })

  if (res.status === 401 && authStore.getRefreshToken() && !path.startsWith('/auth')) {
    const ok = await refreshTokens()
    if (ok) {
      headers.set('Authorization', `Bearer ${authStore.getAccessToken()}`)
      res = await fetch(`${API_BASE}${path}`, { ...init, headers })
    }
  }

  const body = (await res.json().catch(() => null)) as ApiEnvelope<T> | null

  if (!res.ok) {
    throw new ApiError(
      body?.errorCode ?? 'REQUEST_FAILED',
      body?.message ?? `Request failed with ${res.status}`,
      res.status,
    )
  }

  if (!body || !body.success) {
    throw new ApiError(
      body?.errorCode ?? 'REQUEST_FAILED',
      body?.message ?? 'Request failed.',
      res.status,
    )
  }

  return body.data as T
}

export const api = {
  // Auth
  register: (payload: RegisterRequest) =>
    request<AuthResponse>('/auth/register', { method: 'POST', body: JSON.stringify(payload) }),
  login: (email: string, password: string) =>
    request<AuthResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),
  loginWithGoogle: (idToken: string) =>
    request<AuthResponse>('/auth/google', {
      method: 'POST',
      body: JSON.stringify({ idToken }),
    }),
  refresh: () => request<AuthResponse>('/auth/refresh', { method: 'POST' }),

  // Profile
  me: () => request<UserProfile>('/users/me'),
  updatePhone: (phoneNumber: string) =>
    request<UserProfile>('/users/me/phone', {
      method: 'PUT',
      body: JSON.stringify({ phoneNumber }),
    }),

  // Catalog
  browse: (filter: CatalogFilter) => {
    const params = new URLSearchParams()
    params.set('pageNumber', String(filter.pageNumber))
    params.set('pageSize', String(filter.pageSize))
    if (filter.category) params.set('category', filter.category)
    if (filter.condition !== undefined) params.set('condition', String(filter.condition))
    if (filter.status !== undefined) params.set('status', String(filter.status))
    return request<PagedResult<ProductCard>>(`/catalog?${params.toString()}`)
  },
  roomDetail: (auctionId: string) => request<AuctionRoom>(`/auctions/${auctionId}`),

  // Auctions
  createAuction: (payload: CreateAuctionRequest) =>
    request<string>('/auctions', { method: 'POST', body: JSON.stringify(payload) }),
  cancelAuction: (id: string) => request<void>(`/auctions/${id}`, { method: 'DELETE' }),

  // Orders
  orders: () => request<Order[]>('/orders'),
  updateOrderStatus: (id: string, status: number) =>
    request<Order>(`/orders/${id}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ status }),
    }),
}

export { ApiError }
