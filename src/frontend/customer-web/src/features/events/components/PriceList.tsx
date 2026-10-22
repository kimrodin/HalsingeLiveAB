import { List, ListItem, ListItemText, Typography } from '@mui/material'

interface PriceListProps {
  sectionPrices: Array<{ sectionId: string; price: number }>
  sections: Array<{ sectionId: string; name: string }>
}

const formatter = new Intl.NumberFormat('sv-SE', {
  style: 'currency',
  currency: 'SEK',
  maximumFractionDigits: 2,
})

export default function PriceList({ sectionPrices, sections }: PriceListProps) {
  const sectionNames = new Map(sections.map((section) => [section.sectionId, section.name]))

  return (
    <section id="biljetter">
      <Typography variant="h5" component="h2" sx={{ fontWeight: 700 }}>Priser</Typography>
      {sectionPrices.length === 0 ? (
        <Typography color="text.secondary" sx={{ mt: 1 }}>Inga priser tillgängliga.</Typography>
      ) : (
        <List disablePadding>
          {sectionPrices.map(({ sectionId, price }) => (
            <ListItem key={sectionId} disableGutters divider>
              <ListItemText primary={sectionNames.get(sectionId) ?? 'Sektion'} />
              <Typography sx={{ fontWeight: 700 }}>{formatter.format(price)}</Typography>
            </ListItem>
          ))}
        </List>
      )}
    </section>
  )
}
