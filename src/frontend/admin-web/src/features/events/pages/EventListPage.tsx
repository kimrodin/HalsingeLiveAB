import { Link as RouterLink } from 'react-router';
import {
  Alert, Box, Button, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Pagination,
  Paper, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography,
} from '@mui/material';
import { getEvents, deleteEvent } from '../api/events';
import { getVenues } from '../../venues/api/venues';
import type { EventDetails } from '../types';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';

const formatDate = (iso: string) =>
  new Date(iso).toLocaleString('sv-SE', { dateStyle: 'medium', timeStyle: 'short' });

export default function EventsPage() {
  const queryClient = useQueryClient();

  const [eventToDelete, setEventToDelete] = useState<EventDetails | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [eventName, setEventName] = useState('');
  const [venueId, setVenueId] = useState('');
  const [endsAfter, setEndsAfter] = useState('');
  const [startsBefore, setStartsBefore] = useState('');
  const [page, setPage] = useState(1);
  const pageSize = 10;
  const isDateRangeInvalid = Boolean(endsAfter && startsBefore
    && new Date(endsAfter).getTime() > new Date(startsBefore).getTime());

  const mutation = useMutation({
    mutationFn: deleteEvent,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['events'] });
      setDialogOpen(false);
    },
  });

  const { data, isLoading, isError } = useQuery({
    queryKey: ['events', { eventName, venueId, endsAfter, startsBefore, page, pageSize }],
    enabled: !isDateRangeInvalid,
    queryFn: () => getEvents({
      eventName: eventName.trim() || undefined,
      venueId: venueId || undefined,
      // Perioden ska även omfatta evenemang som redan har börjat.
      endsAfter: endsAfter || undefined,
      startsBefore: startsBefore || undefined,
      page,
      pageSize,
    }),
  });

  const { data: venues } = useQuery({
    queryKey: ['venues'],
    queryFn: getVenues,
  });

  const venueName = (id: string) =>
    venues?.find((v) => v.id === id)?.name ?? '…';

  return (
    <>
      <Box sx={{ display: 'flex', flexDirection: { xs: 'column', sm: 'row' }, alignItems: { xs: 'stretch', sm: 'center' }, gap: 1, justifyContent: 'space-between', mb: 2 }}>
        <Typography variant="h4">Evenemang</Typography>
        <Button variant="contained" component={RouterLink} to="/events/new" sx={{ alignSelf: { xs: 'flex-start', sm: 'auto' } }}>
          Nytt evenemang
        </Button>
      </Box>

      {isLoading && <CircularProgress />}
      {!isDateRangeInvalid && isError && <Alert severity="error">Kunde inte hämta evenemang.</Alert>}

      <Paper sx={{ p: 2, mb: 2, overflow: 'visible' }}>
        <Box
          sx={{
            display: 'flex',
            flexDirection: 'row',
            gap: 2,
            alignItems: 'center',
            overflowX: 'auto',
            py: 1,
            '& > *': {
              flexShrink: 0,
            },
          }}
        >
          <TextField
            label="Sök evenemang"
            value={eventName}
            sx={{ flex: '2 1 260px', minWidth: 220 }}
            onChange={(event) => {
              setEventName(event.target.value);
              setPage(1);
            }}
          />

          <TextField
            select
            label="Scen"
            value={venueId}
            sx={{ flex: '1 0 180px', minWidth: 180 }}
            onChange={(event) => {
              setVenueId(event.target.value);
              setPage(1);
            }}
          >
            <MenuItem value="">Alla scener</MenuItem>
            {venues?.map((venue) => (
              <MenuItem key={venue.id} value={venue.id}>
                {venue.name}
              </MenuItem>
            ))}
          </TextField>

          <TextField
            label="Från"
            type="datetime-local"
            value={endsAfter}
            error={isDateRangeInvalid}
            sx={{
              width: 225,
              '& .MuiInputBase-root': { minHeight: 56 },
            }}
            onChange={(event) => {
              setEndsAfter(event.target.value);
              setPage(1);
            }}
            slotProps={{ inputLabel: { shrink: true } }}
          />

          <TextField
            label="Till"
            type="datetime-local"
            value={startsBefore}
            error={isDateRangeInvalid}
            helperText={isDateRangeInvalid ? 'Till måste vara samma tid som eller senare än Från.' : undefined}
            sx={{
              width: 225,
              '& .MuiInputBase-root': { minHeight: 56 },
            }}
            onChange={(event) => {
              setStartsBefore(event.target.value);
              setPage(1);
            }}
            slotProps={{ inputLabel: { shrink: true } }}
          />

          <Button
            sx={{ whiteSpace: 'nowrap' }}
            onClick={() => {
              setEventName('');
              setVenueId('');
              setEndsAfter('');
              setStartsBefore('');
              setPage(1);
            }}
          >
            Rensa
          </Button>
        </Box>
      </Paper>

      {!isDateRangeInvalid && data && data.totalCount === 0 && (
        <Alert severity="info">Det finns inga evenemang ännu.</Alert>
      )}

      {!isDateRangeInvalid && data && data.events.length > 0 && (
        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Namn</TableCell>
                <TableCell>Scen</TableCell>
                <TableCell>Start</TableCell>
                <TableCell>Biljettsläpp</TableCell>
                <TableCell />
              </TableRow>
            </TableHead>
            <TableBody>
              {data.events.map((event) => (
                <TableRow key={event.eventId} hover>
                  <TableCell>{event.name}</TableCell>
                  <TableCell>{venueName(event.venueId)}</TableCell>
                  <TableCell>{formatDate(event.startDate)}</TableCell>
                  <TableCell>{formatDate(event.onSaleFrom)}</TableCell>
                  <TableCell align="right">
                    <Button component={RouterLink} to={`/events/${event.eventId}`}>
                      Redigera
                    </Button>
                    <Button
                      color="error"
                      onClick={() => {
                        mutation.reset();
                        setEventToDelete(event);
                        setDialogOpen(true);
                      }}
                    >
                      Radera
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {!isDateRangeInvalid && data && data.totalCount > pageSize && (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 2 }}>
          <Pagination count={Math.ceil(data.totalCount / pageSize)} page={page}
            onChange={(_, nextPage) => setPage(nextPage)} color="primary" />
        </Box>
      )}

      <Dialog
        open={dialogOpen}
        onClose={() => {
          if (!mutation.isPending) setDialogOpen(false);
        }}
      >
        <DialogTitle>Radera evenemang?</DialogTitle>

        <DialogContent>
          Är du säker på att du vill radera "{eventToDelete?.name}"?

          {mutation.isError && (
            <Alert severity="error" sx={{ mt: 2 }}>
              Kunde inte radera evenemanget. {mutation.error.message}
            </Alert>
          )}
        </DialogContent>

        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} disabled={mutation.isPending}>
            Avbryt
          </Button>

          <Button
            color="error"
            variant="contained"
            disabled={mutation.isPending}
            onClick={() => {
              if (eventToDelete) mutation.mutate(eventToDelete.eventId);
            }}
          >
            Radera
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
