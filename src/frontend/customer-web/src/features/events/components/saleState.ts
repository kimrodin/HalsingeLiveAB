import type { EventListItem } from '../api/events'

type SaleState = 'ended' | 'upcoming' | 'onSale'

export function getSaleState(event: EventListItem, now: number): SaleState {
  if (new Date(event.endDate).getTime() < now) return 'ended'
  if (new Date(event.onSaleFrom).getTime() > now) return 'upcoming'
  return 'onSale'
}
