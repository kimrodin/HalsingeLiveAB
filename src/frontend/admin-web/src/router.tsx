import { createBrowserRouter, Navigate } from 'react-router';
import AdminLayout from './layouts/AdminLayout';
import EventsPage from './pages/EventsPage';
import EventFormPage from './pages/EventFormPage';
import VenuesPage from './pages/VenuesPage';
import NotFoundPage from './pages/NotFoundPage';

export const router = createBrowserRouter([
  {
    path: '/',
    element: <AdminLayout />,
    children: [
      { index: true, element: <Navigate to="/events" replace /> },
      { path: 'events', element: <EventsPage /> },
      { path: 'events/new', element: <EventFormPage /> },
      { path: 'events/:id', element: <EventFormPage /> },
      { path: 'venues', element: <VenuesPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);