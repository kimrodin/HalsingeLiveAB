import { createBrowserRouter } from 'react-router'
import CustomerLayout from './CustomerLayout'
import HomePage from '../features/events/pages/HomePage'
import EventDetailPage from '../features/events/pages/EventDetailPage'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <CustomerLayout />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'events/:eventId', element: <EventDetailPage /> },
    ],
  },
])
