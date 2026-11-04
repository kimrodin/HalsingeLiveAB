import { request, sendJson } from '../../../shared/api/http';
import type {
  SaveVenueRequest,
  SectionSeatingType,
  Venue,
  VenueSection,
  VenueSectionInput,
} from '../types';

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
  streetAddress: string;
  city: string;
  postalCode: string;
  capacity: number;
  sections: SectionDto[];
}

interface VenueListItemDto {
  venueId: string;
  venueName: string;
  venueStreetAddress: string;
  venueCity: string;
  venuePostalCode: string;
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
    streetAddress: venue.streetAddress,
    city: venue.city,
    postalCode: venue.postalCode,
    capacity: venue.capacity,
    sections: venue.sections.map(sectionFromDto),
  };
}

function venueFromListItem(venue: VenueListItemDto): Venue {
  return {
    id: venue.venueId,
    name: venue.venueName,
    streetAddress: venue.venueStreetAddress,
    city: venue.venueCity,
    postalCode: venue.venuePostalCode,
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
    streetAddress: data.streetAddress,
    postalCode: data.postalCode,
    city: data.city,
    capacity: data.capacity,
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
  await sendJson('PUT', `/UpdateVenue/${venueId}`, {
    venueId,
    name: data.name,
    streetAddress: data.streetAddress,
    postalCode: data.postalCode,
    city: data.city,
    capacity: data.capacity,
    sections: data.sections.map((section) => ({
      sectionId: section.id ?? null,
      ...sectionRequestBody(section),
    })),
  });
  return getVenue(venueId);
}

export function deleteVenue(venueId: string): Promise<void> {
  return request(`/DeleteVenue/${venueId}`, { method: 'DELETE' });
}
