import { request } from '../../../shared/api/http';
import type { Booking } from '../types';

export function getBookings(): Promise<Booking[]> {
  return request<Booking[]>('/ListBookings', {}, '/booking-api');
}
