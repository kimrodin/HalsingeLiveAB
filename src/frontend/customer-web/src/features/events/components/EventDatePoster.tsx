import { useState } from 'react'
import { Box, Typography } from '@mui/material'

const posterMonthFormatter = new Intl.DateTimeFormat('sv-SE', { month: 'long' })

interface EventDatePosterProps {
  startDate: string
  eventName: string
  imageUrl?: string | null
  compact?: boolean
}

export default function EventDatePoster({ startDate, eventName, imageUrl, compact = false }: EventDatePosterProps) {
  const [failedUrl, setFailedUrl] = useState<string | null>(null)
  const width = compact ? { xs: 84, sm: 92 } : { xs: 100, sm: 142, md: 164 }
  if (imageUrl && failedUrl !== imageUrl) {
    return <Box component="img" src={imageUrl} alt={`Poster för ${eventName}`}
      loading="lazy" onError={() => setFailedUrl(imageUrl)}
      sx={{ width, aspectRatio: '3 / 4', objectFit: 'cover', borderRadius: 2.5, flexShrink: 0, display: 'block' }} />
  }
  const date = new Date(startDate)

  return (
    <Box
      aria-label={`Grafisk evenemangsposter för ${eventName}`}
      role="img"
      sx={{
        width,
        aspectRatio: '3 / 4',
        p: { xs: 1.25, sm: 2 },
        boxSizing: 'border-box',
        position: 'relative',
        overflow: 'hidden',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
        borderRadius: 2.5,
        bgcolor: 'secondary.dark',
        color: 'secondary.contrastText',
        '&::before': {
          content: '""',
          position: 'absolute',
          width: 150,
          height: 150,
          border: '1px solid',
          borderColor: 'rgba(246,239,227,0.2)',
          borderRadius: '50%',
          right: -75,
          top: -48,
        },
        '&::after': {
          content: '""',
          position: 'absolute',
          width: 110,
          height: 110,
          border: '1px solid',
          borderColor: 'rgba(246,239,227,0.16)',
          borderRadius: '50%',
          right: -40,
          top: -28,
        },
      }}
    >
      <Typography
        variant="overline"
        sx={{
          position: 'relative',
          zIndex: 1,
          letterSpacing: { xs: 0.5, sm: 1.2 },
          lineHeight: 1.2,
          fontSize: { xs: '0.55rem', sm: '0.75rem' },
        }}
      >
        Hälsinge Live
      </Typography>
      <Box sx={{ position: 'relative', zIndex: 1 }}>
        <Typography
          sx={{
            fontFamily: (theme) => theme.typography.h1.fontFamily,
            fontSize: { xs: '2.5rem', sm: '3rem', md: '3.5rem' },
            lineHeight: 0.95,
          }}
        >
          {date.getDate()}
        </Typography>
        <Typography
          variant="subtitle2"
          sx={{
            mt: 0.5,
            textTransform: 'uppercase',
            letterSpacing: { xs: 0.5, sm: 1 },
            fontSize: { xs: '0.65rem', sm: '0.875rem' },
          }}
        >
          {posterMonthFormatter.format(date)}
        </Typography>
      </Box>
    </Box>
  )
}
