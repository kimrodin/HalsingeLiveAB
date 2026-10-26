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

export interface VenueDetail {
  venueId: string
  name: string
  city: string
  streetAddress: string
  postalCode: string
  capacity: number
  sections: Array<{
    sectionId: string
    name: string
    capacity: number
    seatingType: string
  }>
}

export async function getVenues(signal?: AbortSignal): Promise<VenueSummary[]> {
  const response = await getJson<VenueListResponse>('/ListVenues', signal)
  return response.venues.map(({ venueId, venueName, venueCity }) => ({
    id: venueId,
    name: venueName,
    city: venueCity,
  }))
}

export function getVenueById(venueId: string, signal?: AbortSignal): Promise<VenueDetail> {
  return getJson<VenueDetail>(`/GetVenue/${encodeURIComponent(venueId)}`, signal)
}
