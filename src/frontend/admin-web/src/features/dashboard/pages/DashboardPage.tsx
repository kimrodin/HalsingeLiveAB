import {
  Alert, Box, Button, CircularProgress, Link, Paper, Table, TableBody,
  TableCell, TableContainer, TableHead, TableRow, Typography,
} from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { Link as RouterLink } from 'react-router';
import { getEvents } from '../../events/api/events';
import { getVenues } from '../../venues/api/venues';

const dateFormatter = new Intl.DateTimeFormat('sv-SE', {
  dateStyle: 'medium', timeStyle: 'short', timeZone: 'Europe/Stockholm',
});

export default function DashboardPage() {
  const active = useQuery({
    queryKey: ['events', 'dashboard', 'active'],
    queryFn: () => getEvents({ endsAfter: new Date().toISOString(), pageSize: 1 }),
    refetchInterval: 60_000,
  });
  const upcoming = useQuery({
    queryKey: ['events', 'dashboard', 'upcoming'],
    queryFn: () => getEvents({ startAfter: new Date().toISOString(), pageSize: 5 }),
    refetchInterval: 60_000,
  });
  const venues = useQuery({ queryKey: ['venues'], queryFn: getVenues });

  return (
    <>
      <Typography component="h1" variant="h4" sx={{ mb: 1 }}>Dashboard</Typography>
      <Typography color="text.secondary" sx={{ mb: 3 }}>
        Översikt över evenemang på Hälsinge Live.
      </Typography>

      <Paper sx={{ p: 3, maxWidth: { sm: 420 }, mb: 3 }}>
        <Typography component="h2" variant="h6">Aktiva evenemang</Typography>
        <Box aria-live="polite" sx={{ my: 1 }}>
          {active.isError ? (
            <Alert severity="error" action={<Button color="inherit" onClick={() => void active.refetch()}>Försök igen</Button>}>
              Kunde inte hämta aktiva evenemang.
            </Alert>
          ) : active.isPending ? (
            <CircularProgress size={28} aria-label="Hämtar antal aktiva evenemang" />
          ) : (
            <Typography variant="h3" component="p" sx={{ color: 'primary.light' }}>
              {active.data.totalCount.toLocaleString('sv-SE')}
            </Typography>
          )}
        </Box>
        <Typography variant="body2" color="text.secondary">Kommande och pågående evenemang.</Typography>
      </Paper>

      <Paper sx={{ overflow: 'hidden' }}>
        <Box sx={{ p: 2.5, display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 2 }}>
          <Typography component="h2" variant="h6">Kommande evenemang</Typography>
          <Button component={RouterLink} to="/events">Visa alla</Button>
        </Box>
        {upcoming.isError ? (
          <Alert severity="error" sx={{ m: 2 }} action={<Button color="inherit" onClick={() => void upcoming.refetch()}>Försök igen</Button>}>
            Kunde inte hämta kommande evenemang.
          </Alert>
        ) : upcoming.isPending ? (
          <Box sx={{ p: 3 }}><CircularProgress size={28} aria-label="Hämtar kommande evenemang" /></Box>
        ) : upcoming.data.events.length === 0 ? (
          <Typography color="text.secondary" sx={{ px: 3, pb: 3 }}>Det finns inga kommande evenemang.</Typography>
        ) : (
          <>
            {venues.isError && <Alert severity="warning" sx={{ mx: 2, mb: 2 }} action={<Button color="inherit" onClick={() => void venues.refetch()}>Försök igen</Button>}>Scennamnen kunde inte hämtas.</Alert>}
            <TableContainer>
              <Table aria-label="Nästa fem kommande evenemang">
                <TableHead><TableRow>
                  <TableCell>Datum och tid</TableCell><TableCell>Evenemang</TableCell><TableCell>Scen</TableCell>
                </TableRow></TableHead>
                <TableBody>
                  {upcoming.data.events.map((event) => (
                    <TableRow key={event.eventId} hover>
                      <TableCell sx={{ whiteSpace: 'nowrap' }}>{dateFormatter.format(new Date(event.startDate))}</TableCell>
                      <TableCell><Box sx={{ display: 'flex', gap: 1.5, alignItems: 'center' }}>
                        {event.imageUrl && <Box component="img" src={event.imageUrl} alt="" loading="lazy"
                          onError={(e) => { e.currentTarget.style.display = 'none'; }}
                          sx={{ width: 40, height: 52, objectFit: 'cover', borderRadius: 1 }} />}
                        <Link component={RouterLink} to={`/events/${event.eventId}`}>{event.name}</Link>
                      </Box></TableCell>
                      <TableCell>{venues.isPending ? 'Hämtar scen…' : venues.data?.find((venue) => venue.id === event.venueId)?.name ?? 'Scen saknas'}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </>
        )}
      </Paper>
    </>
  );
}
