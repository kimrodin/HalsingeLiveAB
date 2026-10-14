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
  capacity: number;
  sections: VenueSection[];
}

export interface VenueSectionInput {
  id?: string;
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
  'name' | 'description' | 'startDate' | 'endDate' | 'onSaleFrom' | 'sectionPrices'
>;

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
