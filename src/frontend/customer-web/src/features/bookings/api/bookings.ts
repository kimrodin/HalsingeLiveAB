import { getBlob, getJson, postJson } from '../../../shared/api/http'

const bookingApiBase = '/booking-api'

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
  guestAccessToken: string
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

function guestTokenHeader(accessToken: string): HeadersInit {
  return { 'X-Guest-Access-Token': accessToken }
}

export function getBooking(
  bookingId: string,
  accessToken: string,
  signal?: AbortSignal,
): Promise<BookingDetails> {
  return getJson<BookingDetails>(
    `/GetBooking/${encodeURIComponent(bookingId)}`,
    signal,
    bookingApiBase,
    guestTokenHeader(accessToken),
  )
}

export function startPayment(bookingId: string, accessToken: string): Promise<StartPaymentResponse> {
  return postJson<StartPaymentResponse>(
    `/StartPayment/${encodeURIComponent(bookingId)}`,
    undefined,
    bookingApiBase,
    guestTokenHeader(accessToken),
  )
}

export function cancelBooking(bookingId: string, accessToken: string): Promise<BookingStatusResponse> {
  return postJson<BookingStatusResponse>(
    `/CancelBooking/${encodeURIComponent(bookingId)}`,
    undefined,
    bookingApiBase,
    guestTokenHeader(accessToken),
  )
}

export function getTicketsForBooking(
  bookingId: string,
  accessToken: string,
  signal?: AbortSignal,
): Promise<Ticket[]> {
  return getJson<Ticket[]>(
    `/GetTicketsForBooking/${encodeURIComponent(bookingId)}/tickets`,
    signal,
    bookingApiBase,
    guestTokenHeader(accessToken),
  )
}

export function getTicketPdf(
  bookingId: string,
  ticketId: string,
  accessToken: string,
  download: boolean,
): Promise<Blob> {
  return getBlob(
    `/DownloadTicketPdf/${encodeURIComponent(bookingId)}/tickets/${encodeURIComponent(ticketId)}/pdf?download=${download}`,
    bookingApiBase,
    guestTokenHeader(accessToken),
  )
}
