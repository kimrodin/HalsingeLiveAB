import { ApiError, BASE_URL, request, sendJson } from '../../../shared/api/http';
import type {
  CreateEventRequest,
  EventDetails,
  EventSectionPrice,
  UpdateEventRequest,
} from '../types';

interface EventListItemDto {
  eventId: string;
  eventName: string;
  eventDescription: string;
  imageUrl?: string | null;
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
  endsAfter?: string;
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
  inventoryProvisioned: boolean;
}

export interface EventInventorySection {
  eventId: string;
  sectionId: string;
}

export function hasCompleteEventInventory(
  event: Pick<EventDetails, 'eventId' | 'sectionPrices'>,
  inventory: EventInventorySection[],
): boolean {
  const sections = new Set(inventory.filter(row => row.eventId === event.eventId).map(row => row.sectionId));
  return event.sectionPrices.length > 0 && event.sectionPrices.every(section => sections.has(section.sectionId));
}

export function getEventInventory(eventId: string): Promise<EventInventorySection[]> {
  return request(`/ListEventInventory/${eventId}`, {}, '/booking-api');
}

export async function retryEventInventory(eventId: string): Promise<EventInventorySection[]> {
  try {
    return await request(`/CreateEventInventoryFromCatalog/from-catalog/${eventId}`, { method: 'POST' }, '/booking-api');
  } catch (error) {
    if (!(error instanceof ApiError) || error.status !== 409) throw error;

    // A previous attempt may have committed before its response was lost.
    // A conflict alone does not prove that every section has inventory.
    const [event, inventory] = await Promise.all([getEvent(eventId), getEventInventory(eventId)]);
    if (hasCompleteEventInventory(event, inventory)) return inventory;
    throw new Error('Biljettlagret är ofullständigt och behöver kontrolleras av en systemadministratör.', { cause: error });
  }
}

function eventFromListItem(event: EventListItemDto): EventDetails {
  return {
    eventId: event.eventId,
    venueId: event.venueId,
    name: event.eventName,
    description: event.eventDescription,
    imageUrl: eventImageUrl(event.imageUrl),
    startDate: event.startDate,
    endDate: event.endDate,
    onSaleFrom: event.onSaleFrom,
    sectionPrices: event.sectionPrices,
  };
}

export async function getEvents(params: ListEventsParams = {}, signal?: AbortSignal): Promise<ListEventsResult> {
  const query = new URLSearchParams();
  if (params.eventName) query.set('EventName', params.eventName);
  if (params.venueId) query.set('VenueId', params.venueId);
  // datetime-local has no timezone; send UTC just like the event form does.
  if (params.startAfter) query.set('StartAfter', new Date(params.startAfter).toISOString());
  if (params.endsAfter) query.set('EndsAfter', new Date(params.endsAfter).toISOString());
  if (params.startsBefore) query.set('StartsBefore', new Date(params.startsBefore).toISOString());
  query.set('Page', String(params.page ?? 1));
  query.set('PageSize', String(params.pageSize ?? 10));

  const result = await request<ListEventsDto>(`/ListEvents?${query.toString()}`, { signal });
  return {
    ...result,
    events: result.events.map(eventFromListItem),
  };
}

export function eventImageUrl(url?: string | null): string | null {
  if (!url) return null;
  return /^https?:\/\//i.test(url) ? url : `${BASE_URL.replace(/\/$/, '')}/${url.replace(/^\//, '')}`;
}

export async function getEvent(eventId: string): Promise<EventDetails> {
  const event = await request<EventDetails>(`/GetEvent/${eventId}`);
  return { ...event, imageUrl: eventImageUrl(event.imageUrl) };
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

export function updateEvent(eventId: string, data: UpdateEventRequest): Promise<void> {
  return sendJson('PUT', `/UpdateEvent/${eventId}`, {
    name: data.name,
    description: data.description,
    startDate: data.startDate,
    endDate: data.endDate,
    onSaleFrom: data.onSaleFrom,
    venueId: data.venueId,
    sectionPrices: data.sectionPrices,
  });
}

export function deleteEvent(eventId: string): Promise<void> {
  return request(`/DeleteEvent/${eventId}`, { method: 'DELETE' });
}
