import { lazy } from 'react';

export const EventListPage = lazy(() => import('../features/events/pages/EventListPage'));
export const EventFormPage = lazy(() => import('../features/events/pages/EventFormPage'));
export const VenueListPage = lazy(() => import('../features/venues/pages/VenueListPage'));
export const NotFoundPage = lazy(() => import('./pages/NotFoundPage'));
export const VenueFormPage = lazy(() => import('../features/venues/pages/VenueFormPage'));
export const BookingListPage = lazy(() => import('../features/bookings/pages/BookingListPage'));
export const DashboardPage = lazy(() => import('../features/dashboard/pages/DashboardPage'));
export const BookingDetailsPage = lazy(() => import('../features/bookings/pages/BookingDetailsPage'));
export const TicketScannerRoute = lazy(() => import('../features/check-in/pages/TicketScannerRoute'));
