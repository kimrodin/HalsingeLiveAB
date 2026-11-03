import { Alert, Box, Button, Chip, Divider, Skeleton, Stack, Typography } from '@mui/material'
import { useQuery } from '@tanstack/react-query'
import { Link as RouterLink, useParams } from 'react-router'
import EventPoster from '../components/EventPoster'
import VenueInfo from '../../venues/components/VenueInfo'
import VenueSeatingMapViewer from '../../venues/components/VenueSeatingMapViewer'
import EventTicketInventory from '../../bookings/components/EventTicketInventory'
import { ApiError } from '../../../shared/api/http'
import { getEventById } from '../api/events'
import { getVenueById } from '../../venues/api/venues'
import { getVenueSeatingMaps } from '../../venues/api/seatingMaps'
import { useNow } from '../../../shared/hooks/useNow'
import { useState } from 'react'

const dateFormatter = new Intl.DateTimeFormat('sv-SE', {
  weekday: 'short',
  day: 'numeric',
  month: 'short',
})
const timeFormatter = new Intl.DateTimeFormat('sv-SE', { hour: '2-digit', minute: '2-digit' })
const saleDateFormatter = new Intl.DateTimeFormat('sv-SE', { day: 'numeric', month: 'short' })

function formatEventRange(startDate: string, endDate: string): string {
  const start = new Date(startDate)
  const end = new Date(endDate)
  const sameDay = start.toDateString() === end.toDateString()
  const dateRange = sameDay
    ? dateFormatter.format(start)
    : `${dateFormatter.format(start)} – ${dateFormatter.format(end)}`

  return `${dateRange} · kl. ${timeFormatter.format(start)}–${timeFormatter.format(end)}`
}

function isNotFound(error: unknown): boolean {
  return error instanceof ApiError && error.status === 404
}

export default function EventDetailPage() {
  const now = useNow()
  const [mapDialogOpen, setMapDialogOpen] = useState(false)
  const { eventId } = useParams<{ eventId: string }>()
  const eventQuery = useQuery({
    queryKey: ['events', eventId],
    queryFn: ({ signal }) => getEventById(eventId!, signal),
    enabled: Boolean(eventId),
  })
  const venueQuery = useQuery({
    queryKey: ['venues', eventQuery.data?.venueId],
    queryFn: ({ signal }) => getVenueById(eventQuery.data!.venueId, signal),
    enabled: Boolean(eventQuery.data?.venueId),
  })
  const seatingMapsQuery = useQuery({
    queryKey: ['venues', eventQuery.data?.venueId, 'seating-maps'],
    queryFn: ({ signal }) => getVenueSeatingMaps(eventQuery.data!.venueId, signal),
    enabled: Boolean(eventQuery.data?.venueId),
  })

  if (!eventId) {
    return <Alert severity="info">Evenemanget hittades inte.</Alert>
  }

  if (eventQuery.isPending) {
    return (
      <Stack spacing={2} role="status" aria-busy="true" aria-label="Hämtar evenemang">
        <Skeleton height={48} />
        <Skeleton height={180} />
      </Stack>
    )
  }

  if (eventQuery.isError) {
    if (isNotFound(eventQuery.error)) {
      return <Alert severity="info">Evenemanget hittades inte.</Alert>
    }

    return (
      <Alert severity="error" action={<Button color="inherit" size="small" onClick={() => void eventQuery.refetch()}>Försök igen</Button>}>
        Evenemanget kunde inte hämtas just nu.
      </Alert>
    )
  }

  const event = eventQuery.data

  return (
    <Stack spacing={3}>
      <Button component={RouterLink} to="/" sx={{ alignSelf: 'flex-start' }}>Tillbaka till evenemang</Button>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ alignItems: 'center' }}>
        <EventPoster imageUrl={event.imageUrl} eventName={event.name} />
        <Box>
          <Typography variant="h3" component="h1" sx={{ fontWeight: 700 }}>{event.name}</Typography>
          <Typography color="text.secondary">{formatEventRange(event.startDate, event.endDate)}</Typography>
        </Box>
      </Stack>

      {new Date(event.onSaleFrom).getTime() <= now ? (
        <Chip label="Biljettförsäljningen har startat" color="success" sx={{ alignSelf: 'flex-start' }} />
      ) : (
        <Chip
          label={`Biljetter släpps ${saleDateFormatter.format(new Date(event.onSaleFrom))}`}
          sx={{ alignSelf: 'flex-start' }}
        />
      )}

      {event.description && <Typography sx={{ whiteSpace: 'pre-line' }}>{event.description}</Typography>}

      <Divider />

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: 'minmax(0, 1fr)', lg: 'minmax(0, 1fr) minmax(320px, 420px)' },
          gap: 3,
          alignItems: 'start',
        }}
      >
        <Stack spacing={3} sx={{ minWidth: 0 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2 }}>
            <Typography variant="h5" component="h2" sx={{ fontWeight: 700 }}>Biljetter</Typography>
            {seatingMapsQuery.data?.length ? (
              <Button
                aria-controls={`venue-map-${event.venueId}`}
                aria-expanded={mapDialogOpen}
                onClick={() => setMapDialogOpen(true)}
                variant="outlined"
                sx={{ display: { xs: 'inline-flex', lg: 'none' }, flexShrink: 0 }}
              >
                Se platskarta
              </Button>
            ) : null}
          </Box>

          {venueQuery.isSuccess ? (
            <EventTicketInventory
              eventId={event.eventId}
              eventName={event.name}
              sections={venueQuery.data.sections}
              salesOpen={new Date(event.onSaleFrom).getTime() <= now}
            />
          ) : venueQuery.isPending ? (
            <Skeleton height={160} />
          ) : null}

          {venueQuery.isError && (
            <Alert severity={isNotFound(venueQuery.error) ? 'info' : 'error'}
              action={<Button color="inherit" size="small" onClick={() => void venueQuery.refetch()}>Försök igen</Button>}>
              {isNotFound(venueQuery.error) ? 'Scenen hittades inte.' : 'Scenens information kunde inte hämtas.'}
            </Alert>
          )}
        </Stack>

        <VenueSeatingMapViewer
          venueId={event.venueId}
          maps={seatingMapsQuery.data}
          isPending={seatingMapsQuery.isPending}
          isError={seatingMapsQuery.isError}
          onRetry={() => void seatingMapsQuery.refetch()}
          dialogOpen={mapDialogOpen}
          onClose={() => setMapDialogOpen(false)}
        />
      </Box>

      {venueQuery.isSuccess && (
        <>
          <Divider />
          <VenueInfo venue={venueQuery.data} />
        </>
      )}
    </Stack>
  )
}
