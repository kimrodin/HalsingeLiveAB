import { Box, Stack, Typography } from '@mui/material'
import type { VenueDetail } from '../api/venues'

interface VenueInfoProps {
  venue: VenueDetail
}

export default function VenueInfo({ venue }: VenueInfoProps) {
  const address = `${venue.streetAddress}, ${venue.postalCode} ${venue.city}`
  const mapUrl = `https://www.google.com/maps?q=${encodeURIComponent(address)}&output=embed`

  return (
    <section>
      <Stack spacing={0.5}>
        <Typography sx={{ fontWeight: 600 }}>{venue.name}</Typography>
        <Typography color="text.secondary">{venue.streetAddress}</Typography>
        <Typography color="text.secondary">{venue.postalCode} {venue.city}</Typography>
      </Stack>
      <Box
        component="iframe"
        title={`Karta till ${venue.name}`}
        src={mapUrl}
        loading="lazy"
        referrerPolicy="no-referrer-when-downgrade"
        allowFullScreen
        sx={{ display: 'block', width: '100%', height: { xs: 260, sm: 360 }, border: 0, borderRadius: 2, mt: 2 }}
      />
    </section>
  )
}
