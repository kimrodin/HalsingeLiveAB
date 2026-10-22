export interface VenueSeatingMap {
  venueSeatingMapId: string;
  venueId: string;
  name: string;
  fileName: string;
  contentType: string;
  downloadUrl: string;
}

export interface UploadSeatingMapRequest {
  name: string;
  file: File;
}
