import type { UploadSeatingMapRequest, VenueSeatingMap } from './types';
import { BASE_URL, request } from './http';

interface VenueSeatingMapDto {
  venueSeatingMapId: string;
  venueId: string;
  name: string;
  fileName: string;
  contentType: string;
  downloadUrl: string;
}

function venueSeatingMapFromDto(dto: VenueSeatingMapDto): VenueSeatingMap {
  const downloadUrl = /^https?:\/\//i.test(dto.downloadUrl)
    ? dto.downloadUrl
    : `${BASE_URL.replace(/\/$/, '')}/${dto.downloadUrl.replace(/^\//, '')}`;

  return {
    venueSeatingMapId: dto.venueSeatingMapId,
    venueId: dto.venueId,
    name: dto.name,
    fileName: dto.fileName,
    contentType: dto.contentType,
    downloadUrl,
  };
}

export async function getSeatingMaps(
  venueId: string,
): Promise<VenueSeatingMap[]> {
  const data = await request<VenueSeatingMapDto[]>(
    `/ListVenueSeatingMaps/${venueId}`,
  );
  return data.map(venueSeatingMapFromDto);
}

export async function uploadSeatingMap(
  venueId: string,
  data: UploadSeatingMapRequest,
): Promise<VenueSeatingMap> {
  const formData = new FormData();
  formData.append('Name', data.name);
  formData.append('File', data.file);

  const result = await request<VenueSeatingMapDto>(
    `/UploadVenueSeatingMap/${venueId}`,
    {
      method: 'POST',
      body: formData,
    },
  );
  return venueSeatingMapFromDto(result);
}

export async function deleteSeatingMap(
  venueId: string,
  seatingMapId: string,
): Promise<void> {
  await request(
    `/DeleteVenueSeatingMap/${venueId}/${seatingMapId}`,
    { method: 'DELETE' },
  );
}