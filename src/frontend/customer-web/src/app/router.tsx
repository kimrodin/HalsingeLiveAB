import { createBrowserRouter } from 'react-router'
import CustomerLayout from './layouts/CustomerLayout'
import HomePage from '../features/events/pages/HomePage'
import EventDetailPage from '../features/events/pages/EventDetailPage'
import BookingPage from '../features/bookings/pages/BookingPage'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <CustomerLayout />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'events/:eventId', element: <EventDetailPage /> },
      { path: 'bookings/:bookingId', element: <BookingPage /> },
    ],
  },
])
