import { useState } from 'react'
import { Box } from '@mui/material'
import fallbackPoster from '../../../assets/event-poster-fallback.svg'

interface EventPosterProps {
  eventName: string
  imageUrl?: string | null
  compact?: boolean
  tile?: boolean
  banner?: boolean
}

export default function EventPoster({ eventName, imageUrl, compact = false, tile = false, banner = false }: EventPosterProps) {
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
        width: tile ? '100%' : banner ? { xs: '100%', sm: 142, md: 164 } : compact ? { xs: 84, sm: 92 } : { xs: 100, sm: 142, md: 164 },
        aspectRatio: banner ? { xs: '16 / 9', sm: '3 / 4' } : '3 / 4',
        objectFit: 'cover',
        borderRadius: tile ? 2 : 2.5,
        flexShrink: 0,
        display: 'block',
      }}
    />
  )
}
