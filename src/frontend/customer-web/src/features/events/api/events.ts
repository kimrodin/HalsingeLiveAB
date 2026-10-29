import { getJson } from '../../../shared/api/http'

const pageSize = 100

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

async function getEventPage(page: number, signal?: AbortSignal): Promise<EventPage> {
  const query = new URLSearchParams({ Page: String(page), PageSize: String(pageSize) })
  return getJson<EventPage>(`/ListEvents?${query.toString()}`, signal)
}

export async function getAllEvents(signal?: AbortSignal): Promise<EventListItem[]> {
  const firstPage = await getEventPage(1, signal)
  const events = [...firstPage.events]

  for (let page = 2; events.length < firstPage.totalCount; page += 1) {
    const nextPage = await getEventPage(page, signal)
    if (nextPage.events.length === 0) break
    events.push(...nextPage.events)
  }

  return events.map((event) => ({ ...event, imageUrl: toImageUrl(event.imageUrl) }))
}

function toImageUrl(url?: string | null): string | null {
  if (!url) return null
  return /^https?:\/\//i.test(url) || url.startsWith('/api/') ? url : `/api/${url.replace(/^\//, '')}`
}

export async function getEventById(eventId: string, signal?: AbortSignal): Promise<EventDetail> {
  const event = await getJson<EventDetail>(`/GetEvent/${encodeURIComponent(eventId)}`, signal)
  return { ...event, imageUrl: toImageUrl(event.imageUrl) }
}
