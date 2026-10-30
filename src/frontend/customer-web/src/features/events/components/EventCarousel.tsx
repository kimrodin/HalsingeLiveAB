import { useEffect, useRef, useState } from 'react'
import type { KeyboardEvent } from 'react'
import { Box, Button, Card, CardContent, Stack, Typography } from '@mui/material'
import { Link as RouterLink } from 'react-router'
import type { EventListItem } from '../api/events'
import TicketAction from './TicketAction'
import EventPoster from './EventPoster'

const eventDateFormatter = new Intl.DateTimeFormat('sv-SE', {
  weekday: 'long',
  day: 'numeric',
  month: 'long',
  hour: '2-digit',
  minute: '2-digit',
})
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

  useEffect(() => {
    if (events.length === 0 || activeIndex < events.length) return

    const nextIndex = events.length - 1
    const viewport = viewportRef.current
    viewport?.scrollTo({ left: nextIndex * viewport.clientWidth, behavior: 'auto' })
  }, [activeIndex, events.length])

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
      left: slide.offsetLeft,
      behavior: reduceMotion ? 'auto' : 'smooth',
    })
    setActiveIndex(index)
  }

  const handleKeyDown = (keyboardEvent: KeyboardEvent<HTMLDivElement>) => {
    if (keyboardEvent.key === 'ArrowRight') {
      keyboardEvent.preventDefault()
      scrollToIndex(currentIndex + 1)
    } else if (keyboardEvent.key === 'ArrowLeft') {
      keyboardEvent.preventDefault()
      scrollToIndex(currentIndex - 1)
    } else if (keyboardEvent.key === 'Home') {
      keyboardEvent.preventDefault()
      scrollToIndex(0)
    } else if (keyboardEvent.key === 'End') {
      keyboardEvent.preventDefault()
      scrollToIndex(events.length - 1)
    }
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
      sx={{
        mt: 3,
        animation: 'eventCarouselFadeIn 420ms ease-out both',
        '@keyframes eventCarouselFadeIn': {
          from: { opacity: 0, transform: 'translateY(8px)' },
          to: { opacity: 1, transform: 'translateY(0)' },
        },
        '@media (prefers-reduced-motion: reduce)': { animation: 'none' },
      }}
    >
      <Box
        ref={viewportRef}
        onScroll={handleScroll}
        onKeyDown={handleKeyDown}
        tabIndex={0}
        aria-label="Utvalda evenemang. Använd piltangenterna för att byta evenemang."
        sx={{
          position: 'relative', // behövs för att offsetLeft ska räknas från containern
          display: 'flex',
          alignItems: 'stretch',
          gap: 2,
          overflowX: 'auto',
          py: 1,
          scrollSnapType: 'x mandatory',
          scrollBehavior: 'smooth',
          scrollbarWidth: 'none',
          '&::-webkit-scrollbar': { display: 'none' },
          '@media (prefers-reduced-motion: reduce)': { scrollBehavior: 'auto' },
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
              sx={{
                flex: '0 0 calc(100% - 16px)',
                minWidth: 0,
                display: 'flex',
                boxSizing: 'border-box',
                scrollSnapAlign: 'start',
                overflowX: 'clip',
              }}
            >
              <Card
                variant="outlined"
                sx={{
                  height: '100%',
                  width: '100%',
                  display: 'flex',
                  flexDirection: 'column',
                  overflow: 'hidden',
                  borderRadius: 3,
                  borderColor: 'divider',
                  boxShadow: '0 5px 18px rgba(28, 48, 48, 0.075)',
                  position: 'relative',
                  '&::before': {
                    content: '""',
                    position: 'absolute',
                    inset: '0 0 auto',
                    height: 3,
                    bgcolor: 'primary.light',
                  },
                }}
              >
                <CardContent
                  sx={{
                    p: { xs: 2.5, sm: 3.5 },
                    '&:last-child': { pb: { xs: 2.5, sm: 3.5 } },
                    height: '100%',
                    width: '100%',
                    boxSizing: 'border-box',
                    flex: 1,
                    display: 'grid',
                    gridTemplateColumns: { xs: '100px minmax(0, 1fr)', sm: '142px minmax(0, 1fr)', md: '164px minmax(0, 1fr)' },
                    gridTemplateRows: { xs: 'minmax(0, 1fr) auto', sm: 'minmax(0, 1fr) auto' },
                    columnGap: { xs: 1.5, sm: 2.5 },
                    rowGap: 1.5,
                    alignItems: 'center',
                    minHeight: { xs: 280, sm: 272 },
                  }}
                >
                    <Box
                      sx={{
                        gridColumn: 1,
                        gridRow: { xs: 1, sm: '1 / span 2' },
                        justifySelf: 'start',
                      }}
                    >
                      <EventPoster imageUrl={event.imageUrl} eventName={event.eventName} />
                    </Box>
                    <Box sx={{ minWidth: 0, gridColumn: 2, gridRow: 1, py: { sm: 1 } }}>
                      <Typography variant="overline" color="primary.main" sx={{ fontWeight: 700, letterSpacing: 1.2 }}>
                        Utvalt evenemang
                      </Typography>
                      <Typography variant="h3" component="h2" sx={{ mt: 0.25, fontWeight: 700, overflowWrap: 'anywhere', lineHeight: 1.1, display: '-webkit-box', WebkitBoxOrient: 'vertical', WebkitLineClamp: 2, overflow: 'hidden' }}>
                        {event.eventName}
                      </Typography>
                      <Typography color="text.secondary" sx={{ mt: 1, '&::first-letter': { textTransform: 'uppercase' }, display: '-webkit-box', WebkitBoxOrient: 'vertical', WebkitLineClamp: 2, overflow: 'hidden' }}>
                        {eventDateFormatter.format(new Date(event.startDate))}
                        {venueName ? ` · ${venueName}` : ''}
                      </Typography>
                      {event.eventDescription && (
                        <Typography
                          color="text.secondary"
                          sx={{
                            mt: 1.5,
                            display: '-webkit-box',
                            WebkitBoxOrient: 'vertical',
                            WebkitLineClamp: 2,
                            overflow: 'hidden',
                            lineHeight: 1.5,
                          }}
                        >
                          {event.eventDescription}
                        </Typography>
                      )}
                    </Box>
                    <Box
                      sx={{
                        width: '100%',
                        minWidth: 0,
                        gridColumn: { xs: '1 / -1', sm: 2 },
                        gridRow: 2,
                        justifySelf: 'end',
                        alignSelf: 'end',
                      }}
                    >
                      <Stack direction="row" spacing={1}>
                        <Button
                          component={RouterLink}
                          to={`/events/${event.eventId}`}
                          variant="outlined"
                          fullWidth
                          sx={{ height: 40, flex: 1, minWidth: 0, px: 1 }}
                          aria-label={`Detaljer om ${event.eventName}`}
                        >
                          Detaljer
                        </Button>
                        <Box sx={{ flex: 1, minWidth: 0, '& > *': { width: '100%', height: 40 } }}>
                          <TicketAction event={event} now={now} />
                        </Box>
                      </Stack>
                    </Box>
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


export default EventCarousel
