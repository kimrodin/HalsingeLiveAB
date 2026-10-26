import { useState } from 'react'
import {
  Alert, Box, Button, ButtonBase, CircularProgress, Dialog, DialogActions,
  DialogContent, DialogTitle, Paper, Tab, Tabs, Typography, useMediaQuery, useTheme,
} from '@mui/material'
import type { VenueSeatingMap } from '../api/seatingMaps'

interface VenueSeatingMapViewerProps {
  venueId: string
  maps?: VenueSeatingMap[]
  isPending: boolean
  isError: boolean
  onRetry: () => void
  dialogOpen: boolean
  onClose: () => void
}

export default function VenueSeatingMapViewer({
  venueId, maps, isPending, isError, onRetry, dialogOpen, onClose,
}: VenueSeatingMapViewerProps) {
  const theme = useTheme()
  const isDesktop = useMediaQuery(theme.breakpoints.up('lg'))
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [zoomOpen, setZoomOpen] = useState(false)

  const selected = maps?.find((map) => map.venueSeatingMapId === selectedId) ?? maps?.[0]
  const isImage = selected?.contentType.startsWith('image/') ?? false
  const isPdf = selected?.contentType === 'application/pdf'
  const mapHeader = (
    <Box
      sx={{
        px: 2,
        py: 1.5,
        mb: 2,
        borderRadius: 1,
        bgcolor: 'primary.main',
        color: 'primary.contrastText',
      }}
    >
      <Typography
        variant="h5"
        component="h2"
        sx={{ fontFamily: theme.typography.h2.fontFamily, lineHeight: 1.2 }}
      >
        Platskarta
      </Typography>
      {selected && (
        <Typography variant="body1" sx={{ mt: 0.5, fontWeight: 600 }}>
          {selected.name}
        </Typography>
      )}
    </Box>
  )

  if (!isDesktop && !isError && !maps?.length) {
    return null
  }

  if (!isDesktop && isError && !maps?.length) {
    return (
      <Alert
        severity="error"
        action={<Button color="inherit" size="small" onClick={onRetry}>Försök igen</Button>}
      >
        Platskartan kunde inte hämtas just nu.
      </Alert>
    )
  }

  const mapContent = (
    <>
      {isPending && <CircularProgress aria-label="Hämtar platskarta" />}
      {isError && (
        <Alert
          severity="error"
          action={<Button color="inherit" size="small" onClick={onRetry}>Försök igen</Button>}
        >
          Platskartan kunde inte hämtas just nu.
        </Alert>
      )}

      {maps && maps.length === 0 && (
        <Typography variant="body2" color="text.secondary">
          Ingen platskarta finns för scenen.
        </Typography>
      )}

      {selected && (
        <>
          {isImage && (
            <ButtonBase
              onClick={() => setZoomOpen(true)}
              aria-label="Förstora platskartan"
              sx={{ display: 'block', width: '100%' }}
            >
              <Box
                component="img"
                src={selected.downloadUrl}
                alt={selected.name}
                sx={{
                  width: '100%',
                  maxHeight: { xs: 'calc(100dvh - 180px)', lg: 480 },
                  objectFit: 'contain',
                  cursor: 'zoom-in',
                }}
              />
            </ButtonBase>
          )}

          {isPdf && (
            <Box
              component="iframe"
              src={selected.downloadUrl}
              title={selected.name}
              sx={{ width: '100%', height: { xs: 'calc(100dvh - 180px)', lg: 480 }, border: 0 }}
            />
          )}
        </>
      )}

      {selected && isImage && (
        <Dialog open={zoomOpen} onClose={() => setZoomOpen(false)} maxWidth="lg" fullWidth>
          <DialogTitle>{selected.name}</DialogTitle>
          <DialogContent>
            <Box
              component="img"
              src={selected.downloadUrl}
              alt={selected.name}
              sx={{ width: '100%', height: 'auto' }}
            />
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setZoomOpen(false)}>Stäng</Button>
          </DialogActions>
        </Dialog>
      )}
    </>
  )

  if (!isDesktop) {
    const mapPanelId = `venue-map-${venueId}`

    return (
      <Dialog
        id={mapPanelId}
        open={dialogOpen}
        onClose={onClose}
        fullScreen
      >
        <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <Box sx={{ flex: 1, mr: 2 }}>{mapHeader}</Box>
          <Button onClick={onClose} autoFocus sx={{ color: 'text.primary' }}>Stäng</Button>
        </DialogTitle>
        <DialogContent sx={{ flex: 1, minHeight: 0 }}>
          <Paper component="section" sx={{ p: 2 }}>
            {maps && maps.length > 1 && selected && (
              <Tabs
                value={selected.venueSeatingMapId}
                onChange={(_, value: string) => setSelectedId(value)}
                variant="scrollable"
                sx={{ mb: 1 }}
              >
                {maps.map((map) => (
                  <Tab key={map.venueSeatingMapId} value={map.venueSeatingMapId} label={map.name} />
                ))}
              </Tabs>
            )}
            {mapContent}
          </Paper>
        </DialogContent>
      </Dialog>
    )
  }

  return (
    <Paper
      component="section"
      sx={{
        p: 2,
        position: 'sticky',
        top: 24,
        maxHeight: 'calc(100vh - 48px)',
        overflow: 'auto',
      }}
    >
      {mapHeader}
      {maps && maps.length > 1 && selected && (
        <Tabs
          value={selected.venueSeatingMapId}
          onChange={(_, value: string) => setSelectedId(value)}
          variant="scrollable"
          sx={{ mb: 1 }}
        >
          {maps.map((map) => (
            <Tab key={map.venueSeatingMapId} value={map.venueSeatingMapId} label={map.name} />
          ))}
        </Tabs>
      )}
      {mapContent}
    </Paper>
  )
}
