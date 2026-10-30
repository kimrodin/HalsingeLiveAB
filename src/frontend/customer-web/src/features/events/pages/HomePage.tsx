import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  Alert,
  Button,
  Skeleton,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { getAllEvents } from '../api/events'
import { getVenues } from '../../venues/api/venues'
import { useNow } from '../../../shared/hooks/useNow'
import EventCarousel from '../components/EventCarousel'
import EventCard from '../components/EventCard'
import bannerImage from '../../../assets/banner.png'

export default function HomePage() {
  const now = useNow()
  const [searchTerm, setSearchTerm] = useState('')
  const eventsQuery = useQuery({
    queryKey: ['events', 'all'],
    queryFn: ({ signal }) => getAllEvents(signal),
  })
  const venuesQuery = useQuery({
    queryKey: ['venues'],
    queryFn: ({ signal }) => getVenues(signal),
  })

  const venuesById = useMemo(
    () => new Map(venuesQuery.data?.map((venue) => [venue.id, `${venue.name}, ${venue.city}`]) ?? []),
    [venuesQuery.data],
  )

  // Sidan väntar bara på evenemangen. Scennamn är en bonus och fylls i när de har hämtats,
  // så att ett fel på scenerna inte döljer hela listan.
  if (eventsQuery.isPending) {
    return (
      <Stack spacing={2} role="status" aria-busy="true" aria-label="Hämtar evenemang">
        <Typography variant="h3" component="h1" sx={{ mb: 1, fontWeight: 700 }}>
          Evenemang i Hälsingland
        </Typography>
        {[1, 2, 3].map((item) => <Skeleton key={item} variant="rounded" height={150} />)}
      </Stack>
    )
  }

  if (eventsQuery.isError) {
    return (
      <Stack spacing={2}>
        <Typography variant="h3" component="h1" sx={{ fontWeight: 700 }}>
          Evenemang i Hälsingland
        </Typography>
        <Alert
          severity="error"
          action={<Button color="inherit" size="small" onClick={() => void eventsQuery.refetch()}>Försök igen</Button>}
        >
          Evenemangen kunde inte hämtas just nu. Kontrollera anslutningen och försök igen.
        </Alert>
      </Stack>
    )
  }

  const events = eventsQuery.data
  const featuredEvents = events
    .filter((event) => new Date(event.endDate).getTime() >= now)
    .slice(0, 3)
  const normalizedSearchTerm = searchTerm.trim().toLocaleLowerCase('sv-SE')
  const filteredEvents = events.filter((event) =>
    event.eventName.toLocaleLowerCase('sv-SE').includes(normalizedSearchTerm),
  )

  return (
    <Stack spacing={3} useFlexGap>
      <Stack
        spacing={3}
        sx={{
          position: 'relative',
          isolation: 'isolate',
          mt: { xs: -3, sm: -5 },
          pt: { xs: 3, sm: 5 },
          pb: 3,
          '&::before': {
            content: '""',
            position: 'absolute',
            zIndex: -1,
            top: 'calc(-1 * var(--customer-header-height, 77px))',
            bottom: 0,
            left: 'calc(50% - 50vw)',
            width: '100vw',
            backgroundColor: '#062b3c',
            backgroundImage: `url("${bannerImage}")`,
            // Beskär bort mer himmel och lyft fram landskapet längst ned.
            backgroundSize: 'cover',
            backgroundPosition: 'center bottom',
            backgroundRepeat: 'no-repeat',
          },
        }}
      >
        <EventCarousel events={featuredEvents} venuesById={venuesById} now={now} />

        <TextField
          label="Sök evenemang"
          value={searchTerm}
          onChange={(event) => setSearchTerm(event.target.value)}
          fullWidth
          sx={{ bgcolor: 'background.paper', borderRadius: 1 }}
        />
      </Stack>

      {filteredEvents.length === 0 ? (
        <Alert severity="info">
          {events.length === 0
            ? 'Det finns inga evenemang att visa just nu.'
            : 'Inga evenemang matchar din sökning.'}
        </Alert>
      ) : (
        <>
          <Typography variant="body2" color="text.secondary" role="status">
            {filteredEvents.length} evenemang
          </Typography>
          <Stack spacing={2}>
            {filteredEvents.map((event) => (
              <EventCard key={event.eventId} event={event} venueName={venuesById.get(event.venueId)} now={now} />
            ))}
          </Stack>
        </>
      )}
    </Stack>
  )
}
