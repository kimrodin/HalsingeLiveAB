import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Alert, Box, Button, ButtonBase, CircularProgress, Dialog, DialogActions,
  DialogContent, DialogTitle, Paper, Tab, Tabs, Typography,
} from '@mui/material';
import { getSeatingMaps } from '../api/catalog';

export default function SeatingMapViewer({ venueId }: { venueId: string }) {
  // Vi sparar id:t, inte hela kartan, så att valet inte pekar på en gammal karta efter omhämtning
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [zoomOpen, setZoomOpen] = useState(false);

  // Samma nyckel som i SeatingMapsSection, så de delar cache
  const { data: maps, isPending, isError } = useQuery({
    queryKey: ['venues', venueId, 'seating-maps'],
    queryFn: () => getSeatingMaps(venueId),
  });

  // Finns inget giltigt val (t.ex. efter radering) visas första kartan
  const selected = maps?.find((m) => m.id === selectedId) ?? maps?.[0];
  const isImage = selected?.contentType.startsWith('image/') ?? false;
  const isPdf = selected?.contentType === 'application/pdf';

  return (
    <Paper sx={{ p: 2 }}>
      <Typography variant="h6" sx={{ mb: 1 }}>Platskarta</Typography>

      {isPending && <CircularProgress />}
      {isError && <Alert severity="error">Kunde inte hämta platskartan.</Alert>}

      {maps && maps.length === 0 && (
        <Typography variant="body2" color="text.secondary">
          Ingen platskarta uppladdad ännu. Ladda upp en under formuläret.
        </Typography>
      )}

      {maps && maps.length > 1 && selected && (
        <Tabs
          value={selected.id}
          onChange={(_, value: string) => setSelectedId(value)}
          variant="scrollable"
          sx={{ mb: 1 }}
        >
          {maps.map((m) => (
            <Tab key={m.id} value={m.id} label={m.name} />
          ))}
        </Tabs>
      )}

      {selected && (
        <>
          {maps && maps.length === 1 && (
            <Typography variant="subtitle2" sx={{ mb: 1 }}>{selected.name}</Typography>
          )}

          {isImage && (
            <ButtonBase
              onClick={() => setZoomOpen(true)}
              aria-label="Förstora platskartan"
              sx={{ display: 'block', width: '100%' }}
            >
              <Box
                component="img"
                src={selected.url}
                alt={selected.name}
                sx={{ width: '100%', maxHeight: 360, objectFit: 'contain', cursor: 'zoom-in' }}
              />
            </ButtonBase>
          )}

          {isPdf && (
            <Box
              component="iframe"
              src={selected.url}
              title={selected.name}
              sx={{ width: '100%', height: 400, border: 0 }}
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
              src={selected.url}
              alt={selected.name}
              sx={{ width: '100%', height: 'auto' }}
            />
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setZoomOpen(false)}>Stäng</Button>
          </DialogActions>
        </Dialog>
      )}
    </Paper>
  );
}