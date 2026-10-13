import type { UploadSeatingMapRequest, VenueSeatingMap } from './types';

const mockSeatingMaps: VenueSeatingMap[] = [];
const delay = (milliseconds: number) => new Promise((resolve) => setTimeout(resolve, milliseconds));

export async function getSeatingMaps(venueId: string): Promise<VenueSeatingMap[]> {
  await delay(200);
  return mockSeatingMaps.filter((map) => map.venueId === venueId);
}

export async function uploadSeatingMap(
  venueId: string,
  data: UploadSeatingMapRequest,
): Promise<VenueSeatingMap> {
  await delay(400);
  const map: VenueSeatingMap = {
    id: crypto.randomUUID(),
    venueId,
    name: data.name,
    fileName: data.file.name,
    contentType: data.file.type,
    url: URL.createObjectURL(data.file),
  };
  mockSeatingMaps.push(map);
  return map;
}

export async function deleteSeatingMap(venueId: string, seatingMapId: string): Promise<void> {
  await delay(400);
  const index = mockSeatingMaps.findIndex(
    (map) => map.id === seatingMapId && map.venueId === venueId,
  );
  if (index === -1) throw new Error('Platskartan hittades inte');
  URL.revokeObjectURL(mockSeatingMaps[index].url);
  mockSeatingMaps.splice(index, 1);
}
