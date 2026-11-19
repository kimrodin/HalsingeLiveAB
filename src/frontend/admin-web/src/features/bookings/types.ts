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
  personalDataPseudonymizedAtUtc: string | null;
  expiresAt: string | null;
  cancelledAt: string | null;
  paymentRequested: boolean;
  paymentSettled: boolean;
  items: BookingItem[];
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
  eventId: string;
  eventName: string;
  status: string;
  issuedAt: string;
}

export interface TicketStatusChange {
  ticketId: string;
  status: string;
  changedAt: string;
}

export interface BookingDetails {
  booking: Booking;
  history: BookingHistoryItem[];
  payment: BookingPayment | null;
}

export interface BookingHistoryItem {
  bookingId: string;
  status: string;
  totalPrice: number;
  createdAt: string;
}

export interface PersonalDataSummary {
  bookingId: string;
  customerId: string | null;
  customerName: string;
  customerEmail: string;
  pseudonymizedCustomerId: string | null;
  status: string;
  totalPrice: number;
  createdAt: string;
  pseudonymizedAtUtc: string | null;
  items: Array<{
    bookingItemId: string;
    eventName: string;
    eventStartsAt: string;
    venueName: string;
    sectionName: string;
    quantity: number;
    unitPrice: number;
    currency: string;
  }>;
  payment: BookingPayment | null;
  paymentResultOutbox: Array<{
    messageId: string;
    paymentId: string;
    bookingId: string;
    amount: number;
    currency: string;
    outcome: string;
    paidAtUtc: string | null;
    occurredAtUtc: string;
    deliveredAtUtc: string | null;
    attemptCount: number;
  }>;
  tickets: Array<{
    ticketId: string;
    bookingId: string;
    bookingItemId: string;
    ticketNumberInBookingItem: number;
    eventId: string;
    eventName: string;
    eventStartsAt: string;
    venueName: string;
    sectionName: string;
    unitPrice: number;
    currency: string;
    status: string;
    issuedAt: string;
  }>;
  processingRecords: Array<{
    messageId: string;
    type: string;
    state: string;
    createdAtUtc: string;
    attemptCount: number;
  }>;
  manualReviewSources: string[];
}
