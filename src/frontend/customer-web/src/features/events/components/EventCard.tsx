import { Box, Button, Card, CardActionArea, CardContent, Chip, Stack, Typography } from '@mui/material'
import { Link as RouterLink } from 'react-router'
import type { EventListItem } from '../api/events'
import DateBadge from './DateBadge'
import EventPoster from './EventPoster'
import TicketAction from './TicketAction'
import { getSaleState } from './saleState'

const priceFormatter = new Intl.NumberFormat('sv-SE', {
  style: 'currency',
  currency: 'SEK',
  minimumFractionDigits: 0,
  maximumFractionDigits: 2,
})
function EventCard({ event, venueName, now }: { event: EventListItem; venueName?: string; now: number }) {
  const lowestPrice = event.sectionPrices.length > 0
    ? Math.min(...event.sectionPrices.map(({ price }) => price))
    : null
  const hasEnded = getSaleState(event, now) === 'ended'

  return (
    <>
      <Card
        variant="outlined"
        sx={{
          display: { xs: 'none', sm: 'block' },
          bgcolor: 'background.paper',
          borderColor: 'divider',
          borderRadius: 2.5,
          transition: 'transform 160ms ease, box-shadow 160ms ease, border-color 160ms ease',
          '&:hover, &:focus-within': {
            transform: 'translateY(-2px)',
            boxShadow: '0 8px 22px rgba(28, 48, 48, 0.10)',
            borderColor: 'primary.light',
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
              <EventPoster eventName={event.eventName} imageUrl={event.imageUrl} compact />
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

      <Card
        variant="outlined"
        sx={{
          display: { xs: 'block', sm: 'none' },
          height: '100%',
          borderRadius: 2,
          overflow: 'hidden',
          boxShadow: '0 2px 8px rgba(28, 48, 48, 0.06)',
          '&:hover, &:focus-within': { borderColor: 'primary.light' },
        }}
      >
        <CardActionArea
          component={RouterLink}
          to={`/events/${event.eventId}`}
          aria-label={`Visa detaljer om ${event.eventName}`}
          sx={{
            height: '100%',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'stretch',
            '&.Mui-focusVisible': { outline: '2px solid', outlineColor: 'primary.main', outlineOffset: -2 },
          }}
        >
          <Box sx={{ position: 'relative', width: '100%' }}>
            <EventPoster eventName={event.eventName} imageUrl={event.imageUrl} tile />
            <Box sx={{ position: 'absolute', top: 8, left: 8, width: 64 }}>
              <DateBadge startDate={event.startDate} endDate={event.startDate} compact />
            </Box>
          </Box>
          <CardContent sx={{ width: '100%', p: 1.25, '&:last-child': { pb: 1.25 } }}>
            <Typography
              variant="body2"
              component="h2"
              sx={{
                fontWeight: 700,
                lineHeight: 1.3,
                minHeight: '2.6em',
                display: '-webkit-box',
                WebkitBoxOrient: 'vertical',
                WebkitLineClamp: 2,
                overflow: 'hidden',
              }}
            >
              {event.eventName}
            </Typography>
            {venueName && (
              <Typography
                variant="caption"
                color="text.secondary"
                sx={{
                  mt: 0.5,
                  display: '-webkit-box',
                  WebkitBoxOrient: 'vertical',
                  WebkitLineClamp: 1,
                  overflow: 'hidden',
                }}
              >
                {venueName}
              </Typography>
            )}
            <Typography variant="caption" sx={{ mt: 0.75, display: 'block', fontWeight: 700, color: 'primary.dark' }}>
              {lowestPrice === null ? 'Pris saknas' : `Från ${priceFormatter.format(lowestPrice)}`}
            </Typography>
          </CardContent>
        </CardActionArea>
      </Card>
    </>
  )
}


export default EventCard
