import { useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { useNavigate } from 'react-router'
import { ApiError } from '../../../shared/api/http'
import { createBooking, type EventInventory } from '../api/bookings'

interface CheckoutDialogProps {
  eventId: string
  eventName: string
  inventory: EventInventory
  sectionName: string
  quantity: number
  open: boolean
  onClose: () => void
}

const priceFormatter = new Intl.NumberFormat('sv-SE', {
  style: 'currency',
  currency: 'SEK',
  maximumFractionDigits: 2,
})

export default function CheckoutDialog({
  eventId,
  eventName,
  inventory,
  sectionName,
  quantity,
  open,
  onClose,
}: CheckoutDialogProps) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [customerName, setCustomerName] = useState('')
  const [customerEmail, setCustomerEmail] = useState('')
  const bookingMutation = useMutation({
    mutationFn: createBooking,
    onSuccess: async (booking) => {
      await queryClient.invalidateQueries({ queryKey: ['booking-inventory', eventId] })
      navigate(`/bookings/${booking.bookingId}`, {
        state: { eventName, sectionName, quantity },
      })
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) {
        void queryClient.invalidateQueries({ queryKey: ['booking-inventory', eventId] })
      }
    },
  })

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    bookingMutation.mutate({
      inventoryId: inventory.inventoryId,
      quantity,
      customerName,
      customerEmail,
    })
  }

  const handleClose = () => {
    if (bookingMutation.isPending) return
    bookingMutation.reset()
    setCustomerName('')
    setCustomerEmail('')
    onClose()
  }

  const apiError = bookingMutation.error instanceof ApiError ? bookingMutation.error : null
  const errorMessage = apiError?.status === 409
    ? 'Biljetterna tog just slut. Lagerstatusen har uppdaterats, välj antal igen.'
    : apiError?.status === 404
      ? 'Biljettalternativet hittades inte längre.'
      : apiError?.message

  return (
    <Dialog open={open} onClose={handleClose} fullWidth maxWidth="sm">
      <Box component="form" onSubmit={handleSubmit}>
        <DialogTitle>Uppgifter för bokningen</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <Typography>
              {eventName} · {sectionName} · {quantity} {quantity === 1 ? 'biljett' : 'biljetter'}
            </Typography>
            <Typography sx={{ fontWeight: 700 }}>
              Totalt {priceFormatter.format(inventory.price * quantity)}
            </Typography>
            {bookingMutation.isError && !apiError?.errors && (
              <Alert severity="error">
                {errorMessage ?? 'Bokningen kunde inte skapas. Försök igen.'}
              </Alert>
            )}
            <TextField
              label="Namn"
              name="name"
              autoComplete="name"
              value={customerName}
              onChange={(event) => setCustomerName(event.target.value)}
              required
              fullWidth
              disabled={bookingMutation.isPending}
              error={Boolean(apiError?.errors?.CustomerName)}
              helperText={apiError?.errors?.CustomerName?.join(' ')}
            />
            <TextField
              label="E-post"
              name="email"
              type="email"
              autoComplete="email"
              value={customerEmail}
              onChange={(event) => setCustomerEmail(event.target.value)}
              required
              fullWidth
              disabled={bookingMutation.isPending}
              error={Boolean(apiError?.errors?.CustomerEmail)}
              helperText={apiError?.errors?.CustomerEmail?.join(' ')}
            />
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={handleClose} disabled={bookingMutation.isPending}>Avbryt</Button>
          <Button type="submit" variant="contained" disabled={bookingMutation.isPending}>
            {bookingMutation.isPending ? 'Skapar reservation…' : 'Reservera biljetter'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
