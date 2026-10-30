export interface BookingItem {
  inventoryId: string;
  quantity: number;
  unitPrice: number;
  currency: string;
  eventName: string;
  eventStartsAt: string;
  venueName: string;
  sectionName: string;
}

export interface Booking {
  bookingId: string;
  customerName: string;
  customerEmail: string;
  status: string;
  totalPrice: number;
  createdAt: string;
  expiresAt: string | null;
  cancelledAt: string | null;
  paymentRequested: boolean;
  paymentSettled: boolean;
  items: BookingItem[];
}

export interface BookingHistoryItem {
  bookingId: string;
  status: string;
  totalPrice: number;
  createdAt: string;
}

export interface BookingPayment {
  status: string;
  createdAt: string;
  completedAt: string | null;
  paidAt: string | null;
  refundedAt: string | null;
}

export interface BookingTicket {
  ticketId: string;
  eventName: string;
  status: string;
  issuedAt: string;
}

export interface BookingDetails {
  booking: Booking;
  history: BookingHistoryItem[];
  payment: BookingPayment | null;
  tickets: BookingTicket[];
}
