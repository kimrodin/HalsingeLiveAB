import { useRef, useState } from 'react';
import type { ChangeEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Box, Button, CircularProgress, Dialog, DialogActions, DialogContent,
  DialogTitle, Link, Paper, Stack, TextField, Typography,
} from '@mui/material';
import { deleteSeatingMap, getSeatingMaps, uploadSeatingMap } from '../api/seatingMaps';
import type { UploadSeatingMapRequest, VenueSeatingMap } from '../types';
import { validateSeatingMapFile } from '../utils/validateSeatingMapFile';

interface UploadErrors {
  name?: string;
  file?: string;
}

export default function SeatingMapsSection({ venueId }: { venueId: string }) {
  const queryClient = useQueryClient();
  const queryKey = ['venues', venueId, 'seating-maps'];
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [name, setName] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [errors, setErrors] = useState<UploadErrors>({});

  const [mapToDelete, setMapToDelete] = useState<VenueSeatingMap | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);

  const { data: maps, isPending, isError } = useQuery({
    queryKey,
    queryFn: () => getSeatingMaps(venueId),
  });

  const uploadMutation = useMutation({
    mutationFn: (data: UploadSeatingMapRequest) => uploadSeatingMap(venueId, data),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey });
      setName('');
      setFile(null);
      setErrors({});
      if (fileInputRef.current) fileInputRef.current.value = '';
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (seatingMapId: string) => deleteSeatingMap(venueId, seatingMapId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey });
      setDialogOpen(false);
    },
  });

  const handleFileChange = (e: ChangeEvent<HTMLInputElement>) => {
    const selected = e.target.files?.[0] ?? null;
    setFile(selected);
    setErrors((prev) => ({
      ...prev,
      file: selected ? (validateSeatingMapFile(selected) ?? undefined) : undefined,
    }));
  };

  const handleUpload = () => {
    const found: UploadErrors = {};
    if (!name.trim()) found.name = 'Namn krävs';
    if (!file) found.file = 'Välj en fil';
    else {
      const fileError = validateSeatingMapFile(file);
      if (fileError) found.file = fileError;
    }
    setErrors(found);
    if (Object.keys(found).length > 0 || !file) return;

    uploadMutation.mutate({ name: name.trim(), file });
  };

  return (
    <Paper sx={{ p: 3, mt: 3, maxWidth: 900 }}>
      <Stack spacing={2}>
        <Typography variant="h6">Platskartor</Typography>

        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ alignItems: 'flex-start' }}>
          <TextField
            label="Namn på platskarta" fullWidth value={name}
            onChange={(e) => setName(e.target.value)}
            error={!!errors.name} helperText={errors.name}
          />
          <Box>
            <Button component="label" variant="outlined" sx={{ height: 56, whiteSpace: 'nowrap' }}>
              Välj fil
              <input
                ref={fileInputRef}
                type="file"
                hidden
                accept="image/png,image/jpeg,application/pdf"
                onChange={handleFileChange}
              />
            </Button>
          </Box>
        </Stack>

        <Typography variant="body2" color={errors.file ? 'error' : 'text.secondary'}>
          {errors.file ?? (file ? file.name : 'Ingen fil vald (PNG, JPG eller PDF, max 5 MB)')}
        </Typography>

        {uploadMutation.isError && (
          <Alert severity="error">Kunde inte ladda upp platskartan. Försök igen.</Alert>
        )}

        <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
          <Button
            variant="contained"
            onClick={handleUpload}
            disabled={uploadMutation.isPending}
          >
            {uploadMutation.isPending ? 'Laddar upp…' : 'Ladda upp'}
          </Button>
        </Box>

        {isPending && <CircularProgress />}
        {isError && <Alert severity="error">Kunde inte hämta platskartor.</Alert>}
        {maps && maps.length === 0 && (
          <Typography variant="body2" color="text.secondary">
            Inga platskartor har lagts till ännu.
          </Typography>
        )}

        {maps?.map((map) => (
          <Paper key={map.venueSeatingMapId} variant="outlined" sx={{ p: 2 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
              {map.contentType.startsWith('image/') && (
                <Box
                  component="img"
                  src={map.downloadUrl}
                  alt={map.name}
                  sx={{ height: 64, maxWidth: 120, objectFit: 'contain', borderRadius: 1 }}
                />
              )}
              <Box sx={{ flexGrow: 1 }}>
                <Typography variant="subtitle1">{map.name}</Typography>
                <Typography variant="body2" color="text.secondary">{map.fileName}</Typography>
                {map.contentType === 'application/pdf' && (
                  <Link href={map.downloadUrl} target="_blank" rel="noopener">Öppna PDF</Link>
                )}
              </Box>
              <Button
                color="error"
                onClick={() => {
                  deleteMutation.reset();
                  setMapToDelete(map);
                  setDialogOpen(true);
                }}
              >
                Ta bort
              </Button>
            </Box>
          </Paper>
        ))}
      </Stack>

      <Dialog
        open={dialogOpen}
        onClose={() => {
          if (!deleteMutation.isPending) setDialogOpen(false);
        }}
      >
        <DialogTitle>Ta bort platskarta?</DialogTitle>
        <DialogContent>
          Är du säker på att du vill ta bort "{mapToDelete?.name}"?
          {deleteMutation.isError && (
            <Alert severity="error" sx={{ mt: 2 }}>
              Kunde inte ta bort platskartan.
            </Alert>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} disabled={deleteMutation.isPending}>
            Avbryt
          </Button>
          <Button
            color="error"
            variant="contained"
            disabled={deleteMutation.isPending}
            onClick={() => {
              if (mapToDelete) deleteMutation.mutate(mapToDelete.venueSeatingMapId);
            }}
          >
            Ta bort
          </Button>
        </DialogActions>
      </Dialog>
    </Paper>
  );
}
