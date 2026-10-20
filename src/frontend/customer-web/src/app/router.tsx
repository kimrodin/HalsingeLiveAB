import { createBrowserRouter } from 'react-router'
import CustomerLayout from './CustomerLayout'
import HomePage from '../features/events/pages/HomePage'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <CustomerLayout />,
    children: [{ index: true, element: <HomePage /> }],
  },
])
