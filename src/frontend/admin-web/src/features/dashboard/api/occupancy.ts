import { request } from '../../../shared/api/http';

export interface EventOccupancy {
  eventId: string;
  eventName: string;
  eventStartsAt: string;
  venueName: string;
  capacity: number;
  sold: number;
  occupancyPercent: number;
}

export function getEventOccupancy(from: string, to: string, signal?: AbortSignal): Promise<EventOccupancy[]> {
  const query = new URLSearchParams({ from, to });
  return request<EventOccupancy[]>(`/GetEventOccupancy?${query}`, { signal }, '/booking-api');
}
