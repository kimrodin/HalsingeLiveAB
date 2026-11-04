import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  MenuItem,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { BrowserQRCodeReader, type IScannerControls } from '@zxing/browser';
import { getEvents } from '../../events/api/events';
import { ApiError } from '../../../shared/api/http';
import { checkInTicket } from '../../bookings/api/bookings';

type ScanFeedback = {
  severity: 'success' | 'error' | 'info';
  message: string;
};

type ScanSource = 'camera' | 'manual';

const ticketIdPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function getCheckInErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 404)
      return 'Biljetten hittades inte.';

    if (error.status === 409) {
      const details = error.message.toLowerCase();
      if (details.includes('different event'))
        return 'Biljetten gäller ett annat event.';
      if (details.includes('checkedin'))
        return 'Biljetten är redan incheckad.';
      if (details.includes('cancelled'))
        return 'Biljetten är avbokad.';
      return 'Biljetten kan inte checkas in.';
    }

    if (error.status === 400)
      return 'Kontrollera att ett event är valt och att biljett-ID:t är giltigt.';

    if (error.status >= 500)
      return 'Ett tekniskt fel uppstod. Kontrollera anslutningen och försök igen.';

    return error.message;
  }

  return 'Ett tekniskt fel uppstod. Kontrollera anslutningen och försök igen.';
}

export default function TicketScannerPage() {
  const [eventId, setEventId] = useState('');
  const [manualTicketId, setManualTicketId] = useState('');
  const [feedback, setFeedback] = useState<ScanFeedback | null>(null);
  const [cameraActive, setCameraActive] = useState(false);
  const [scanSource, setScanSource] = useState<ScanSource>('manual');
  const videoRef = useRef<HTMLVideoElement>(null);
  const controlsRef = useRef<IScannerControls | null>(null);
  const scanLockedRef = useRef(false);
  const cameraSessionRef = useRef(0);

  const eventsQuery = useQuery({
    queryKey: ['events', 'ticket-scanner'],
    queryFn: () => getEvents({ endsAfter: new Date().toISOString(), page: 1, pageSize: 100 }),
  });

  const checkInMutation = useMutation({
    mutationFn: ({ ticketId, selectedEventId }: { ticketId: string; selectedEventId: string }) =>
      checkInTicket(ticketId, selectedEventId),
    onSuccess: () => {
      setManualTicketId('');
      setFeedback({ severity: 'success', message: 'Biljetten är godkänd. Besökaren är insläppt.' });
    },
    onError: (error: unknown) => {
      setFeedback({ severity: 'error', message: getCheckInErrorMessage(error) });
    },
  });

  const stopCamera = () => {
    cameraSessionRef.current += 1;
    controlsRef.current?.stop();
    controlsRef.current = null;
    setCameraActive(false);
  };

  useEffect(() => () => {
    cameraSessionRef.current += 1;
    controlsRef.current?.stop();
    controlsRef.current = null;
  }, []);

  const processTicketId = async (rawTicketId: string, source: ScanSource) => {
    if (scanLockedRef.current)
      return;

    scanLockedRef.current = true;
    setScanSource(source);
    stopCamera();

    const ticketId = rawTicketId.trim();
    if (!ticketIdPattern.test(ticketId)) {
      setFeedback({
        severity: 'error',
        message: source === 'camera'
          ? 'QR-koden innehåller inget giltigt biljett-ID.'
          : 'Ange ett giltigt biljett-ID.',
      });
      return;
    }

    if (!eventId) {
      setFeedback({ severity: 'error', message: 'Välj event innan du checkar in en biljett.' });
      return;
    }

    setFeedback({ severity: 'info', message: 'Kontrollerar biljetten…' });
    try {
      await checkInMutation.mutateAsync({ ticketId, selectedEventId: eventId });
    } catch {
      // The mutation displays a localized result in onError.
    }
  };

  const startCamera = async () => {
    if (!eventId) {
      setFeedback({ severity: 'error', message: 'Välj event innan du startar skannern.' });
      return;
    }

    if (!navigator.mediaDevices?.getUserMedia || !videoRef.current) {
      setFeedback({
        severity: 'error',
        message: 'Kameran är inte tillgänglig. Tillåt kameraåtkomst eller skriv in biljett-ID:t manuellt.',
      });
      return;
    }

    scanLockedRef.current = false;
    const cameraSession = cameraSessionRef.current + 1;
    cameraSessionRef.current = cameraSession;
    setFeedback(null);
    setCameraActive(true);

    try {
      const reader = new BrowserQRCodeReader();
      const controls = await reader.decodeFromVideoDevice(undefined, videoRef.current, (result) => {
        if (result)
          void processTicketId(result.getText(), 'camera');
      });

      if (cameraSession !== cameraSessionRef.current || scanLockedRef.current) {
        controls.stop();
        if (cameraSession === cameraSessionRef.current)
          setCameraActive(false);
      } else {
        controlsRef.current = controls;
      }
    } catch {
      if (cameraSession !== cameraSessionRef.current)
        return;
      setCameraActive(false);
      scanLockedRef.current = false;
      setFeedback({
        severity: 'error',
        message: 'Kunde inte starta kameran. Kontrollera kameraåtkomst och försök igen, eller skriv in biljett-ID:t manuellt.',
      });
    }
  };

  const handleEventChange = (nextEventId: string) => {
    stopCamera();
    scanLockedRef.current = false;
    setEventId(nextEventId);
    setFeedback(null);
  };

  const submitManualTicket = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    void processTicketId(manualTicketId, 'manual');
  };

  const continueScanning = () => {
    const source = scanSource;
    scanLockedRef.current = false;
    setFeedback(null);
    setManualTicketId('');
    if (source === 'camera')
      void startCamera();
  };

  const events = eventsQuery.data?.events ?? [];

  return (
    <Stack spacing={2} sx={{ maxWidth: 760, mx: 'auto' }}>
      <Box>
        <Typography variant="h4">Entréskanner</Typography>
        <Typography color="text.secondary">
          Välj event och skanna biljettens QR-kod, eller skriv in biljett-ID:t.
        </Typography>
      </Box>

      <Paper sx={{ p: { xs: 2, sm: 3 } }}>
        <Stack spacing={2}>
          {eventsQuery.isLoading && <CircularProgress size={28} />}
          {eventsQuery.isError && (
            <Alert severity="error">Kunde inte hämta event. Kontrollera anslutningen och försök igen.</Alert>
          )}
          {!eventsQuery.isLoading && !eventsQuery.isError && events.length === 0 && (
            <Alert severity="info">Det finns inga pågående eller kommande event att välja.</Alert>
          )}

          {events.length > 0 && (
            <TextField
              select
              required
              label="Event"
              value={eventId}
              disabled={checkInMutation.isPending}
              onChange={(change) => handleEventChange(change.target.value)}
            >
              {events.map((event) => (
                <MenuItem key={event.eventId} value={event.eventId}>
                  {event.name} · {new Date(event.startDate).toLocaleString('sv-SE', {
                    dateStyle: 'medium', timeStyle: 'short',
                  })}
                </MenuItem>
              ))}
            </TextField>
          )}

          <Box>
            <video
              ref={videoRef}
              muted
              playsInline
              aria-label="Kamerabild för QR-skanning"
              style={{
                display: cameraActive ? 'block' : 'none',
                width: '100%',
                maxHeight: 420,
                objectFit: 'cover',
                borderRadius: 8,
                background: '#111',
              }}
            />
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mt: cameraActive ? 1 : 0 }}>
              <Button
                variant="contained"
                onClick={() => void startCamera()}
                disabled={!eventId || cameraActive || checkInMutation.isPending}
              >
                Starta kamera
              </Button>
              <Button onClick={stopCamera} disabled={!cameraActive}>
                Stäng kamera
              </Button>
            </Stack>
          </Box>

          <Box component="form" onSubmit={submitManualTicket}>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
              <TextField
                fullWidth
                label="Biljett-ID"
                value={manualTicketId}
                onChange={(change) => setManualTicketId(change.target.value)}
                disabled={!eventId || checkInMutation.isPending}
                autoComplete="off"
                placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
              />
              <Button
                type="submit"
                variant="outlined"
                disabled={!eventId || !manualTicketId.trim() || checkInMutation.isPending}
              >
                Checka in
              </Button>
            </Stack>
          </Box>

          {checkInMutation.isPending && (
            <Alert severity="info" icon={<CircularProgress size={18} />}>
              Kontrollerar biljett…
            </Alert>
          )}
          {feedback && !checkInMutation.isPending && (
            <Alert severity={feedback.severity}>{feedback.message}</Alert>
          )}
          {feedback && !checkInMutation.isPending && (
            <Button onClick={continueScanning}>
              {scanSource === 'camera' ? 'Skanna nästa biljett' : 'Nästa biljett'}
            </Button>
          )}
        </Stack>
      </Paper>
    </Stack>
  );
}
