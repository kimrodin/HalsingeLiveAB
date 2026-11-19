import { Suspense } from 'react'
import { CircularProgress } from '@mui/material'
import { createBrowserRouter } from 'react-router'
import CustomerLayout from './layouts/CustomerLayout'
import { BookingPage, EventDetailPage, HomePage, PrivacyPolicyPage } from './routePages'

export const router = createBrowserRouter([
  {
    path: '/',
    element: (
      <Suspense fallback={<CircularProgress aria-label="Laddar sida" />}>
        <CustomerLayout />
      </Suspense>
    ),
    children: [
      { index: true, element: <HomePage /> },
      { path: 'events/:eventId', element: <EventDetailPage /> },
      { path: 'bookings/:bookingId', element: <BookingPage /> },
      { path: 'integritetspolicy', element: <PrivacyPolicyPage /> },
    ],
  },
])
