import { Link as RouterLink, useParams } from 'react-router';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRef, useState } from 'react';
import { getBookingDetails, getTicketPdf, updateBookingCustomer } from '../api/bookings';

const formatDate = (value: string | null | undefined) => value
  ? new Date(value).toLocaleString('sv-SE', { dateStyle: 'medium', timeStyle: 'short' })
  : '—';

const formatMoney = (amount: number, currency: string) =>
  new Intl.NumberFormat('sv-SE', { style: 'currency', currency }).format(amount);

function statusLabel(status: string) {
  const labels: Record<string, string> = {
    Reserved: 'Reserverad',
    Confirming: 'Bekräftas',
    Expiring: 'Frisläpps',
    Confirmed: 'Bekräftad',
    Cancelling: 'Avbokas',
    Cancelled: 'Avbokad',
    Expired: 'Utgången',
    Failed: 'Misslyckades',
    Valid: 'Giltig',
    CheckedIn: 'Insläppt',
  };
  return labels[status] ?? status;
}

export default function BookingDetailsPage() {
  const { id } = useParams();
  const queryClient = useQueryClient();
  const customerName = useRef<HTMLInputElement>(null);
  const customerEmail = useRef<HTMLInputElement>(null);
  const [ticketError, setTicketError] = useState('');
  const { data, isPending, isError, error } = useQuery({
    queryKey: ['booking', id],
    queryFn: () => {
      if (!id) throw new Error('Bokningsnummer saknas.');
      return getBookingDetails(id);
    },
    enabled: Boolean(id),
  });

  const updateCustomer = useMutation({
    mutationFn: () => {
      if (!id) throw new Error('Bokningsnummer saknas.');
      return updateBookingCustomer(id, {
        customerName: customerName.current?.value.trim() ?? '',
        customerEmail: customerEmail.current?.value.trim() ?? '',
      });
    },
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['booking', id] }),
        queryClient.invalidateQueries({ queryKey: ['bookings'] }),
      ]);
    },
  });

  const openTicket = async (ticketId: string) => {
    const ticketWindow = window.open('about:blank', '_blank');
    setTicketError('');
    try {
      if (!id) throw new Error('Bokningsnummer saknas.');
      const pdf = await getTicketPdf(id, ticketId);
      const url = URL.createObjectURL(pdf);
      if (ticketWindow) {
        ticketWindow.location.href = url;
      } else {
        const link = document.createElement('a');
        link.href = url;
        link.download = `ticket-${ticketId}.pdf`;
        link.click();
      }
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (ticketFetchError) {
      ticketWindow?.close();
      setTicketError(ticketFetchError instanceof Error
        ? ticketFetchError.message
        : 'Kunde inte öppna biljetten.');
    }
  };

  if (isPending) return <CircularProgress />;
  if (isError) return <Alert severity="error">Kunde inte hämta bokningen. {error.message}</Alert>;
  if (!data) return <Alert severity="error">Bokningen kunde inte hittas.</Alert>;

  const { booking, history, payment, tickets } = data;
  return (
    <Stack spacing={2}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 2 }}>
        <Box>
          <Typography variant="h4">Bokning</Typography>
          <Typography color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>
            {booking.bookingId}
          </Typography>
        </Box>
        <Button component={RouterLink} to="/bookings">Till bokningar</Button>
      </Box>

      <Paper sx={{ p: 2 }}>
        <Typography variant="h6" gutterBottom>Kunduppgifter</Typography>
        {updateCustomer.isError && (
          <Alert severity="error" sx={{ mb: 2 }}>{updateCustomer.error.message}</Alert>
        )}
        {updateCustomer.isSuccess && <Alert severity="success" sx={{ mb: 2 }}>Kunduppgifter sparade.</Alert>}
        <Box component="form" onSubmit={(event) => {
          event.preventDefault();
          updateCustomer.mutate();
        }} sx={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 220px), 1fr))',
          gap: 2,
          alignItems: 'start',
        }}>
          <TextField label="Namn" defaultValue={booking.customerName} inputRef={customerName} required fullWidth />
          <TextField label="E-post" type="email" defaultValue={booking.customerEmail}
            inputRef={customerEmail} required fullWidth />
          <Button
            variant="contained"
            type="submit"
            disabled={updateCustomer.isPending}
          >
            Spara kunduppgifter
          </Button>
        </Box>
      </Paper>

      <Paper sx={{ p: 2 }}>
        <Typography variant="h6" gutterBottom>Bokning och betalning</Typography>
        <Typography>Bokningsstatus: {statusLabel(booking.status)}</Typography>
        <Typography>Skapad: {formatDate(booking.createdAt)}</Typography>
        <Typography>Reservation går ut: {formatDate(booking.expiresAt)}</Typography>
        <Typography>Avbokad: {formatDate(booking.cancelledAt)}</Typography>
        <Typography>Betalningsstatus: {payment ? statusLabel(payment.status) : 'Ingen betalning'}</Typography>
        <Typography>Betalning skapad: {formatDate(payment?.createdAt)}</Typography>
        <Typography>Betalning slutförd: {formatDate(payment?.completedAt)}</Typography>
        <Typography>Betald: {formatDate(payment?.paidAt)}</Typography>
        <Typography>Återbetalad: {formatDate(payment?.refundedAt)}</Typography>
      </Paper>

      <Paper sx={{ p: 2 }}>
        <Typography variant="h6" gutterBottom>Bokningsrader</Typography>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Evenemang</TableCell>
              <TableCell>Datum och plats</TableCell>
              <TableCell>Antal</TableCell>
              <TableCell>Styckpris</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {booking.items.map((item) => (
              <TableRow key={item.inventoryId}>
                <TableCell>{item.eventName}</TableCell>
                <TableCell>
                  {formatDate(item.eventStartsAt)} · {item.venueName}, {item.sectionName}
                </TableCell>
                <TableCell>{item.quantity}</TableCell>
                <TableCell>{formatMoney(item.unitPrice, item.currency)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        <Typography align="right" sx={{ mt: 1 }}>
          Summa: {formatMoney(booking.totalPrice, booking.items[0]?.currency ?? 'SEK')}
        </Typography>
      </Paper>

      <Paper sx={{ p: 2 }}>
        <Typography variant="h6" gutterBottom>Biljetter</Typography>
        {ticketError && <Alert severity="error" sx={{ mb: 1 }}>{ticketError}</Alert>}
        {tickets.length === 0 && <Typography>Inga biljetter har utfärdats.</Typography>}
        {tickets.map((ticket) => (
          <Box key={ticket.ticketId} sx={{
            display: 'flex',
            flexWrap: 'wrap',
            justifyContent: 'space-between',
            alignItems: 'center',
            gap: 1,
            py: 1,
            borderBottom: 1,
            borderColor: 'divider',
          }}>
            <Box>
              <Typography>{ticket.eventName} · {statusLabel(ticket.status)}</Typography>
              <Typography variant="body2" color="text.secondary">
                Utfärdad: {formatDate(ticket.issuedAt)}
              </Typography>
            </Box>
            <Button onClick={() => void openTicket(ticket.ticketId)}>Öppna biljett</Button>
          </Box>
        ))}
      </Paper>

      <Paper sx={{ p: 2 }}>
        <Typography variant="h6" gutterBottom>Tidigare bokningar för kunden</Typography>
        {history.length === 0 && <Typography>Inga tidigare bokningar hittades.</Typography>}
        {history.map((item) => (
          <Box key={item.bookingId} sx={{ display: 'flex', flexWrap: 'wrap', gap: 1, py: 0.5 }}>
            <Button component={RouterLink} to={`/bookings/${item.bookingId}`}>
              {item.bookingId}
            </Button>
            <Typography>{statusLabel(item.status)} · {formatDate(item.createdAt)}</Typography>
            <Typography>{formatMoney(item.totalPrice, booking.items[0]?.currency ?? 'SEK')}</Typography>
          </Box>
        ))}
      </Paper>
    </Stack>
  );
}
