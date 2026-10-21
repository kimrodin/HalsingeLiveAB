import { getJson } from '../../../shared/api/http'

const pageSize = 100

export interface EventListItem {
  eventId: string
  eventName: string
  eventDescription: string
  sectionPrices: Array<{ sectionId: string; price: number }>
  startDate: string
  endDate: string
  onSaleFrom: string
  venueId: string
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

  return events
}
