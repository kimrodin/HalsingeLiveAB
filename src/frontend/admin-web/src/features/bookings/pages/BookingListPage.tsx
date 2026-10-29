import {
  Alert,
  Box,
  Button,
  CircularProgress,
  MenuItem,
  Pagination,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TableSortLabel,
  TextField,
  Typography,
} from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { getBookings } from '../api/bookings';
import type { Booking } from '../types';

const pageSize = 10;

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
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [sortBy, setSortBy] = useState('createdAt');
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('desc');
  const [page, setPage] = useState(1);
  const { data, isPending, isError, error } = useQuery({
    queryKey: ['bookings', { search, status, from, to, sortBy, sortDirection, page }],
    queryFn: () => getBookings({
      search: search.trim(),
      status: status || undefined,
      from: from || undefined,
      to: to || undefined,
      sortBy,
      sortDirection,
      page,
      pageSize,
    }),
  });
  const bookings = data?.bookings ?? [];
  const setFilter = (update: () => void) => {
    update();
    setPage(1);
  };
  const toggleSort = (field: string) => {
    if (sortBy === field) setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
    else {
      setSortBy(field);
      setSortDirection('asc');
    }
    setPage(1);
  };

  return (
    <>
      <Box sx={{ mb: 2 }}>
        <Typography variant="h4">Bokningar</Typography>
      </Box>

      <Paper sx={{ p: 2, mb: 2 }}>
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: {
              xs: 'minmax(180px, 2fr) minmax(110px, 1fr) minmax(140px, 1fr) minmax(140px, 1fr) auto',
              md: 'minmax(220px, 2.5fr) minmax(130px, 1fr) minmax(150px, 1fr) minmax(150px, 1fr) auto',
            },
            gap: 2,
            alignItems: 'center',
            overflowX: 'auto',
            overflowY: 'visible',
            py: 1,
          }}
        >
          <TextField
            label="Sök bokningsnummer, e-post, namn eller evenemang"
            value={search}
            fullWidth
            onChange={(event) => setFilter(() => setSearch(event.target.value))}
          />

          <TextField
            select
            label="Status"
            value={status}
            fullWidth
            onChange={(event) => setFilter(() => setStatus(event.target.value))}
          >
            <MenuItem value="">Alla statusar</MenuItem>
            {data?.statuses.map((value) => (
              <MenuItem key={value} value={value}>
                {statusLabel(value)}
              </MenuItem>
            ))}
          </TextField>

          <TextField
            label="Från datum"
            type="date"
            value={from}
            fullWidth
            onChange={(event) => setFilter(() => setFrom(event.target.value))}
            slotProps={{ inputLabel: { shrink: true } }}
          />

          <TextField
            label="Till datum"
            type="date"
            value={to}
            fullWidth
            onChange={(event) => setFilter(() => setTo(event.target.value))}
            slotProps={{ inputLabel: { shrink: true } }}
          />

          <Button
            onClick={() => {
              setSearch('');
              setStatus('');
              setFrom('');
              setTo('');
              setPage(1);
            }}
            sx={{ whiteSpace: 'nowrap' }}
          >
            Rensa
          </Button>
        </Box>
      </Paper>

      {isPending && <CircularProgress />}
      {isError && <Alert severity="error">Kunde inte hämta bokningar. {error.message}</Alert>}
      {data && data.totalCount === 0 && <Alert severity="info">Det finns inga bokningar som matchar sökningen.</Alert>}

      {data && bookings.length > 0 && (
        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell><TableSortLabel active={sortBy === 'bookingId'} direction={sortBy === 'bookingId' ? sortDirection : 'asc'} onClick={() => toggleSort('bookingId')}>Bokningsnummer</TableSortLabel></TableCell>
                <TableCell><TableSortLabel active={sortBy === 'customer'} direction={sortBy === 'customer' ? sortDirection : 'asc'} onClick={() => toggleSort('customer')}>Kund</TableSortLabel></TableCell>
                <TableCell>Evenemang</TableCell>
                <TableCell><TableSortLabel active={sortBy === 'createdAt'} direction={sortBy === 'createdAt' ? sortDirection : 'asc'} onClick={() => toggleSort('createdAt')}>Skapad</TableSortLabel></TableCell>
                <TableCell>Reservation går ut</TableCell>
                <TableCell><TableSortLabel active={sortBy === 'status'} direction={sortBy === 'status' ? sortDirection : 'asc'} onClick={() => toggleSort('status')}>Status</TableSortLabel></TableCell>
                <TableCell>Betalning</TableCell>
                <TableCell align="right"><TableSortLabel active={sortBy === 'totalPrice'} direction={sortBy === 'totalPrice' ? sortDirection : 'asc'} onClick={() => toggleSort('totalPrice')}>Summa</TableSortLabel></TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {bookings.map((booking: Booking) => (
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
      {data && data.totalCount > pageSize && (
        <Pagination
          count={Math.ceil(data.totalCount / pageSize)}
          page={page}
          onChange={(_, value) => setPage(value)}
          sx={{ display: 'flex', justifyContent: 'center', mt: 2 }}
        />
      )}
    </>
  );
}
