import { Link as RouterLink } from 'react-router';
import {
  Alert, Box, Button, Chip, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, Paper, Table, TableBody,
  TableCell, TableContainer, TableHead, TableRow, Typography,
} from '@mui/material';
import { getEvents, getVenues, deleteEvent } from '../api/catalog';
import type { EventStatus, EventDetails } from '../api/catalog';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';

const statusLabel: Record<EventStatus, string> = {
  draft: 'Utkast',
  published: 'Publicerad',
  cancelled: 'Inställd',
};

const statusColor: Record<EventStatus, 'default' | 'success' | 'error'> = {
  draft: 'default',
  published: 'success',
  cancelled: 'error',
};

export default function EventsPage() {
  const queryClient = useQueryClient();

  const [eventToDelete, setEventToDelete] = useState<EventDetails | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);

  const mutation = useMutation({
    mutationFn: deleteEvent,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['events'] });
      setDialogOpen(false);
    }
  });
  const { data, isPending, isError } = useQuery({
    queryKey: ['events'],
    queryFn: getEvents,
  });

  const { data: venues } = useQuery({
    queryKey: ['venues'],
    queryFn: getVenues,
  });

  const venueName = (id: string) =>
    venues?.find((v) => v.id === id)?.name ?? '…';

  return (
    <>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2 }}>
        <Typography variant="h4">Evenemang</Typography>
        <Button variant="contained" component={RouterLink} to="/events/new">
          Nytt evenemang
        </Button>
      </Box>

      {isPending && <CircularProgress />}
      {isError && <Alert severity="error">Kunde inte hämta evenemang.</Alert>}

      {data && (
        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Namn</TableCell>
                <TableCell>Scen</TableCell>
                <TableCell>Datum</TableCell>
                <TableCell>Pris</TableCell>
                <TableCell>Status</TableCell>
                <TableCell />
              </TableRow>
            </TableHead>
            <TableBody>
              {data.map((event) => (
                <TableRow key={event.eventId} hover>
                  <TableCell>{event.name}</TableCell>
                  <TableCell>{venueName(event.venueId)}</TableCell>
                  <TableCell>
                    {new Date(event.startDate).toLocaleString('sv-SE', {
                      dateStyle: 'medium',
                      timeStyle: 'short',
                    })}
                  </TableCell>
                  <TableCell>
                    {event.price.toLocaleString('sv-SE', {
                      style: 'currency',
                      currency: 'SEK',
                    })}
                  </TableCell>
                  <TableCell>
                    <Chip
                      size="small"
                      label={statusLabel[event.status]}
                      color={statusColor[event.status]}
                    />
                  </TableCell>
                  <TableCell align="right">
                    <Button component={RouterLink} to={`/events/${event.eventId}`}>
                      Redigera
                    </Button>
                    <Button onClick={() => {
                      mutation.reset();
                      setEventToDelete(event);
                      setDialogOpen(true);
                    }} color="error">
                      Radera
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
      <Dialog
        open={dialogOpen}
        onClose={() => {
          if (!mutation.isPending) { setDialogOpen(false) }
        }}
      >
        <DialogTitle>Radera evenemang?</DialogTitle>

        <DialogContent>Är du säker på att du vill radera "{eventToDelete?.name}"?

          {mutation.isError && (
            <Alert severity="error" sx={{ mt: 2 }}>
              Kunde inte radera evenemanget.
            </Alert>
          )}
        </DialogContent>

        <DialogActions>
          <Button onClick={() => {
            setDialogOpen(false);
          }}
            disabled={mutation.isPending}
          >
            Avbryt
          </Button>

          <Button
            color="error"
            variant="contained"
            disabled={mutation.isPending}
            onClick={() => {
              if (eventToDelete) {
                mutation.mutate(eventToDelete.eventId);
              }
            }}
          >
            Radera
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}