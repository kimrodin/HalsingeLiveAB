import { getJson } from '../../../shared/api/http'

interface VenueListItem {
  venueId: string
  venueName: string
  venueCity: string
}

interface VenueListResponse {
  venues: VenueListItem[]
}

export interface VenueSummary {
  id: string
  name: string
  city: string
}

export async function getVenues(signal?: AbortSignal): Promise<VenueSummary[]> {
  const response = await getJson<VenueListResponse>('/ListVenues', signal)
  return response.venues.map(({ venueId, venueName, venueCity }) => ({
    id: venueId,
    name: venueName,
    city: venueCity,
  }))
}
