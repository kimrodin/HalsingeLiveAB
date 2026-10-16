import { request } from './http';

export interface DeleteSeedDataResult {
  deletedEventCount: number;
  deletedSectionCount: number;
  deletedVenueCount: number;
}

export function seedData(): Promise<unknown> {
  return request('/seed', { method: 'POST' });
}

export function deleteSeedData(): Promise<DeleteSeedDataResult> {
  return request<DeleteSeedDataResult>('/seed', { method: 'DELETE' });
}
