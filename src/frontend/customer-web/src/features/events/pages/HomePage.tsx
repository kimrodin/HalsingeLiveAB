import { Stack, Typography } from '@mui/material'

export default function HomePage() {
  return (
    <Stack spacing={2}>
      <Typography variant="h3" component="h1" sx={{ maxWidth: 620, fontWeight: 700, letterSpacing: '-0.035em' }}>
        Upplev Hälsingland tillsammans
      </Typography>
      <Typography color="text.secondary">
        Här samlar vi kommande evenemang. Evenemangslistan blir nästa steg.
      </Typography>
    </Stack>
  )
}
