export interface EventSectionPrice {
  sectionId: string;
  price: number;
}

export interface EventDetails {
  eventId: string;
  venueId: string;
  name: string;
  description: string;
  startDate: string;
  endDate: string;
  onSaleFrom: string;
  sectionPrices: EventSectionPrice[];
}

export interface CreateEventRequest {
  venueId: string;
  name: string;
  description: string;
  startDate: string;
  endDate: string;
  onSaleFrom: string;
  sectionPrices: EventSectionPrice[];
}

export type UpdateEventRequest = Pick<
  EventDetails,
  'venueId' | 'name' | 'description' | 'startDate' | 'endDate' | 'onSaleFrom' | 'sectionPrices'
>;
