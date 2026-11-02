import { request } from '../../../shared/api/http';

export interface SalesSummary {
  grossRevenue: number;
  refundedRevenue: number;
  netRevenue: number;
  soldTickets: number;
  refundedTickets: number;
}

export interface SalesStatistics {
  from: string;
  to: string;
  currency: 'SEK';
  timeZone: 'Europe/Stockholm';
  summary: SalesSummary;
  previousPeriod: { from: string; to: string; summary: SalesSummary };
  changes: { netRevenuePercent: number | null; soldTicketsPercent: number | null };
  daily: Array<SalesSummary & { date: string }>;
}

// Inclusive Swedish calendar dates (YYYY-MM-DD), not datetime-local or UTC timestamps.
export function getSalesStatistics(params: { from?: string; to?: string } = {}, signal?: AbortSignal): Promise<SalesStatistics> {
  const query = new URLSearchParams();
  if (params.from) query.set('from', params.from);
  if (params.to) query.set('to', params.to);
  return request<SalesStatistics>(`/GetSalesStatistics?${query}`, { signal }, '/booking-api');
}
