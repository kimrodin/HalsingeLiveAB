import { getJson } from '../../../shared/api/http'

export const eventPageSize = 24

export interface EventListItem {
  eventId: string
  eventName: string
  eventDescription: string
  imageUrl?: string | null
  sectionPrices: Array<{ sectionId: string; price: number }>
  startDate: string
  endDate: string
  onSaleFrom: string
  venueId: string
}

export interface EventDetail extends Omit<EventListItem, 'eventName' | 'eventDescription'> {
  name: string
  description: string
}

interface EventPage {
  events: EventListItem[]
  totalCount: number
  page: number
  pageSize: number
}

export async function getEventPage(
  page: number,
  options: { eventName?: string; endsAfter?: string } = {},
  signal?: AbortSignal,
): Promise<EventPage> {
  const query = new URLSearchParams({ Page: String(page), PageSize: String(eventPageSize) })
  if (options.eventName) query.set('EventName', options.eventName)
  if (options.endsAfter) query.set('EndsAfter', options.endsAfter)
  const result = await getJson<EventPage>(`/ListEvents?${query.toString()}`, signal)
  return {
    ...result,
    events: result.events.map((event) => ({ ...event, imageUrl: toImageUrl(event.imageUrl) })),
  }
}

function toImageUrl(url?: string | null): string | null {
  if (!url) return null
  return /^https?:\/\//i.test(url) || url.startsWith('/api/') ? url : `/api/${url.replace(/^\//, '')}`
}

export async function getEventById(eventId: string, signal?: AbortSignal): Promise<EventDetail> {
  const event = await getJson<EventDetail>(`/GetEvent/${encodeURIComponent(eventId)}`, signal)
  return { ...event, imageUrl: toImageUrl(event.imageUrl) }
}
