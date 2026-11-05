import { Suspense } from 'react';
import { CircularProgress } from '@mui/material';
import { createBrowserRouter, Navigate } from 'react-router';
import AdminLayout from './layouts/AdminLayout';

import {
  BookingDetailsPage,
  BookingListPage,
  DashboardPage,
  EventFormPage,
  EventListPage,
  NotFoundPage,
  TicketScannerRoute,
  VenueFormPage,
  VenueListPage,
} from './routePages';

export const router = createBrowserRouter([
  {
    path: '/',
    element: (
      <Suspense fallback={<CircularProgress aria-label="Laddar sida" />}>
        <AdminLayout />
      </Suspense>
    ),
    children: [
      { index: true, element: <Navigate to="/dashboard" replace /> },
      { path: 'dashboard', element: <DashboardPage /> },
      { path: 'events', element: <EventListPage /> },
      { path: 'events/new', element: <EventFormPage /> },
      { path: 'events/:id', element: <EventFormPage /> },
      { path: 'venues', element: <VenueListPage /> },
      { path: 'venues/new', element: <VenueFormPage /> },
      { path: 'venues/:id', element: <VenueFormPage /> },
      { path: 'bookings', element: <BookingListPage /> },
      { path: 'bookings/:id', element: <BookingDetailsPage /> },
      { path: 'ticket-scanner', element: <TicketScannerRoute /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
