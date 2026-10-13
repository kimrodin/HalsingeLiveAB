import { request, sendJson } from './http';
import type {
  SaveVenueRequest,
  SectionSeatingType,
  Venue,
  VenueSection,
  VenueSectionInput,
} from './types';

type SeatingTypeDto = number | string;

interface SectionDto {
  sectionId: string;
  name: string;
  capacity: number;
  seatingType: SeatingTypeDto;
}

interface VenueDto {
  venueId: string;
  name: string;
  address: string;
  capacity: number;
  sections: SectionDto[];
}

interface VenueListItemDto {
  venueId: string;
  venueName: string;
  venueAddress: string;
  venueCapacity: number;
  sections: SectionDto[];
}

interface ListVenuesDto {
  venues: VenueListItemDto[];
}

interface CreateVenueResponseDto {
  venueId: string;
}

function seatingFromApi(value: SeatingTypeDto): SectionSeatingType {
  return value === 1 || value === 'AssignedSeating' || value === 'assignedSeating'
    ? 'seated'
    : 'standing';
}

function seatingToApi(value: SectionSeatingType): number {
  return value === 'seated' ? 1 : 0;
}

function sectionFromDto(section: SectionDto): VenueSection {
  return {
    id: section.sectionId,
    name: section.name,
    seatCount: section.capacity,
    seatingType: seatingFromApi(section.seatingType),
  };
}

function venueFromDto(venue: VenueDto): Venue {
  return {
    id: venue.venueId,
    name: venue.name,
    address: venue.address,
    capacity: venue.capacity,
    sections: venue.sections.map(sectionFromDto),
  };
}

function venueFromListItem(venue: VenueListItemDto): Venue {
  return {
    id: venue.venueId,
    name: venue.venueName,
    address: venue.venueAddress,
    capacity: venue.venueCapacity,
    sections: venue.sections.map(sectionFromDto),
  };
}

function sectionRequestBody(section: VenueSectionInput) {
  return {
    name: section.name,
    capacity: section.seatCount,
    seatingType: seatingToApi(section.seatingType),
  };
}

function totalSeats(sections: { seatCount: number }[]): number {
  return sections.reduce((total, section) => total + section.seatCount, 0);
}

export async function getVenues(): Promise<Venue[]> {
  const dto = await request<ListVenuesDto>('/ListVenues');
  return dto.venues.map(venueFromListItem);
}

export async function getVenue(venueId: string): Promise<Venue> {
  return venueFromDto(await request<VenueDto>(`/GetVenue/${venueId}`));
}

export async function createVenue(data: SaveVenueRequest): Promise<Venue> {
  const created = await sendJson<CreateVenueResponseDto>('POST', '/CreateVenue', {
    name: data.name,
    address: data.address,
    capacity: totalSeats(data.sections),
  });

  try {
    for (const section of data.sections) {
      await sendJson('POST', `/CreateSection/${created.venueId}`, sectionRequestBody(section));
    }
  } catch (error) {
    await request(`/DeleteVenue/${created.venueId}`, { method: 'DELETE' }).catch(() => undefined);
    throw error;
  }

  return getVenue(created.venueId);
}

export async function updateVenue(venueId: string, data: SaveVenueRequest): Promise<Venue> {
  const current = await getVenue(venueId);
  const keptIds = new Set(data.sections.flatMap((section) => (section.id ? [section.id] : [])));
  const toDelete = current.sections.filter((section) => !keptIds.has(section.id));
  const toUpdate = data.sections.filter((section) => {
    const old = current.sections.find((currentSection) => currentSection.id === section.id);
    return old && (
      old.name !== section.name
      || old.seatCount !== section.seatCount
      || old.seatingType !== section.seatingType
    );
  });
  const toAdd = data.sections.filter((section) => !section.id);

  await sendJson('PUT', `/UpdateVenue/${venueId}`, {
    venueId,
    name: data.name,
    address: data.address,
    capacity: totalSeats(data.sections),
  });

  for (const section of toDelete) {
    await request(`/DeleteSection/${venueId}/${section.id}`, { method: 'DELETE' });
  }
  for (const section of toAdd) {
    await sendJson('POST', `/CreateSection/${venueId}`, sectionRequestBody(section));
  }
  for (const section of toUpdate) {
    await sendJson(
      'PUT',
      `/UpdateSection/${venueId}/${section.id}`,
      sectionRequestBody(section),
    );
  }

  return getVenue(venueId);
}

export function deleteVenue(venueId: string): Promise<void> {
  return request(`/DeleteVenue/${venueId}`, { method: 'DELETE' });
}
