import { request } from '../../../shared/api/http';

export interface DeleteSeedDataResult {
  deletedEventCount: number;
  deletedSectionCount: number;
  deletedVenueCount: number;
  deletedBookingCount: number;
  deletedTicketCount: number;
  deletedPaymentCount: number;
}

export interface SeedDataResult {
  events: {
    createdCount: number;
    events: Array<{ eventId: string; inventoryProvisioned: boolean }>;
  };
  purchases: { createdBookings: number; createdTickets: number; totalBookings: number; totalTickets: number };
}

export async function seedData(): Promise<SeedDataResult> {
  const catalog = await request<Omit<SeedDataResult, 'purchases'>>('/seed', { method: 'POST' });
  if (catalog.events.events.some(event => !event.inventoryProvisioned)) {
    throw new Error('Evenemangen sparades men biljettlagret kunde inte skapas. Kör seedningen igen för att fortsätta.');
  }
  const purchases = await request<SeedDataResult['purchases']>('/SeedPurchases', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ eventIds: catalog.events.events.map(event => event.eventId) }),
  }, '/booking-api');
  return { ...catalog, purchases };
}

export async function deleteSeedData(): Promise<DeleteSeedDataResult> {
  const purchases = await request<{ deletedBookings: number; deletedTickets: number; deletedPayments: number }>(
    '/SeedPurchases', { method: 'DELETE' }, '/booking-api');
  const catalog = await request<{ deletedEvents: number; deletedSections: number; deletedVenues: number }>(
    '/seed', { method: 'DELETE' });
  return {
    deletedEventCount: catalog.deletedEvents,
    deletedSectionCount: catalog.deletedSections,
    deletedVenueCount: catalog.deletedVenues,
    deletedBookingCount: purchases.deletedBookings,
    deletedTicketCount: purchases.deletedTickets,
    deletedPaymentCount: purchases.deletedPayments,
  };
}
