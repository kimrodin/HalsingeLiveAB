import { Alert, Button, Paper, Stack } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link as RouterLink } from 'react-router';
import { getEventInventory, hasCompleteEventInventory, retryEventInventory } from '../api/events';
import { ApiError } from '../../../shared/api/http';
import type { EventDetails } from '../types';

export default function EventInventoryStatus({ event, initialFailure }: {
  event: EventDetails;
  initialFailure: boolean;
}) {
  const queryClient = useQueryClient();
  const queryKey = ['event-inventory', event.eventId];
  const inventory = useQuery({
    queryKey,
    queryFn: () => getEventInventory(event.eventId),
    retry: false,
  });
  const retry = useMutation({
    mutationFn: () => retryEventInventory(event.eventId),
    onSuccess: rows => {
      queryClient.setQueryData(queryKey, rows);
    },
  });

  if (inventory.data && hasCompleteEventInventory(event, inventory.data)) {
    return initialFailure || retry.isSuccess
      ? <Alert severity="success" sx={{ mb: 2 }}>Evenemanget är sparat och biljettlagret är klart.</Alert>
      : null;
  }

  if (inventory.isPending && !initialFailure) {
    return <Alert severity="info" sx={{ mb: 2 }}>Kontrollerar biljettlagret…</Alert>;
  }

  const authorizationError = retry.error instanceof ApiError && [401, 403].includes(retry.error.status);
  return (
    <Paper sx={{ p: 2, mb: 2 }}>
      <Stack spacing={2}>
        <Alert severity="warning">
          {inventory.isError && !initialFailure
            ? 'Evenemanget är sparat, men biljettlagrets status kunde inte hämtas.'
            : 'Evenemanget är sparat, men biljettlagret kunde inte skapas. Evenemanget är inte klart för biljettförsäljning.'}
        </Alert>
        {retry.isError && (
          <Alert severity="error">
            Kunde inte skapa biljettlagret. {authorizationError
              ? 'Kontrollera att du är inloggad som administratör och försök igen.'
              : retry.error.message}
          </Alert>
        )}
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
          <Button variant="contained" onClick={() => retry.mutate()}
            disabled={inventory.isFetching || retry.isPending}>
            {retry.isPending ? 'Skapar biljettlager…' : 'Försök skapa biljettlagret igen'}
          </Button>
          <Button component={RouterLink} to="/events">Till evenemangslistan</Button>
        </Stack>
      </Stack>
    </Paper>
  );
}
