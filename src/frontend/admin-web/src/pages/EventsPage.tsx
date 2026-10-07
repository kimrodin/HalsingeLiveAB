import { Link as RouterLink } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import {
  Alert, Box, Button, Chip, CircularProgress, Paper, Table, TableBody,
  TableCell, TableContainer, TableHead, TableRow, Typography,
} from '@mui/material';
import { getEvents, getVenues } from '../api/catalog';
import type { EventStatus } from '../api/catalog';

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
                <TableCell>Titel</TableCell>
                <TableCell>Scen</TableCell>
                <TableCell>Datum</TableCell>
                <TableCell>Status</TableCell>
                <TableCell />
              </TableRow>
            </TableHead>
            <TableBody>
              {data.map((event) => (
                <TableRow key={event.id} hover>
                  <TableCell>{event.title}</TableCell>
                  <TableCell>{venueName(event.venueId)}</TableCell>
                  <TableCell>
                    {new Date(event.startsAt).toLocaleString('sv-SE', {
                      dateStyle: 'medium',
                      timeStyle: 'short',
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
                    <Button component={RouterLink} to={`/events/${event.id}`}>
                      Redigera
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </>
  );
}