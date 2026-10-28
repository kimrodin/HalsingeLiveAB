import { useState } from 'react'
import { useLocation, useNavigate, useParams } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, CircularProgress, Divider, Skeleton, Stack, Typography } from '@mui/material'
import { ApiError } from '../../../shared/api/http'
import { useNow } from '../../../shared/hooks/useNow'
import { cancelBooking, getBooking, getTicketPdf, getTicketsForBooking, startPayment } from '../api/bookings'

interface BookingLocationState {
  eventId?: string
  eventName?: string
  sectionName?: string
  quantity?: number
}

const priceFormatter = new Intl.NumberFormat('sv-SE', {
  style: 'currency',
  currency: 'SEK',
  maximumFractionDigits: 2,
})

function formatRemainingTime(expiresAt: string, now: number): string {
  const totalSeconds = Math.max(0, Math.ceil((new Date(expiresAt).getTime() - now) / 1000))
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes}:${String(seconds).padStart(2, '0')}`
}

export default function BookingPage() {
  const { bookingId } = useParams<{ bookingId: string }>()
  const location = useLocation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const now = useNow(1000)
  const [pdfError, setPdfError] = useState<string | null>(null)
  const guestAccessToken = bookingId
    ? sessionStorage.getItem(`booking-access:${bookingId}`)
    : null
  const state = (location.state ?? {}) as BookingLocationState
  const bookingQuery = useQuery({
    queryKey: ['booking', bookingId],
    queryFn: ({ signal }) => getBooking(bookingId!, guestAccessToken!, signal),
    enabled: Boolean(bookingId && guestAccessToken),
    refetchInterval: (query) => {
      const booking = query.state.data
      if (!booking) return false
      if (['Confirming', 'Cancelling', 'Expiring'].includes(booking.status)) return 1000
      // Payment reconciliation may finish after a lost HTTP response or page reload.
      if (booking.status === 'Reserved') return 2000
      return false
    },
  })
  const ticketsQuery = useQuery({
    queryKey: ['tickets', bookingId],
    queryFn: ({ signal }) => getTicketsForBooking(bookingId!, guestAccessToken!, signal),
    enabled: Boolean(bookingId && guestAccessToken && bookingQuery.data?.status === 'Confirmed'),
    refetchInterval: (query) => query.state.data?.length ? false : 1500,
  })
  const invalidateBooking = () => queryClient.invalidateQueries({ queryKey: ['booking', bookingId] })
  const refreshAfterActionError = (error: Error) => {
    if (error instanceof ApiError && (error.status === 404 || error.status === 409)) {
      void invalidateBooking()
    }
  }
  const paymentMutation = useMutation({
    mutationFn: () => startPayment(bookingId!, guestAccessToken!),
    onSuccess: invalidateBooking,
    onError: refreshAfterActionError,
  })
  const cancelMutation = useMutation({
    mutationFn: () => cancelBooking(bookingId!, guestAccessToken!),
    onSuccess: invalidateBooking,
    onError: refreshAfterActionError,
  })

  if (!bookingId) return <Alert severity="info">Bokningen hittades inte.</Alert>
  if (!guestAccessToken) {
    return <Alert severity="info">Åtkomst till bokningen saknas på den här enheten.</Alert>
  }

  if (bookingQuery.isPending) {
    return (
      <Stack spacing={2} role="status" aria-busy="true" aria-label="Hämtar bokning">
        <Skeleton height={48} />
        <Skeleton height={160} />
      </Stack>
    )
  }

  if (bookingQuery.isError) {
    const notFound = bookingQuery.error instanceof ApiError && bookingQuery.error.status === 404
    return (
      <Alert
        severity={notFound ? 'info' : 'error'}
        action={!notFound && <Button color="inherit" size="small" onClick={() => void bookingQuery.refetch()}>Försök igen</Button>}
      >
        {notFound ? 'Bokningen hittades inte.' : 'Bokningen kunde inte hämtas just nu.'}
      </Alert>
    )
  }

  const booking = bookingQuery.data
  const actionError = paymentMutation.error ?? cancelMutation.error
  const actionErrorStatus = actionError instanceof ApiError ? actionError.status : undefined
  const actionMessage = actionErrorStatus === 404
    ? 'Bokningen hittades inte längre.'
    : actionErrorStatus === 409
      ? 'Bokningens status har ändrats. Statusen har uppdaterats.'
      : actionError instanceof ApiError
        ? actionError.message
        : actionError
          ? 'Åtgärden kunde inte genomföras. Försök igen.'
          : null
  const isPending = paymentMutation.isPending || cancelMutation.isPending
  const isTransitioning = ['Confirming', 'Cancelling', 'Expiring'].includes(booking.status)
  const isReserved = booking.status === 'Reserved'
  const isPaymentWaiting = isReserved && (paymentMutation.isPending
    || ['Pending', 'Unknown'].includes(paymentMutation.data?.paymentStatus ?? ''))
  const isExpiredByTime = Boolean(booking.expiresAt && new Date(booking.expiresAt).getTime() <= now)
  const handleTicketPdf = async (ticketId: string, download: boolean) => {
    setPdfError(null)
    const pdfWindow = download ? null : window.open('about:blank', '_blank')
    try {
      const pdf = await getTicketPdf(bookingId, ticketId, guestAccessToken, download)
      const objectUrl = URL.createObjectURL(pdf)
      if (download) {
        const anchor = document.createElement('a')
        anchor.href = objectUrl
        anchor.download = `ticket-${ticketId}.pdf`
        anchor.click()
      } else if (pdfWindow) {
        pdfWindow.location.href = objectUrl
      } else {
        throw new Error('PDF-fönstret kunde inte öppnas. Tillåt popup-fönster och försök igen.')
      }
      window.setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000)
    } catch (error) {
      pdfWindow?.close()
      setPdfError(error instanceof Error ? error.message : 'PDF-filen kunde inte hämtas.')
    }
  }

  const statusContent: Record<string, { severity: 'success' | 'info' | 'warning' | 'error'; message: string }> = {
    Confirmed: { severity: 'success', message: 'Bokningen är bekräftad.' },
    Cancelled: { severity: 'info', message: 'Bokningen är avbokad.' },
    Expired: { severity: 'warning', message: 'Reservationen har gått ut.' },
    Failed: { severity: 'error', message: 'Bokningen kunde inte slutföras.' },
  }

  return (
    <Stack spacing={2.5}>
      <Button
        onClick={() => navigate(state.eventId ? `/events/${encodeURIComponent(state.eventId)}` : '/')}
        sx={{ alignSelf: 'flex-start' }}
      >
        Tillbaka till evenemang
      </Button>
      <Typography variant="h3" component="h1" sx={{ fontWeight: 700 }}>Din bokning</Typography>
      <Typography variant="h5" component="h2">{state.eventName ?? 'Evenemang'}</Typography>
      {state.sectionName && <Typography color="text.secondary">{state.sectionName}</Typography>}
      {booking.items.map((item) => (
        <Stack key={item.inventoryId} direction="row" sx={{ justifyContent: 'space-between' }} spacing={2}>
          <Typography>{state.quantity ?? item.quantity} {item.quantity === 1 ? 'biljett' : 'biljetter'}</Typography>
          <Typography>{priceFormatter.format(item.unitPrice * item.quantity)}</Typography>
        </Stack>
      ))}
      <Divider />
      <Stack direction="row" sx={{ justifyContent: 'space-between' }} spacing={2}>
        <Typography variant="h6">Totalt</Typography>
        <Typography variant="h6" sx={{ fontWeight: 700 }}>{priceFormatter.format(booking.totalPrice)}</Typography>
      </Stack>
      <Typography color="text.secondary">Bokningen står på {booking.customerName} · {booking.customerEmail}</Typography>

      {isPaymentWaiting && (
        <Alert severity="info" icon={<CircularProgress color="inherit" size={20} />}>
          Betalningen behandlas. Bokningens status uppdateras automatiskt när resultatet är klart.
        </Alert>
      )}
      {isTransitioning && (
        <Alert severity="info" icon={<CircularProgress color="inherit" size={20} />}>
          {booking.status === 'Confirming' ? 'Bekräftar bokningen…' : 'Uppdaterar bokningen…'}
        </Alert>
      )}
      {statusContent[booking.status] && (
        <Alert severity={statusContent[booking.status].severity}>{statusContent[booking.status].message}</Alert>
      )}
      {booking.status === 'Confirmed' && ticketsQuery.isPending && (
        <Alert severity="info" icon={<CircularProgress color="inherit" size={20} />}>
          Biljetterna skapas. De visas här automatiskt så snart de är klara.
        </Alert>
      )}
      {booking.status === 'Confirmed' && ticketsQuery.isError && (
        <Alert
          severity="warning"
          action={<Button color="inherit" size="small" onClick={() => void ticketsQuery.refetch()}>Försök igen</Button>}
        >
          Biljetterna kunde inte hämtas just nu.
        </Alert>
      )}
      {booking.status === 'Confirmed' && ticketsQuery.data?.length === 0 && (
        <Alert severity="info" icon={<CircularProgress color="inherit" size={20} />}>
          Biljetterna skapas. De visas här automatiskt så snart de är klara.
        </Alert>
      )}
      {pdfError && <Alert severity="error">{pdfError}</Alert>}
      {ticketsQuery.data && ticketsQuery.data.length > 0 && (
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="h5" component="h2">Dina biljetter</Typography>
          {ticketsQuery.data.map((ticket) => {
            return (
              <Stack
                key={ticket.ticketId}
                spacing={1}
                sx={{ p: 2, border: '1px solid', borderColor: 'divider', borderRadius: 2 }}
              >
                <Typography variant="h6">{ticket.eventName}</Typography>
                <Typography>{new Intl.DateTimeFormat('sv-SE', {
                  dateStyle: 'long',
                  timeStyle: 'short',
                  timeZone: 'Europe/Stockholm',
                }).format(new Date(ticket.eventStartsAt))}</Typography>
                <Typography color="text.secondary">
                  {ticket.venueName} · {ticket.sectionName} · Biljett {ticket.ticketNumberInBookingItem}
                </Typography>
                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
                  <Button onClick={() => void handleTicketPdf(ticket.ticketId, true)}>
                    Ladda ner PDF
                  </Button>
                  <Button onClick={() => void handleTicketPdf(ticket.ticketId, false)}>
                    Öppna / skriv ut
                  </Button>
                </Stack>
              </Stack>
            )
          })}
        </Stack>
      )}
      {isReserved && booking.expiresAt && !isExpiredByTime && (
        <Alert severity="warning">
          Reservationen gäller i {formatRemainingTime(booking.expiresAt, now)} till.
        </Alert>
      )}
      {isReserved && isExpiredByTime && (
        <Alert severity="warning">Reservationen har gått ut. Hämtar aktuell status…</Alert>
      )}
      {actionMessage && (
        <Alert severity={actionErrorStatus === 404 ? 'error' : 'warning'}>
          {actionMessage}
          {actionError instanceof ApiError && actionError.errors
            ? ` ${Object.values(actionError.errors).flat().join(' ')}`
            : ''}
        </Alert>
      )}

      {isReserved && !isExpiredByTime && (
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
          <Button
            variant="contained"
            onClick={() => { paymentMutation.reset(); cancelMutation.reset(); paymentMutation.mutate() }}
            disabled={isPending || isPaymentWaiting}
          >
            Betala och bekräfta
          </Button>
          <Button
            onClick={() => { paymentMutation.reset(); cancelMutation.reset(); cancelMutation.mutate() }}
            disabled={isPending}
          >
            Avbryt bokning
          </Button>
        </Stack>
      )}
    </Stack>
  )
}
