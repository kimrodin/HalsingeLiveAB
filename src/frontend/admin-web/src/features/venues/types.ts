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
  streetAddress: string;
  city: string;
  postalCode: string;
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
  streetAddress: string;
  postalCode: string;
  city: string;
  capacity: number;
  sections: VenueSectionInput[];
}
