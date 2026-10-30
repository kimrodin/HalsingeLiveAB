import { request, requestBlob, sendJson } from '../../../shared/api/http';
import type { Booking, BookingDetails } from '../types';

export interface ListBookingsParams {
  search?: string;
  status?: string;
  from?: string;
  to?: string;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
  page: number;
  pageSize: number;
}

export interface ListBookingsResult {
  bookings: Booking[];
  totalCount: number;
  page: number;
  pageSize: number;
  statuses: string[];
}

export type UpdateBookingCustomerRequest = Pick<Booking, 'customerName' | 'customerEmail'>;

export function getBookings(params: ListBookingsParams): Promise<ListBookingsResult> {
  const query = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== '') query.set(key, String(value));
  });
  return request<ListBookingsResult>(`/ListBookings?${query}`, {}, '/booking-api');
}

export function getBookingDetails(bookingId: string): Promise<BookingDetails> {
  return request<BookingDetails>(`/GetAdminBookingDetails/${bookingId}`, {}, '/booking-api');
}

export function updateBookingCustomer(
  bookingId: string,
  customer: UpdateBookingCustomerRequest,
): Promise<void> {
  return sendJson('PUT', `/UpdateBookingCustomer/${bookingId}`, customer);
}

export function getTicketPdf(bookingId: string, ticketId: string): Promise<Blob> {
  return requestBlob(
    `/GetAdminTicketPdf/${bookingId}/${ticketId}?download=false`,
    {},
    '/booking-api',
  );
}
