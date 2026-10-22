import { useEffect, useRef, useState } from 'react'
import { Box, Button, Card, CardContent, Stack, Typography } from '@mui/material'
import { Link as RouterLink } from 'react-router'
import type { EventListItem } from '../api/events'
import DateBadge from './DateBadge'
import TicketAction from './TicketAction'

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


export default EventCarousel
