import { request, sendJson } from './http'

/* ==========================================================================
 * 1. UI-MODELLER
 *    Det React-sidorna arbetar med. Ändras bara när sidorna behöver något nytt.
 * ========================================================================== */

export type SectionSeatingType = 'standing' | 'seated';

export interface VenueSection {
  id: string;
  name: string;
  seatCount: number;
  seatingType: SectionSeatingType;
}

export interface Venue {
  id: string;
  name: string;
  address: string;
  capacity: number; // summa av sektionernas platser
  sections: VenueSection[];
}

export interface VenueSectionInput {
  id?: string; // saknas på nya sektioner, finns på befintliga
  name: string;
  seatCount: number;
  seatingType: SectionSeatingType;
}

export interface SaveVenueRequest {
  name: string;
  address: string;
  sections: VenueSectionInput[];
}

export interface EventSectionPrice {
  sectionId: string;
  price: number; // kronor
}

export interface EventDetails {
  eventId: string;
  venueId: string;
  name: string;
  description: string;
  startDate: string; // ISO 8601
  endDate: string;
  onSaleFrom: string;
  sectionPrices: EventSectionPrice[];
}

export type CreateEventRequest = Omit<EventDetails, 'eventId'>;
// Backend (UpdateEventRequest) kan bara ändra dessa fält efter att eventet skapats
export type UpdateEventRequest = Pick<EventDetails, 'name' | 'description' | 'startDate' | 'endDate'>;

export interface VenueSeatingMap {
  id: string;
  venueId: string;
  name: string;
  fileName: string;
  contentType: string;
  url: string;
}

export interface UploadSeatingMapRequest {
  name: string;
  file: File;
}

/* ==========================================================================
 * 2. API-TYPER (DTO)
 *    Exakt hur JSON ser ut från backend. ASP.NET gör PascalCase till camelCase.
 *    Källa: Response-recordsen i EventCatalog/features/...
 * ========================================================================== */

// Backend skickar enum som siffra (0 = GeneralAdmission, 1 = AssignedSeating) eftersom
// JsonStringEnumConverter inte är registrerad. Typen tillåter båda om det läggs till senare.
type SeatingTypeDto = number | string;

interface SectionDto {
  sectionId: string;
  name: string;
  capacity: number;
  seatingType: SeatingTypeDto;
}

// GET /venues/{id}  (GetVenueResponse)
interface VenueDto {
  venueId: string;
  name: string;
  address: string;
  capacity: number;
  sections: SectionDto[];
}

// GET /venues  (ListVenuesResponse). Obs: andra fältnamn än GET /venues/{id}.
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

// POST /venues  (CreateVenueResponse)
interface CreateVenueResponseDto {
  venueId: string;
}

// GET /events returnerar Event-entiteten, GET /events/{id} returnerar GetEventResponse.
// sectionPrices är tom i listan eftersom handlern inte hämtar dem där.
interface EventDto {
  eventId: string;
  venueId: string;
  name: string;
  description: string | null;
  startDate: string;
  endDate: string;
  onSaleFrom: string;
  sectionPrices?: { sectionId: string; price: number }[];
}

// POST /events  (CreateEventResponse)
interface CreateEventResponseDto {
  eventId: string;
}

/* ==========================================================================
 * 3. MAPPNING DTO <-> UI-modell
 * ========================================================================== */

function seatingFromApi(value: SeatingTypeDto): SectionSeatingType {
  return value === 1 || value === 'AssignedSeating' || value === 'assignedSeating'
    ? 'seated'
    : 'standing';
}

// Siffran fungerar även om backend senare får JsonStringEnumConverter (den tar emot siffror också)
function seatingToApi(value: SectionSeatingType): number {
  return value === 'seated' ? 1 : 0;
}

function sectionFromDto(s: SectionDto): VenueSection {
  return {
    id: s.sectionId,
    name: s.name,
    seatCount: s.capacity,
    seatingType: seatingFromApi(s.seatingType),
  };
}

function venueFromDto(v: VenueDto): Venue {
  return {
    id: v.venueId,
    name: v.name,
    address: v.address,
    capacity: v.capacity,
    sections: v.sections.map(sectionFromDto),
  };
}

function venueFromListItem(v: VenueListItemDto): Venue {
  return {
    id: v.venueId,
    name: v.venueName,
    address: v.venueAddress,
    capacity: v.venueCapacity,
    sections: v.sections.map(sectionFromDto),
  };
}

function eventFromDto(e: EventDto): EventDetails {
  return {
    eventId: e.eventId,
    venueId: e.venueId,
    name: e.name,
    description: e.description ?? '',
    startDate: e.startDate,
    endDate: e.endDate,
    onSaleFrom: e.onSaleFrom,
    sectionPrices: e.sectionPrices ?? [],
  };
}

function sectionRequestBody(s: VenueSectionInput) {
  // CreateSectionRequest: Name, Capacity, SeatingType
  return { name: s.name, capacity: s.seatCount, seatingType: seatingToApi(s.seatingType) };
}

function totalSeats(sections: { seatCount: number }[]): number {
  return sections.reduce((total, s) => total + s.seatCount, 0);
}

/* ==========================================================================
 * 4. EVENEMANG
 * ========================================================================== */

// GET /events
export async function getEvents(): Promise<EventDetails[]> {
  const dtos = await request<EventDto[]>('/events');
  return dtos.map(eventFromDto);
}

// GET /events/{id}
export async function getEvent(eventId: string): Promise<EventDetails> {
  return eventFromDto(await request<EventDto>(`/events/${eventId}`));
}

// POST /events -> 201 { eventId }
export function createEvent(data: CreateEventRequest): Promise<CreateEventResponseDto> {
  return sendJson<CreateEventResponseDto>('POST', '/events', {
    name: data.name,
    description: data.description,
    venueId: data.venueId,
    startDate: data.startDate,
    endDate: data.endDate,
    onSaleFrom: data.onSaleFrom,
    sectionPrices: data.sectionPrices,
  });
}

// PUT /events/{id} -> 204
export function updateEvent(eventId: string, data: UpdateEventRequest): Promise<void> {
  return sendJson('PUT', `/events/${eventId}`, { eventId, ...data });
}

// DELETE /events/{id} -> 204
export function deleteEvent(eventId: string): Promise<void> {
  return request(`/events/${eventId}`, { method: 'DELETE' });
}

/* ==========================================================================
 * 5. SCENER OCH SEKTIONER
 * ========================================================================== */

// GET /venues -> { venues: [...] }
export async function getVenues(): Promise<Venue[]> {
  const dto = await request<ListVenuesDto>('/venues');
  return dto.venues.map(venueFromListItem);
}

// GET /venues/{id}
export async function getVenue(venueId: string): Promise<Venue> {
  return venueFromDto(await request<VenueDto>(`/venues/${venueId}`));
}

// Backend skapar scen och sektioner med separata anrop och tar emot kapaciteten som
// ett eget fält. Därför: 1) POST /venues  2) POST /venues/{id}/sections per sektion.
// Går något fel på vägen tas scenen bort igen så att inget halvfärdigt blir kvar.
export async function createVenue(data: SaveVenueRequest): Promise<Venue> {
  const created = await sendJson<CreateVenueResponseDto>('POST', '/venues', {
    name: data.name,
    address: data.address,
    capacity: totalSeats(data.sections),
  });

  try {
    for (const section of data.sections) {
      await sendJson('POST', `/venues/${created.venueId}/sections`, sectionRequestBody(section));
    }
  } catch (error) {
    await request(`/venues/${created.venueId}`, { method: 'DELETE' }).catch(() => undefined);
    throw error;
  }

  return getVenue(created.venueId);
}

// PUT /venues/{id} uppdaterar bara namn, adress och kapacitet. Sektioner hanteras så här:
//  - sektion utan id    -> POST   /venues/{id}/sections
//  - sektion som tagits bort -> DELETE /venues/{id}/sections/{sectionId}
//  - ändrade fält på befintlig sektion ignoreras (det finns ingen PUT för sektion)
// Obs: att ta bort en sektion tar även bort dess priser på evenemang (cascade i databasen).
export async function updateVenue(venueId: string, data: SaveVenueRequest): Promise<Venue> {
  const current = await getVenue(venueId);
  const keptIds = new Set(data.sections.flatMap((s) => (s.id ? [s.id] : [])));
  const toDelete = current.sections.filter((s) => !keptIds.has(s.id));
  const toUpdate = data.sections.filter((s) => {
    const old = current.sections.find((c) => c.id === s.id);
    return old && (old.name !== s.name || old.seatCount !== s.seatCount || old.seatingType !== s.seatingType);
  });
  const toAdd = data.sections.filter((s) => !s.id);

  // Scenen uppdateras först: valideringsfel (t.ex. tomt namn) är vanligast och då
  // hinner inga sektioner ändras.
  await sendJson('PUT', `/venues/${venueId}`, {
    venueId,
    name: data.name,
    address: data.address,
    capacity: totalSeats(data.sections),
  });

  for (const section of toDelete) {
    await request(`/venues/${venueId}/sections/${section.id}`, { method: 'DELETE' });
  }
  for (const section of toAdd) {
    await sendJson('POST', `/venues/${venueId}/sections`, sectionRequestBody(section));
  }
  for (const s of toUpdate) {
    await sendJson('PUT', `/venues/${venueId}/sections/${s.id}`, sectionRequestBody(s));
  }

  return getVenue(venueId);
}

// DELETE /venues/{id} -> 204. Backend ger för tillfället 500 om scenen används av evenemang.
export function deleteVenue(venueId: string): Promise<void> {
  return request(`/venues/${venueId}`, { method: 'DELETE' });
}

/* ==========================================================================
 * 6. PLATSKARTOR
 *    Det finns ännu inga endpoints för platskartor i catalog-API:t.
 *    Tills de finns ligger de kvar som mock i minnet så att UI:t fungerar.
 * ========================================================================== */

const mockSeatingMaps: VenueSeatingMap[] = [];
const delay = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

export async function getSeatingMaps(venueId: string): Promise<VenueSeatingMap[]> {
  await delay(200);
  return mockSeatingMaps.filter((m) => m.venueId === venueId);
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
  const index = mockSeatingMaps.findIndex((m) => m.id === seatingMapId && m.venueId === venueId);
  if (index === -1) throw new Error('Platskartan hittades inte');
  URL.revokeObjectURL(mockSeatingMaps[index].url);
  mockSeatingMaps.splice(index, 1);
}