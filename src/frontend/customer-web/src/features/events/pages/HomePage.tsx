import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Skeleton,
  Stack,
  Typography,
} from '@mui/material'
import { getAllEvents, type EventListItem } from '../api/events'
import { getVenues } from '../../venues/api/venues'
import { useNow } from '../../../hooks/useNow'
import DateBadge from '../../../components/DateBadge'

const priceFormatter = new Intl.NumberFormat('sv-SE', {
  style: 'currency',
  currency: 'SEK',
  maximumFractionDigits: 2,
})



function EventCard({ event, venueName, now }: { event: EventListItem; venueName?: string; now: number }) {
  const lowestPrice = event.sectionPrices.length > 0
    ? Math.min(...event.sectionPrices.map(({ price }) => price))
    : null
  const hasEnded = new Date(event.endDate).getTime() < now

  return (
    <Card variant="outlined" sx={{ bgcolor: 'background.paper', borderColor: 'divider', borderRadius: 2.5 }}>
      <CardContent sx={{ p: { xs: 2, sm: 2.5 }, '&:last-child': { pb: { xs: 2, sm: 2.5 } } }}>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2.5}>
          <Box
            sx={{
              flexShrink: 0,
              width: { xs: 'fit-content', sm: 112 },
              minWidth: { xs: 120, sm: 112 },
              px: 1.5,
              py: 1.25,
              borderRadius: 2,
              bgcolor: 'secondary.light',
              color: 'secondary.contrastText',
              textAlign: { xs: 'left', sm: 'center' },
            }}
          >
            <DateBadge startDate={event.startDate} endDate={event.endDate} />
          </Box>

          <Stack spacing={1} sx={{ minWidth: 0, flex: 1 }}>
            <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-start', justifyContent: 'space-between' }}>
              <Typography variant="h5" component="h2" sx={{ fontWeight: 700, overflowWrap: 'anywhere' }}>
                {event.eventName}
              </Typography>
              {hasEnded && <Chip label="Avslutat" size="small" />}
            </Box>
            <Typography variant="body2" color="text.secondary">
              {venueName ?? 'Lokal saknas'}
            </Typography>
            {event.eventDescription && (
              <Typography
                variant="body1"
                color="text.secondary"
                sx={{ display: '-webkit-box', WebkitBoxOrient: 'vertical', WebkitLineClamp: 3, overflow: 'hidden' }}
              >
                {event.eventDescription}
              </Typography>
            )}
            <Typography variant="body2" sx={{ pt: 0.5, fontWeight: 700 }}>
              {lowestPrice === null ? 'Pris saknas' : `Från ${priceFormatter.format(lowestPrice)}`}
            </Typography>
          </Stack>
        </Stack>
      </CardContent>
    </Card>
  )
}

export default function HomePage() {
  const now = useNow()
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

  if (eventsQuery.isPending || venuesQuery.isPending) {
    return (
      <Stack spacing={2} aria-label="Hämtar evenemang">
        <Typography variant="h3" component="h1" sx={{ mb: 1, fontWeight: 700 }}>
          Evenemang i Hälsingland
        </Typography>
        {[1, 2, 3].map((item) => <Skeleton key={item} variant="rounded" height={150} />)}
      </Stack>
    )
  }

  if (eventsQuery.isError || venuesQuery.isError) {
    const retry = () => {
      void eventsQuery.refetch()
      void venuesQuery.refetch()
    }

    return (
      <Stack spacing={2}>
        <Typography variant="h3" component="h1" sx={{ fontWeight: 700 }}>
          Evenemang i Hälsingland
        </Typography>
        <Alert severity="error" action={<Button color="inherit" size="small" onClick={retry}>Försök igen</Button>}>
          Evenemangen kunde inte hämtas just nu. Kontrollera anslutningen och försök igen.
        </Alert>
      </Stack>
    )
  }

  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h3" component="h1" sx={{ maxWidth: 620, fontWeight: 700, letterSpacing: '-0.035em' }}>
          Evenemang i Hälsingland
        </Typography>
        <Typography color="text.secondary" sx={{ mt: 1 }}>
          Hitta nästa upplevelse, från lokala konserter till stora evenemang.
        </Typography>
      </Box>

      {eventsQuery.data.length === 0 ? (
        <Alert severity="info">Det finns inga evenemang att visa just nu.</Alert>
      ) : (
        <>
          <Typography variant="body2" color="text.secondary">
            {eventsQuery.data.length} evenemang
          </Typography>
          <Stack spacing={2}>
            {eventsQuery.data.map((event) => (
              <EventCard key={event.eventId} event={event} venueName={venuesById.get(event.venueId)} now={now} />
            ))}
          </Stack>
        </>
      )}
    </Stack>
  )
}
