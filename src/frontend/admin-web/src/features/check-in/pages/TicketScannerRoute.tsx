import { lazy, Suspense } from 'react';
import { CircularProgress } from '@mui/material';

const TicketScannerPage = lazy(() => import('./TicketScannerPage'));

export default function TicketScannerRoute() {
  return (
    <Suspense fallback={<CircularProgress />}>
      <TicketScannerPage />
    </Suspense>
  );
}
