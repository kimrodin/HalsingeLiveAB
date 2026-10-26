import { useState } from 'react'
import { Alert, Button, List, ListItem, MenuItem, Skeleton, Stack, TextField, Typography } from '@mui/material'
import { useQuery } from '@tanstack/react-query'
import { ApiError } from '../../../shared/api/http'
import { getEventInventory } from '../api/bookings'
import CheckoutDialog from './CheckoutDialog'

interface EventTicketInventoryProps {
  eventId: string
  eventName: string
  sections: Array<{ sectionId: string; name: string }>
  salesOpen: boolean
}

const maximumBookingQuantity = 10
const priceFormatter = new Intl.NumberFormat('sv-SE', {
  style: 'currency',
  currency: 'SEK',
  maximumFractionDigits: 2,
})

export default function EventTicketInventory({
  eventId, eventName, sections, salesOpen,
}: EventTicketInventoryProps) {
  const inventoryQuery = useQuery({
    queryKey: ['booking-inventory', eventId],
    queryFn: ({ signal }) => getEventInventory(eventId, signal),
  })
  const [selectedInventoryId, setSelectedInventoryId] = useState<string | null>(null)
  const [selectedQuantity, setSelectedQuantity] = useState(0)
  const [checkoutOpen, setCheckoutOpen] = useState(false)

  if (inventoryQuery.isPending) return <Skeleton height={180} />

  if (inventoryQuery.isError) {
    const notFound = inventoryQuery.error instanceof ApiError && inventoryQuery.error.status === 404
    return (
      <Alert
        severity={notFound ? 'info' : 'error'}
        action={<Button color="inherit" size="small" onClick={() => void inventoryQuery.refetch()}>Försök igen</Button>}
      >
        {notFound ? 'Biljettlagret hittades inte.' : 'Biljettlagret kunde inte hämtas just nu.'}
      </Alert>
    )
  }

  const sectionNames = new Map(sections.map((section) => [section.sectionId, section.name]))
  const rows = inventoryQuery.data.filter((item) => sectionNames.has(item.sectionId))
  if (rows.length === 0) {
    return (
      <section id="biljetter">
        <Typography color="text.secondary" sx={{ mt: 1 }}>Biljetter är inte tillgängliga ännu.</Typography>
      </section>
    )
  }

  const selectedInventory = rows.find((item) => item.inventoryId === selectedInventoryId)
  const selectedSectionName = selectedInventory ? sectionNames.get(selectedInventory.sectionId) : undefined
  const selectedMaximum = selectedInventory
    ? Math.min(Math.max(selectedInventory.availableQuantity, 0), maximumBookingQuantity)
    : 0
  const canCheckout = Boolean(selectedInventory)
    && selectedQuantity > 0
    && selectedQuantity <= selectedMaximum

  return (
    <section id="biljetter">
      <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
        En bokning gäller en sektion.
      </Typography>
      <List disablePadding>
        {rows.map((item) => {
          const maxQuantity = Math.min(Math.max(item.availableQuantity, 0), maximumBookingQuantity)
          const quantity = selectedInventoryId === item.inventoryId
            ? Math.min(selectedQuantity, maxQuantity)
            : 0

          return (
            <ListItem
              key={item.inventoryId}
              disableGutters
              divider
              sx={{ gap: 2, flexWrap: 'wrap', py: 1.5 }}
            >
              <Stack spacing={0.25} sx={{ minWidth: 120, flex: 1 }}>
                <Typography sx={{ fontWeight: 600 }}>{sectionNames.get(item.sectionId)}</Typography>
                <Typography variant="body2" color="text.secondary">
                  {item.availableQuantity} {item.availableQuantity === 1 ? 'biljett' : 'biljetter'} kvar
                </Typography>
              </Stack>
              <Typography sx={{ minWidth: 90, fontWeight: 700 }}>
                {priceFormatter.format(item.price)}
              </Typography>
              <TextField
                select
                size="small"
                label="Antal"
                value={quantity}
                disabled={maxQuantity === 0 || !salesOpen}
                sx={{ width: 110 }}
                onChange={(event) => {
                  const nextQuantity = Number(event.target.value)
                  setSelectedInventoryId(nextQuantity > 0 ? item.inventoryId : null)
                  setSelectedQuantity(nextQuantity)
                }}
              >
                <MenuItem value={0}>0</MenuItem>
                {Array.from({ length: maxQuantity }, (_, index) => index + 1).map((value) => (
                  <MenuItem key={value} value={value}>{value}</MenuItem>
                ))}
              </TextField>
            </ListItem>
          )
        })}
      </List>
      {salesOpen && (
        <Button
          variant="contained"
          sx={{ mt: 2 }}
          disabled={!canCheckout}
          onClick={() => setCheckoutOpen(true)}
        >
          Fortsätt till kassan
        </Button>
      )}
      {salesOpen && selectedInventory && selectedSectionName && (
        <CheckoutDialog
          eventId={eventId}
          eventName={eventName}
          inventory={selectedInventory}
          sectionName={selectedSectionName}
          quantity={selectedQuantity}
          open={checkoutOpen}
          onClose={() => setCheckoutOpen(false)}
        />
      )}
    </section>
  )
}
