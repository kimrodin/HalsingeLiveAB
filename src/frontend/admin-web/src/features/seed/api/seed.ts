import { request } from '../../../shared/api/http';

export interface DeleteSeedDataResult {
  deletedEventCount: number;
  deletedSectionCount: number;
  deletedVenueCount: number;
}

export interface SeedDataResult {
  events: {
    createdCount: number;
  };
}

export function seedData(): Promise<SeedDataResult> {
  return request<SeedDataResult>('/seed', { method: 'POST' });
}

export function deleteSeedData(): Promise<DeleteSeedDataResult> {
  return request<DeleteSeedDataResult>('/seed', { method: 'DELETE' });
}
