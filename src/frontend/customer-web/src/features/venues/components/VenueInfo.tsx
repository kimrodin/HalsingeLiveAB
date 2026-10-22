import { Stack, Typography } from '@mui/material'
import type { VenueDetail } from '../api/venues'

interface VenueInfoProps {
  venue: VenueDetail
}

export default function VenueInfo({ venue }: VenueInfoProps) {
  return (
    <section>
      <Typography variant="h5" component="h2" sx={{ mb: 1, fontWeight: 700 }}>Scen</Typography>
      <Stack spacing={0.5}>
        <Typography sx={{ fontWeight: 600 }}>{venue.name}</Typography>
        <Typography color="text.secondary">{venue.streetAddress}</Typography>
        <Typography color="text.secondary">{venue.postalCode} {venue.city}</Typography>
      </Stack>
    </section>
  )
}
