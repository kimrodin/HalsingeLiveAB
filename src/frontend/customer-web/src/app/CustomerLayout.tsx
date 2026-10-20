import { Box, Container, Link, Typography } from '@mui/material'
import { Link as RouterLink, Outlet } from 'react-router'

export default function CustomerLayout() {
  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'flex',
        flexDirection: 'column',
        bgcolor: 'background.default',
      }}
    >
      <Box
        component="header"
        sx={{ bgcolor: 'background.paper', borderBottom: '1px solid', borderColor: 'divider' }}
      >
        <Container maxWidth={false} sx={{ maxWidth: 820 }}>
          <Box sx={{ minHeight: 76, display: 'flex', alignItems: 'center' }}>
            <Link
              component={RouterLink}
              to="/"
              underline="none"
              color="inherit"
              aria-label="Hälsinge Live – startsida"
            >
              <Typography variant="h6" component="span" sx={{ fontWeight: 750, letterSpacing: '-0.03em' }}>
                Hälsinge Live
              </Typography>
            </Link>
          </Box>
        </Container>
      </Box>

      <Container
        component="main"
        maxWidth={false}
        sx={{ width: '100%', maxWidth: 820, flex: 1, py: { xs: 5, sm: 8 } }}
      >
        <Outlet />
      </Container>

      <Box component="footer" sx={{ borderTop: '1px solid', borderColor: 'divider', bgcolor: 'background.paper' }}>
        <Container maxWidth={false} sx={{ maxWidth: 820, py: 2.5 }}>
          <Typography variant="body2" color="text.secondary">
            Hälsinge Live
          </Typography>
        </Container>
      </Box>
    </Box>
  )
}
