export type EventStatus = 'draft' | 'published' | 'cancelled';
export type EventCategory = 'concert' | 'standup' | 'conference' | 'workshop';

export interface EventDetails {
  id: string;
  title: string;
  description?: string;
  category: EventCategory;
  venueId: string;
  startsAt: string;      // ISO 8601, UTC
  endsAt?: string;
  onSaleFrom?: string;
  status: EventStatus;
}

export type CreateEventRequest = Omit<EventDetails, 'id' | 'status'>;

export interface Venue {
  id: string;
  name: string;
}

const mockVenues: Venue[] = [
  { id: 'venue-kulturhuset', name: 'Kulturhuset (stora salen)' },
  { id: 'venue-studion', name: 'Studion' },
  { id: 'venue-festival', name: 'Sommarfestivalens område' },
];

// Byts senare mot: fetch(`${import.meta.env.VITE_CATALOG_API_URL}/events`)
const mockEvents: EventDetails[] = [
  {
    id: '1',
    title: 'Sommarkonsert',
    description: 'En kväll med lokala artister i stora salen.',
    category: 'concert',
    venueId: 'venue-kulturhuset',
    startsAt: '2026-11-14T18:00:00Z',
    endsAt: '2026-11-14T21:00:00Z',
    onSaleFrom: '2026-10-01T08:00:00Z',
    status: 'published',
  },
  {
    id: '2',
    title: 'Ståupp-kväll',
    description: 'Tre komiker, en scen.',
    category: 'standup',
    venueId: 'venue-studion',
    startsAt: '2026-11-21T19:00:00Z',
    endsAt: '2026-11-21T21:30:00Z',
    onSaleFrom: '2026-10-15T08:00:00Z',
    status: 'published',
  },
  {
    id: '3',
    title: 'Konferens: Digitala Hälsingland',
    description: 'En heldag om digitalisering i regionen.',
    category: 'conference',
    venueId: 'venue-kulturhuset',
    startsAt: '2026-12-03T08:00:00Z',
    endsAt: '2026-12-03T16:00:00Z',
    status: 'draft',
  },
  {
    id: '4',
    title: 'Sommarfestivalen 2027',
    category: 'concert',
    venueId: 'venue-festival',
    startsAt: '2027-07-09T10:00:00Z',
    onSaleFrom: '2027-03-01T08:00:00Z',
    status: 'draft',
  },
  {
    id: '5',
    title: 'Workshop: Ljudteknik',
    description: 'Praktisk introduktion till ljud för små scener.',
    category: 'workshop',
    venueId: 'venue-studion',
    startsAt: '2026-10-25T09:00:00Z',
    endsAt: '2026-10-25T15:00:00Z',
    status: 'cancelled',
  },
];

export async function getEvents(): Promise<EventDetails[]> {
  await new Promise((resolve) => setTimeout(resolve, 400));
  return mockEvents;
}

export async function getVenues(): Promise<Venue[]> {
  await new Promise((resolve) => setTimeout(resolve, 200));
  return mockVenues;
}


//CRUD för events
const delay = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

export async function getEvent(id: string): Promise<EventDetails> {
  await delay(200);
  const event = mockEvents.find((e) => e.id === id);
  if (!event) throw new Error('Evenemanget hittades inte');
  return event;
}

export async function createEvent(data: CreateEventRequest): Promise<EventDetails> {
  await delay(400);
  const event: EventDetails = { ...data, id: crypto.randomUUID(), status: 'draft' };
  mockEvents.push(event);
  return event;
}

export async function updateEvent(id: string, data: CreateEventRequest): Promise<EventDetails> {
  await delay(400);
  const index = mockEvents.findIndex((e) => e.id === id);
  if (index === -1) throw new Error('Evenemanget hittades inte');
  const updated: EventDetails = { ...mockEvents[index], ...data };
  mockEvents[index] = updated;
  return updated;
}