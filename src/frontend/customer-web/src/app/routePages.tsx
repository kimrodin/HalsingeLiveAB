import { lazy } from 'react'

export const HomePage = lazy(() => import('../features/events/pages/HomePage'))
export const EventDetailPage = lazy(() => import('../features/events/pages/EventDetailPage'))
export const BookingPage = lazy(() => import('../features/bookings/pages/BookingPage'))
