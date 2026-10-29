import { createBrowserRouter, Navigate } from 'react-router';
import AdminLayout from './layouts/AdminLayout';
import EventListPage from '../features/events/pages/EventListPage';
import EventFormPage from '../features/events/pages/EventFormPage';
import VenueListPage from '../features/venues/pages/VenueListPage';
import NotFoundPage from './pages/NotFoundPage';
import VenueFormPage from '../features/venues/pages/VenueFormPage';
import BookingListPage from '../features/bookings/pages/BookingListPage';
import BookingDetailsPage from '../features/bookings/pages/BookingDetailsPage';

export const router = createBrowserRouter([
  {
    path: '/',
    element: <AdminLayout />,
    children: [
      { index: true, element: <Navigate to="/events" replace /> },
      { path: 'events', element: <EventListPage /> },
      { path: 'events/new', element: <EventFormPage /> },
      { path: 'events/:id', element: <EventFormPage /> },
      { path: 'venues', element: <VenueListPage /> },
      { path: 'venues/new', element: <VenueFormPage /> },
      { path: 'venues/:id', element: <VenueFormPage /> },
      { path: 'bookings', element: <BookingListPage /> },
      { path: 'bookings/:id', element: <BookingDetailsPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
