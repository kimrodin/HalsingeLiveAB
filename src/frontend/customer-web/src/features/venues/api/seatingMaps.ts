import { getJson } from '../../../shared/api/http'

export interface VenueSeatingMap {
  venueSeatingMapId: string
  venueId: string
  name: string
  fileName: string
  contentType: string
  downloadUrl: string
}

interface VenueSeatingMapDto extends Omit<VenueSeatingMap, 'downloadUrl'> {
  downloadUrl: string
}

function toDownloadUrl(downloadUrl: string): string {
  if (/^https?:\/\//i.test(downloadUrl) || downloadUrl.startsWith('/api/')) {
    return downloadUrl
  }

  return `/api/${downloadUrl.replace(/^\//, '')}`
}

export async function getVenueSeatingMaps(
  venueId: string,
  signal?: AbortSignal,
): Promise<VenueSeatingMap[]> {
  const maps = await getJson<VenueSeatingMapDto[]>(
    `/ListVenueSeatingMaps/${encodeURIComponent(venueId)}`,
    signal,
  )

  return maps.map((map) => ({ ...map, downloadUrl: toDownloadUrl(map.downloadUrl) }))
}
