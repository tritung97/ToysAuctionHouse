// Mirrors ToysAuctionHouse.Application.Dtos (C# records) 1:1 for type safety.

export interface ApiEnvelope<T> {
  success: boolean
  data: T | null
  errorCode: string | null
  message: string | null
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface AuthResponse {
  accessToken: string
  refreshToken: string
  expiresAt: string
  userId: string
  username: string
}

export interface UserProfile {
  id: string
  username: string
  email: string
  fullName: string
  phoneNumber: string | null
  avatarUrl: string | null
  authProvider: number
}

export interface ProductCard {
  id: string
  name: string
  category: string
  brand: string
  condition: number
  currentPrice: number
  endTime: string | null
  status: number
  imageUrl: string | null
}

export interface ProductDetail {
  id: string
  sellerId: string
  sellerName: string
  sellerPhone: string
  name: string
  category: string
  scale: string | null
  brand: string
  condition: number
  description: string
  imageUrls: string[]
  auctionStatus: number | null
  endTime: string | null
}

export interface Bid {
  bidderId: string
  bidderName: string
  amount: number
  timestamp: string
}

export interface AuctionRoom {
  auctionId: string
  productId: string
  productName: string
  category: string
  brand: string
  scale: string | null
  condition: number
  description: string
  imageUrls: string[]
  sellerId: string
  sellerName: string
  sellerPhone: string
  startPrice: number
  stepPrice: number
  currentPrice: number
  startTime: string
  endTime: string
  status: string
  isAntiSnipingEnabled: boolean
  topBids: Bid[]
}

export interface BidResult {
  status: string
  currentPrice: number
  winnerId: string | null
  extended: boolean
  newEndTime: string | null
  message: string | null
}

export interface Order {
  id: string
  auctionSessionId: string
  buyerId: string
  buyerName: string
  buyerPhone: string
  sellerId: string
  sellerName: string
  sellerPhone: string
  finalAmount: number
  status: number
  note: string | null
  createdAt: string
}

export interface RegisterRequest {
  username: string
  email: string
  password: string
  fullName: string
  phoneNumber?: string | null
}

export interface CreateAuctionRequest {
  product: {
    name: string
    category: string
    scale: string | null
    brand: string
    condition: number
    description: string
    imageUrls: string[]
  }
  auction: {
    startPrice: number
    stepPrice: number
    startTime: string
    endTime: string
    isAntiSnipingEnabled: boolean
  }
}

export interface CatalogFilter {
  category?: string
  condition?: number
  status?: number
  pageNumber: number
  pageSize: number
}

// ------- SignalR event payloads (HubContract) -------

export interface ReceiveNewBidPayload {
  bidderId?: string
  bidderName: string
  amount: number
  timestamp: string
}

export interface AuctionEndedPayload {
  auctionId: string
  winnerName: string | null
  finalAmount: number
}

export interface AuctionStartedPayload {
  auctionId: string
  startTime: string
}

export interface BidRejectedPayload {
  status: string
  message: string
}

// AuctionSessionStatus string values as carried in AuctionRoom.status
export type AuctionStatus = '1' | '2' | '3' | '4'
export const AuctionStatusName: Record<string, string> = {
  '1': 'Scheduled',
  '2': 'Live',
  '3': 'Ended',
  '4': 'Cancelled',
}

export const ConditionNames: Record<number, string> = {
  1: 'New / Sealed',
  2: 'Second Hand',
  3: 'Custom',
}

export const OrderStatusNames: Record<number, string> = {
  1: 'Pending',
  2: 'Completed',
  3: 'Cancelled',
}
