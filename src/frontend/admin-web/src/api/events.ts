import { request, sendJson } from './http';
import type {
  CreateEventRequest,
  EventDetails,
  EventSectionPrice,
  UpdateEventRequest,
} from './types';

interface EventDto {
  eventId: string;
  name: string;
  description: string;
  startDate: string;
  endDate: string;
  onSaleFrom: string;
  venueId: string;
  sectionPrices: EventSectionPrice[];
}

interface EventListItemDto {
  eventId: string;
  eventName: string;
  eventDescription: string;
  sectionPrices: EventSectionPrice[];
  startDate: string;
  endDate: string;
  onSaleFrom: string;
  venueId: string;
}

interface ListEventsDto {
  events: EventListItemDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ListEventsParams {
  eventName?: string;
  venueId?: string;
  startAfter?: string;
  startsBefore?: string;
  page?: number;
  pageSize?: number;
}

export interface ListEventsResult {
  events: EventDetails[];
  totalCount: number;
  page: number;
  pageSize: number;
}

interface CreateEventResponseDto {
  eventId: string;
}

function eventFromDto(event: EventDto): EventDetails {
  return {
    eventId: event.eventId,
    venueId: event.venueId,
    name: event.name,
    description: event.description,
    startDate: event.startDate,
    endDate: event.endDate,
    onSaleFrom: event.onSaleFrom,
    sectionPrices: event.sectionPrices,
  };
}

function eventFromListItem(event: EventListItemDto): EventDetails {
  return {
    eventId: event.eventId,
    venueId: event.venueId,
    name: event.eventName,
    description: event.eventDescription,
    startDate: event.startDate,
    endDate: event.endDate,
    onSaleFrom: event.onSaleFrom,
    sectionPrices: event.sectionPrices,
  };
}

export async function getEvents(params: ListEventsParams = {}): Promise<ListEventsResult> {
  const query = new URLSearchParams();
  if (params.eventName) query.set('EventName', params.eventName);
  if (params.venueId) query.set('VenueId', params.venueId);
  if (params.startAfter) query.set('StartAfter', params.startAfter);
  if (params.startsBefore) query.set('StartsBefore', params.startsBefore);
  query.set('Page', String(params.page ?? 1));
  query.set('PageSize', String(params.pageSize ?? 10));

  const result = await request<ListEventsDto>(`/ListEvents?${query.toString()}`);
  return {
    ...result,
    events: result.events.map(eventFromListItem),
  };
}

export async function getEvent(eventId: string): Promise<EventDetails> {
  return eventFromDto(await request<EventDto>(`/GetEvent/${eventId}`));
}

export function createEvent(data: CreateEventRequest): Promise<CreateEventResponseDto> {
  return sendJson<CreateEventResponseDto>('POST', '/CreateEvent', {
    name: data.name,
    description: data.description,
    venueId: data.venueId,
    startDate: data.startDate,
    endDate: data.endDate,
    onSaleFrom: data.onSaleFrom,
    sectionPrices: data.sectionPrices,
  });
}

export async function updateEvent(eventId: string, data: UpdateEventRequest): Promise<void> {
  const current = await getEvent(eventId);
  await sendJson('PUT', `/UpdateEvent/${eventId}`, {
    name: data.name,
    description: data.description,
    startDate: data.startDate,
    endDate: data.endDate,
    onSaleFrom: data.onSaleFrom,
    venueId: current.venueId,
    sectionPrices: data.sectionPrices,
  });
}

export function deleteEvent(eventId: string): Promise<void> {
  return request(`/DeleteEvent/${eventId}`, { method: 'DELETE' });
}
