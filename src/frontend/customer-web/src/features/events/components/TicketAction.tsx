import { Button, Chip } from '@mui/material'
import { Link as RouterLink } from 'react-router'
import type { EventListItem } from '../api/events'
import { getSaleState } from './saleState'

const onSaleFormatter = new Intl.DateTimeFormat('sv-SE', { day: 'numeric', month: 'short' })


// Visar köpknapp, "släpps"-chip eller ingenting beroende på försäljningsläget
export default function TicketAction({ event, now }: { event: EventListItem; now: number }) {
  const state = getSaleState(event, now)

  if (state === 'ended') {
    return (
      <Chip
        label="Avslutad"
        sx={{
          width: '100%',
          height: 40,
          borderRadius: 1,
          justifyContent: 'center',
          '& .MuiChip-label': { typography: 'button' },
        }}
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
          '& .MuiChip-label': { typography: 'button', whiteSpace: 'normal', textAlign: 'center' },
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

