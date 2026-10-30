import { useState } from 'react'
import { Box } from '@mui/material'
import fallbackPoster from '../../../assets/event-poster-fallback.svg'

interface EventPosterProps {
  eventName: string
  imageUrl?: string | null
  compact?: boolean
}

export default function EventPoster({ eventName, imageUrl, compact = false }: EventPosterProps) {
  const [failedUrl, setFailedUrl] = useState<string | null>(null)
  const hasImage = Boolean(imageUrl && failedUrl !== imageUrl)

  return (
    <Box
      component="img"
      src={hasImage ? imageUrl! : fallbackPoster}
      alt={hasImage ? `Poster för ${eventName}` : `Hälsinge Live – ${eventName}`}
      loading="lazy"
      onError={hasImage ? () => setFailedUrl(imageUrl!) : undefined}
      sx={{
        width: compact ? { xs: 84, sm: 92 } : { xs: 100, sm: 142, md: 164 },
        aspectRatio: '3 / 4',
        objectFit: 'cover',
        borderRadius: 2.5,
        flexShrink: 0,
        display: 'block',
      }}
    />
  )
}
