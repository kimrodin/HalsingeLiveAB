import { useEffect, useState } from 'react';
import { Alert, Box, Button, Paper, Stack, TextField, Typography } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { request } from '../../../shared/api/http';
import type { EventDetails } from '../types';

export default function EventImageManager({ event }: { event: EventDetails }) {
  const queryClient = useQueryClient();
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [name, setName] = useState(event.name);
  const [notice, setNotice] = useState('');
  const [failedUrl, setFailedUrl] = useState<string | null>(null);
  useEffect(() => {
    if (!preview) return;
    return () => URL.revokeObjectURL(preview);
  }, [preview]);
  const mutation = useMutation({
    mutationFn: async (action: 'upload' | 'delete') => {
      if (action === 'delete') return request(`/DeleteEventImage/${event.eventId}`, { method: 'DELETE' });
      if (!file) throw new Error('Välj en bild.');
      const data = new FormData();
      data.append('Name', name.trim());
      data.append('File', file);
      await request(`/UploadEventImage/${event.eventId}`, { method: 'POST', body: data });
    },
    onSuccess: async (_, action) => {
      setFile(null);
      setPreview(null);
      setNotice(action === 'upload' ? 'Evenemangsbilden har sparats.' : 'Evenemangsbilden har tagits bort.');
      await queryClient.invalidateQueries({ queryKey: ['events'] });
    },
  });
  const shownUrl = file ? preview : event.imageUrl;
  const validFile = file && ['image/png', 'image/jpeg'].includes(file.type) && file.size > 0 && file.size <= 10 * 1024 * 1024;
  return (
    <Paper sx={{ p: 3, mb: 2 }}>
      <Stack spacing={2}>
        <Typography component="h2" variant="h6">Evenemangsbild</Typography>
        <Typography variant="body2" color="text.secondary">Samma poster används i dashboarden och kundwebben. PNG eller JPEG, högst 10 MB.</Typography>
        {shownUrl && shownUrl !== failedUrl && <Box component="img" src={shownUrl} alt={name || event.name}
          onError={() => setFailedUrl(shownUrl)} sx={{ width: '100%', maxHeight: 260, objectFit: 'contain', borderRadius: 1 }} />}
        {shownUrl && shownUrl === failedUrl && <Alert severity="warning">Bilden kunde inte visas.</Alert>}
        <TextField label="Bildnamn" value={name} onChange={(e) => setName(e.target.value)}
          disabled={mutation.isPending} slotProps={{ htmlInput: { maxLength: 200 } }} />
        <Button component="label" variant="outlined" disabled={mutation.isPending}>
          Välj bild
          <input hidden type="file" accept="image/png,image/jpeg" onChange={(e) => {
            const selectedFile = e.target.files?.[0] ?? null;
            setFile(selectedFile);
            setPreview(selectedFile ? URL.createObjectURL(selectedFile) : null);
            setFailedUrl(null);
            setNotice(''); mutation.reset(); e.target.value = '';
          }} />
        </Button>
        {file && <Typography variant="body2">{file.name}</Typography>}
        {file && !validFile && <Alert severity="error">Välj en PNG- eller JPEG-bild på högst 10 MB.</Alert>}
        {mutation.isError && <Alert severity="error">{mutation.error.message}</Alert>}
        {notice && <Alert severity="success">{notice}</Alert>}
        <Stack direction="row" spacing={1}>
          <Button variant="contained" disabled={!validFile || !name.trim() || mutation.isPending}
            onClick={() => { setNotice(''); mutation.mutate('upload'); }}>{mutation.isPending ? 'Arbetar…' : 'Spara bild'}</Button>
          {event.imageUrl && <Button color="error" disabled={mutation.isPending} onClick={() => {
            if (window.confirm('Ta bort evenemangets bild?')) { setNotice(''); mutation.mutate('delete'); }
          }}>Ta bort bild</Button>}
        </Stack>
      </Stack>
    </Paper>
  );
}
