import {
  Alert,
  Box,
  CircularProgress,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { getBookings } from '../api/bookings';
import type { Booking } from '../types';

const formatDate = (value: string | null) => value
  ? new Date(value).toLocaleString('sv-SE', { dateStyle: 'medium', timeStyle: 'short' })
  : '—';

const formatMoney = (amount: number, currency: string | null | undefined) => {
  const currencyCode = currency?.trim().toUpperCase() ?? '';
  try {
    if (!currencyCode) throw new RangeError('Currency code is empty.');
    return new Intl.NumberFormat('sv-SE', { style: 'currency', currency: currencyCode }).format(amount);
  } catch {
    const formattedAmount = new Intl.NumberFormat('sv-SE', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(amount);
    return `${formattedAmount} ${currencyCode || '(okänd valuta)'}`;
  }
};

function paymentLabel(booking: Booking) {
  if (!booking.paymentRequested) return 'Inte påbörjad';
  return booking.paymentSettled ? 'Avstämd' : 'Väntar på avstämning';
}

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
  };
  return labels[status] ?? status;
}

function bookingItemsLabel(booking: Booking) {
  return booking.items.map((item) => (
    <Box key={item.inventoryId} sx={{ mb: 0.75, '&:last-child': { mb: 0 } }}>
      <Typography variant="body2">{item.eventName} · {item.quantity} st</Typography>
      <Typography variant="caption" color="text.secondary" component="div">
        {formatDate(item.eventStartsAt)} · {item.venueName}, {item.sectionName}
      </Typography>
    </Box>
  ));
}

export default function BookingListPage() {
  const { data: bookings, isPending, isError, error } = useQuery({
    queryKey: ['bookings'],
    queryFn: getBookings,
  });

  return (
    <>
      <Box sx={{ mb: 2 }}>
        <Typography variant="h4">Bokningar</Typography>
      </Box>

      {isPending && <CircularProgress />}
      {isError && <Alert severity="error">Kunde inte hämta bokningar. {error.message}</Alert>}
      {bookings?.length === 0 && <Alert severity="info">Det finns inga bokningar.</Alert>}

      {bookings && bookings.length > 0 && (
        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Bokningsnummer</TableCell>
                <TableCell>Kund</TableCell>
                <TableCell>Evenemang</TableCell>
                <TableCell>Skapad</TableCell>
                <TableCell>Reservation går ut</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Betalning</TableCell>
                <TableCell align="right">Summa</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {bookings.map((booking) => (
                <TableRow key={booking.bookingId} hover>
                  <TableCell sx={{ fontFamily: 'monospace', whiteSpace: 'nowrap' }}>
                    {booking.bookingId}
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2">{booking.customerName}</Typography>
                    <Typography variant="caption" color="text.secondary">{booking.customerEmail}</Typography>
                  </TableCell>
                  <TableCell>{bookingItemsLabel(booking)}</TableCell>
                  <TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDate(booking.createdAt)}</TableCell>
                  <TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDate(booking.expiresAt)}</TableCell>
                  <TableCell>{statusLabel(booking.status)}</TableCell>
                  <TableCell>{paymentLabel(booking)}</TableCell>
                  <TableCell align="right" sx={{ whiteSpace: 'nowrap' }}>
                    {formatMoney(booking.totalPrice, booking.items[0]?.currency ?? 'SEK')}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </>
  );
}
