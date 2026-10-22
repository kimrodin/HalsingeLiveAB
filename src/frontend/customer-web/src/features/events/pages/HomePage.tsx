import { useMemo, useRef, useState } from 'react'
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
  TextField,
  Typography,
} from '@mui/material'
import { Link as RouterLink } from 'react-router'
import { getAllEvents, type EventListItem } from '../api/events'
import { getVenues } from '../../venues/api/venues'
import { useNow } from '../../../shared/hooks/useNow'
import DateBadge from '../components/DateBadge'

const priceFormatter = new Intl.NumberFormat('sv-SE', {
  style: 'currency',
  currency: 'SEK',
  maximumFractionDigits: 2,
})

const eventDateFormatter = new Intl.DateTimeFormat('sv-SE', {
  weekday: 'long',
  day: 'numeric',
  month: 'long',
  hour: '2-digit',
  minute: '2-digit',
})

function EventCarousel({ events, venuesById }: { events: EventListItem[]; venuesById: Map<string, string> }) {
  const viewportRef = useRef<HTMLDivElement>(null)
  const [activeIndex, setActiveIndex] = useState(0)

  if (events.length === 0) return null

  const scrollToIndex = (requestedIndex: number) => {
    const viewport = viewportRef.current
    if (!viewport) return

    const index = (requestedIndex + events.length) % events.length
    const slide = viewport.querySelector<HTMLElement>(`[data-carousel-index="${index}"]`)
    if (!slide) return

    const viewportRect = viewport.getBoundingClientRect()
    const slideRect = slide.getBoundingClientRect()
    viewport.scrollTo({
      left: viewport.scrollLeft + slideRect.left - viewportRect.left - (viewport.clientWidth - slide.clientWidth) / 2,
      behavior: 'smooth',
    })
    setActiveIndex(index)
  }

  const updateActiveIndex = () => {
    const viewport = viewportRef.current
    if (!viewport) return

    const viewportCenter = viewport.getBoundingClientRect().left + viewport.clientWidth / 2
    const slides = Array.from(viewport.querySelectorAll<HTMLElement>('[data-carousel-index]'))
    const closestIndex = slides.reduce((closest, slide) => {
      const slideIndex = Number(slide.dataset.carouselIndex)
      const closestSlide = slides[closest]
      return Math.abs(slide.getBoundingClientRect().left + slide.clientWidth / 2 - viewportCenter)
        < Math.abs(closestSlide.getBoundingClientRect().left + closestSlide.clientWidth / 2 - viewportCenter)
        ? slideIndex
        : closest
    }, 0)

    setActiveIndex((current) => current === closestIndex ? current : closestIndex)
  }

  return (
    <Box component="section" aria-label="Kommande evenemang" sx={{ mt: 3 }}>
      <Box
        ref={viewportRef}
        onScroll={updateActiveIndex}
        sx={{
          display: 'flex',
          gap: 2,
          overflowX: 'auto',
          py: 1,
          scrollSnapType: 'x mandatory',
          scrollBehavior: 'smooth',
          scrollbarWidth: 'none',
          '&::-webkit-scrollbar': { display: 'none' },
          '&::before, &::after': { content: '""', flex: '0 0 9%' },
          '& > [data-carousel-index]': { flex: '0 0 82%', scrollSnapAlign: 'center' },
        }}
      >
        {events.map((event, index) => (
          <Box key={event.eventId} data-carousel-index={index}>
            <Card variant="outlined" sx={{ height: '100%', overflow: 'hidden', borderRadius: 3 }}>
              <CardContent sx={{ p: { xs: 2.5, sm: 4 } }}>
                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2.5} sx={{ alignItems: { sm: 'center' } }}>
                  <DateBadge startDate={event.startDate} endDate={event.endDate} />
                  <Box sx={{ minWidth: 0, flex: 1 }}>
                    <Typography variant="overline" color="text.secondary">Utvalt evenemang</Typography>
                    <Typography variant="h4" component="h2" sx={{ fontWeight: 700, overflowWrap: 'anywhere' }}>
                      {event.eventName}
                    </Typography>
                    <Typography color="text.secondary" sx={{ textTransform: 'capitalize' }}>
                      {eventDateFormatter.format(new Date(event.startDate))}
                      {venuesById.get(event.venueId) ? ` · ${venuesById.get(event.venueId)}` : ''}
                    </Typography>
                    {event.eventDescription && (
                      <Typography color="text.secondary" sx={{ mt: 1, display: '-webkit-box', WebkitBoxOrient: 'vertical', WebkitLineClamp: 2, overflow: 'hidden' }}>
                        {event.eventDescription}
                      </Typography>
                    )}
                  </Box>
                  <Stack spacing={1} sx={{ width: { xs: '100%', sm: 160 }, flexShrink: 0 }}>
                    <Button component={RouterLink} to={`/events/${event.eventId}`} variant="outlined" fullWidth>Detaljer</Button>
                    <Button component={RouterLink} to={`/events/${event.eventId}#biljetter`} variant="contained" fullWidth>Köp biljett</Button>
                  </Stack>
                </Stack>
              </CardContent>
            </Card>
          </Box>
        ))}
      </Box>
      {events.length > 1 && (
        <Stack direction="row" spacing={1} sx={{ mt: 1.5, alignItems: 'center', justifyContent: 'center' }}>
          <Button aria-label="Föregående evenemang" onClick={() => scrollToIndex(activeIndex - 1)} sx={{ minWidth: 44, fontSize: '1.5rem' }}>
            ‹
          </Button>
          {events.map((event, index) => (
            <Button
              key={event.eventId}
              aria-label={`Visa evenemang ${index + 1}: ${event.eventName}`}
              aria-current={index === activeIndex ? 'true' : undefined}
              onClick={() => scrollToIndex(index)}
              sx={{ minWidth: 32, px: 1, color: index === activeIndex ? 'primary.main' : 'text.disabled' }}
            >
              {index === activeIndex ? '●' : '○'}
            </Button>
          ))}
          <Button aria-label="Nästa evenemang" onClick={() => scrollToIndex(activeIndex + 1)} sx={{ minWidth: 44, fontSize: '1.5rem' }}>
            ›
          </Button>
        </Stack>
      )}
    </Box>
  )
}

function EventCard({ event, venueName, now }: { event: EventListItem; venueName?: string; now: number }) {
  const lowestPrice = event.sectionPrices.length > 0
    ? Math.min(...event.sectionPrices.map(({ price }) => price))
    : null
  const hasEnded = new Date(event.endDate).getTime() < now

  return (
    <Card
      variant="outlined"
      sx={{
        bgcolor: 'background.paper',
        borderColor: 'divider',
        borderRadius: 2.5,
        transition: 'transform 160ms ease, box-shadow 160ms ease, border-color 160ms ease',
        '&:hover, &:focus-within': {
          transform: 'translateY(-2px)',
          boxShadow: 3,
          borderColor: 'primary.main',
        },
      }}
    >
      <CardContent sx={{ p: { xs: 2, sm: 2.5 }, '&:last-child': { pb: { xs: 2, sm: 2.5 } } }}>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2.5}>
          <Stack direction="row" spacing={1.25} sx={{ flexShrink: 0, alignItems: 'stretch' }}>
            <Box
              sx={{
                width: 56,
                display: 'flex',
                '& > div': {
                  width: '100%',
                  flex: 1,
                  boxSizing: 'border-box',
                  display: 'flex',
                  flexDirection: 'column',
                  justifyContent: 'center',
                },
              }}
            >
              <DateBadge startDate={event.startDate} endDate={event.startDate} />
            </Box>
            <Box
              role="img"
              aria-label={`Poster saknas för ${event.eventName}`}
              sx={{
                width: { xs: 84, sm: 92 },
                aspectRatio: '3 / 4',
                display: 'grid',
                placeItems: 'center',
                flexShrink: 0,
                border: '1px dashed',
                borderColor: 'divider',
                borderRadius: 2,
                bgcolor: 'action.hover',
                color: 'text.secondary',
              }}
            >
              <Typography variant="caption">Poster</Typography>
            </Box>
          </Stack>

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
          <Stack
            spacing={1}
            sx={{
              alignSelf: { xs: 'stretch', sm: 'center' },
              flexShrink: 0,
              width: { xs: '100%', sm: 160 },
            }}
          >
            <Button component={RouterLink} to={`/events/${event.eventId}`} variant="outlined" fullWidth>
              Detaljer
            </Button>
            <Button component={RouterLink} to={`/events/${event.eventId}#biljetter`} variant="contained" fullWidth>
              Köp biljett
            </Button>
          </Stack>
        </Stack>
      </CardContent>
    </Card>
  )
}

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
  const featuredEvents = eventsQuery.data?.filter((event) => new Date(event.endDate).getTime() >= now).slice(0, 3) ?? []
  const normalizedSearchTerm = searchTerm.trim().toLocaleLowerCase('sv-SE')
  const filteredEvents = eventsQuery.data?.filter((event) =>
    event.eventName.toLocaleLowerCase('sv-SE').includes(normalizedSearchTerm),
  ) ?? []

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
      <Box component="header">
        <Typography variant="h3" component="h1" sx={{ maxWidth: 620, fontWeight: 700, letterSpacing: '-0.035em' }}>
          Evenemang i Hälsingland
        </Typography>
        <Typography color="text.secondary" sx={{ mt: 1 }}>
          Hitta nästa upplevelse, från lokala konserter till stora evenemang.
        </Typography>
        <EventCarousel events={featuredEvents} venuesById={venuesById} />
      </Box>

      <TextField
        label="Sök evenemang"
        value={searchTerm}
        onChange={(event) => setSearchTerm(event.target.value)}
        fullWidth
      />

      {filteredEvents.length === 0 ? (
        eventsQuery.data.length === 0 ? (
          <Alert severity="info">Det finns inga evenemang att visa just nu.</Alert>
        ) : (
          <Alert severity="info">Inga evenemang matchar din sökning.</Alert>
        )
      ) : (
        <>
          <Typography variant="body2" color="text.secondary">
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
