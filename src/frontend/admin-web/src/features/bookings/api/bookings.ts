import { request } from '../../../shared/api/http';
import type { Booking } from '../types';

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

export function getBookings(params: ListBookingsParams): Promise<ListBookingsResult> {
  const query = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== '') query.set(key, String(value));
  });
  return request<ListBookingsResult>(`/ListBookings?${query}`, {}, '/booking-api');
}
