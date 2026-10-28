import { getJson, postJson } from '../../../shared/api/http'

const bookingApiBase = '/booking-api'
const ticketApiBase = '/ticket-api'

export interface EventInventory {
  inventoryId: string
  eventId: string
  sectionId: string
  totalQuantity: number
  availableQuantity: number
  price: number
}

export interface BookingItem {
  inventoryId: string
  quantity: number
  unitPrice: number
}

export interface BookingDetails {
  bookingId: string
  customerName: string
  customerEmail: string
  status: string
  totalPrice: number
  createdAt: string
  expiresAt: string | null
  items: BookingItem[]
}

export interface CreateBookingRequest {
  inventoryId: string
  quantity: number
  customerName: string
  customerEmail: string
}

export interface CreateBookingResponse {
  bookingId: string
  expiresAt: string
  message: string
}

export interface BookingStatusResponse {
  bookingId: string
  status: string
}

export interface StartPaymentResponse {
  bookingId: string
  bookingStatus: string
  paymentStatus: string
  message: string
}

export interface Ticket {
  ticketId: string
  bookingId: string
  bookingItemId: string
  ticketNumberInBookingItem: number
  eventId: string
  eventName: string
  eventStartsAt: string
  venueName: string
  sectionName: string
  unitPrice: number
  currency: string
  status: string
  issuedAt: string
}

export function getEventInventory(eventId: string, signal?: AbortSignal): Promise<EventInventory[]> {
  return getJson<EventInventory[]>(`/ListEventInventory/${encodeURIComponent(eventId)}`, signal, bookingApiBase)
}

export function createBooking(request: CreateBookingRequest): Promise<CreateBookingResponse> {
  return postJson<CreateBookingResponse>('/CreateBooking', request, bookingApiBase)
}

export function getBooking(bookingId: string, signal?: AbortSignal): Promise<BookingDetails> {
  return getJson<BookingDetails>(`/GetBooking/${encodeURIComponent(bookingId)}`, signal, bookingApiBase)
}

export function startPayment(bookingId: string): Promise<StartPaymentResponse> {
  return postJson<StartPaymentResponse>(`/StartPayment/${encodeURIComponent(bookingId)}`, undefined, bookingApiBase)
}

export function cancelBooking(bookingId: string): Promise<BookingStatusResponse> {
  return postJson<BookingStatusResponse>(`/CancelBooking/${encodeURIComponent(bookingId)}`, undefined, bookingApiBase)
}

export function getTicketsForBooking(bookingId: string, signal?: AbortSignal): Promise<Ticket[]> {
  return getJson<Ticket[]>(
    `/tickets/by-booking/${encodeURIComponent(bookingId)}`,
    signal,
    ticketApiBase,
  )
}
