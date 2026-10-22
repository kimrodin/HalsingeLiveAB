import { useEffect, useMemo, useRef, useState } from 'react'
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
  minimumFractionDigits: 0,
  maximumFractionDigits: 2,
})

const eventDateFormatter = new Intl.DateTimeFormat('sv-SE', {
  weekday: 'long',
  day: 'numeric',
  month: 'long',
  hour: '2-digit',
  minute: '2-digit',
})

const onSaleFormatter = new Intl.DateTimeFormat('sv-SE', { day: 'numeric', month: 'short' })

type SaleState = 'ended' | 'upcoming' | 'onSale'

function getSaleState(event: EventListItem, now: number): SaleState {
  if (new Date(event.endDate).getTime() < now) return 'ended'
  if (new Date(event.onSaleFrom).getTime() > now) return 'upcoming'
  return 'onSale'
}

// Visar köpknapp, "släpps"-chip eller ingenting beroende på försäljningsläget
function TicketAction({ event, now }: { event: EventListItem; now: number }) {
  const state = getSaleState(event, now)

  if (state === 'ended') {
    return (
      <Chip
        label="Avslutad"
        sx={{ width: '100%', height: 40, borderRadius: 1, justifyContent: 'center' }}
      />
    )
  }

  if (state === 'upcoming') {
    return (
      <Chip
        color="success"
        label={`Biljettsläpp ${onSaleFormatter.format(new Date(event.onSaleFrom))}`}
        sx={{
          width: '100%',
          height: 40,
          borderRadius: 1,
          justifyContent: 'center',
          '& .MuiChip-label': { whiteSpace: 'normal', textAlign: 'center' },
        }}
      />
    )
  }

  return (
    <Button
      component={RouterLink}
      to={`/events/${event.eventId}#biljetter`}
      variant="contained"
      fullWidth
      sx={{ height: 40 }}
      aria-label={`Köp biljett till ${event.eventName}`}
    >
      Köp biljett
    </Button>
  )
}

interface EventCarouselProps {
  events: EventListItem[]
  venuesById: Map<string, string>
  now: number
}

function EventCarousel({ events, venuesById, now }: EventCarouselProps) {
  const viewportRef = useRef<HTMLDivElement>(null)
  const frameRef = useRef<number | null>(null)
  const [activeIndex, setActiveIndex] = useState(0)

  useEffect(() => {
    return () => {
      if (frameRef.current !== null) cancelAnimationFrame(frameRef.current)
    }
  }, [])

  if (events.length === 0) return null

  // Listan kan bli kortare när ett evenemang tar slut medan sidan är öppen
  const currentIndex = Math.min(activeIndex, events.length - 1)

  const scrollToIndex = (requestedIndex: number) => {
    const viewport = viewportRef.current
    if (!viewport) return

    const index = (requestedIndex + events.length) % events.length
    const slide = viewport.querySelector<HTMLElement>(`[data-carousel-index="${index}"]`)
    if (!slide) return

    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    viewport.scrollTo({
      left: slide.offsetLeft - (viewport.clientWidth - slide.offsetWidth) / 2,
      behavior: reduceMotion ? 'auto' : 'smooth',
    })
    setActiveIndex(index)
  }

  const updateActiveIndex = () => {
    const viewport = viewportRef.current
    if (!viewport) return

    const center = viewport.scrollLeft + viewport.clientWidth / 2
    let closestIndex = 0
    let smallestDistance = Infinity

    viewport.querySelectorAll<HTMLElement>('[data-carousel-index]').forEach((slide) => {
      const distance = Math.abs(slide.offsetLeft + slide.offsetWidth / 2 - center)
      if (distance < smallestDistance) {
        smallestDistance = distance
        closestIndex = Number(slide.dataset.carouselIndex)
      }
    })

    setActiveIndex(closestIndex)
  }

  // Scrollhändelsen avfyras väldigt ofta; räkna högst en gång per bildruta
  const handleScroll = () => {
    if (frameRef.current !== null) return
    frameRef.current = requestAnimationFrame(() => {
      frameRef.current = null
      updateActiveIndex()
    })
  }

  return (
    <Box
      component="section"
      aria-label="Kommande evenemang"
      aria-roledescription="karusell"
      sx={{ mt: 3 }}
    >
      <Box
        ref={viewportRef}
        onScroll={handleScroll}
        tabIndex={0}
        sx={{
          position: 'relative', // behövs för att offsetLeft ska räknas från containern
          display: 'flex',
          gap: 2,
          overflowX: 'auto',
          py: 1,
          scrollSnapType: 'x mandatory',
          scrollBehavior: 'smooth',
          scrollbarWidth: 'none',
          '&::-webkit-scrollbar': { display: 'none' },
          '@media (prefers-reduced-motion: reduce)': { scrollBehavior: 'auto' },
          // gap läggs även runt spacers, så dra bort det
          '&::before, &::after': { content: '""', flex: '0 0 calc(9% - 16px)' },
        }}
      >
        {events.map((event, index) => {
          const venueName = venuesById.get(event.venueId)

          return (
            <Box
              key={event.eventId}
              data-carousel-index={index}
              role="group"
              aria-roledescription="bild"
              aria-label={`${index + 1} av ${events.length}`}
              sx={{ flex: '0 0 82%', scrollSnapAlign: 'center' }}
            >
              <Card variant="outlined" sx={{ height: '100%', overflow: 'hidden', borderRadius: 3 }}>
                <CardContent sx={{ p: { xs: 2.5, sm: 4 } }}>
                  <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2.5} sx={{ alignItems: { sm: 'center' } }}>
                    <DateBadge startDate={event.startDate} endDate={event.endDate} />
                    <Box sx={{ minWidth: 0, flex: 1 }}>
                      <Typography variant="overline" color="text.secondary">Utvalt evenemang</Typography>
                      <Typography variant="h4" component="h2" sx={{ fontWeight: 700, overflowWrap: 'anywhere' }}>
                        {event.eventName}
                      </Typography>
                      <Typography color="text.secondary" sx={{ '&::first-letter': { textTransform: 'uppercase' } }}>
                        {eventDateFormatter.format(new Date(event.startDate))}
                        {venueName ? ` · ${venueName}` : ''}
                      </Typography>
                      {event.eventDescription && (
                        <Typography
                          color="text.secondary"
                          sx={{
                            mt: 1,
                            display: '-webkit-box',
                            WebkitBoxOrient: 'vertical',
                            WebkitLineClamp: 2,
                            overflow: 'hidden',
                          }}
                        >
                          {event.eventDescription}
                        </Typography>
                      )}
                    </Box>
                    <Stack spacing={1} sx={{ width: { xs: '100%', sm: 160 }, flexShrink: 0 }}>
                      <Button
                        component={RouterLink}
                        to={`/events/${event.eventId}`}
                        variant="outlined"
                        fullWidth
                        sx={{ height: 40 }}
                        aria-label={`Detaljer om ${event.eventName}`}
                      >
                        Detaljer
                      </Button>
                      <TicketAction event={event} now={now} />
                    </Stack>
                  </Stack>
                </CardContent>
              </Card>
            </Box>
          )
        })}
      </Box>

      {events.length > 1 && (
        <Stack direction="row" spacing={1} sx={{ mt: 1.5, alignItems: 'center', justifyContent: 'center' }}>
          <Button
            aria-label="Föregående evenemang"
            onClick={() => scrollToIndex(currentIndex - 1)}
            sx={{ minWidth: 44, fontSize: '1.5rem' }}
          >
            ‹
          </Button>
          {events.map((event, index) => (
            <Button
              key={event.eventId}
              aria-label={`Visa evenemang ${index + 1}: ${event.eventName}`}
              aria-current={index === currentIndex ? 'true' : undefined}
              onClick={() => scrollToIndex(index)}
              sx={{ minWidth: 32, px: 1, color: index === currentIndex ? 'primary.main' : 'text.disabled' }}
            >
              {index === currentIndex ? '●' : '○'}
            </Button>
          ))}
          <Button
            aria-label="Nästa evenemang"
            onClick={() => scrollToIndex(currentIndex + 1)}
            sx={{ minWidth: 44, fontSize: '1.5rem' }}
          >
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
  const hasEnded = getSaleState(event, now) === 'ended'

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
        '@media (prefers-reduced-motion: reduce)': {
          transition: 'none',
          '&:hover, &:focus-within': { transform: 'none' },
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
              aria-hidden="true"
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
            {venueName && (
              <Typography variant="body2" color="text.secondary">
                {venueName}
              </Typography>
            )}
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
            <Button
              component={RouterLink}
              to={`/events/${event.eventId}`}
              variant="outlined"
              fullWidth
              sx={{ height: 40 }}
              aria-label={`Detaljer om ${event.eventName}`}
            >
              Detaljer
            </Button>
            <TicketAction event={event} now={now} />
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
    <Stack spacing={3}>
      <Box component="header">
        <Typography variant="h3" component="h1" sx={{ maxWidth: 620, fontWeight: 700, letterSpacing: '-0.035em' }}>
          Evenemang i Hälsingland
        </Typography>
        <Typography color="text.secondary" sx={{ mt: 1 }}>
          Hitta nästa upplevelse, från lokala konserter till stora evenemang.
        </Typography>
        <EventCarousel events={featuredEvents} venuesById={venuesById} now={now} />
      </Box>

      <TextField
        label="Sök evenemang"
        value={searchTerm}
        onChange={(event) => setSearchTerm(event.target.value)}
        fullWidth
      />

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
