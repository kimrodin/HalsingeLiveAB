import { Box, Container, Link, Typography } from '@mui/material'
import { Link as RouterLink, Outlet, useLocation } from 'react-router'

export default function CustomerLayout() {
  const isHomePage = useLocation().pathname === '/'

  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'flex',
        flexDirection: 'column',
        bgcolor: 'background.default',
        '--customer-header-height': '77px',
      }}
    >
      <Box
        component="header"
        sx={{
          position: 'relative',
          zIndex: (theme) => theme.zIndex.appBar,
          bgcolor: isHomePage ? 'transparent' : 'primary.dark',
          color: 'common.white',
          borderBottom: '1px solid',
          borderColor: isHomePage ? 'transparent' : 'primary.dark',
          transition: 'background-color 180ms ease, border-color 180ms ease',
          '@media (prefers-reduced-motion: reduce)': { '&, & *': { transition: 'none' } },
        }}
      >
        <Container maxWidth={false} sx={{ maxWidth: 1120 }}>
          <Box sx={{ height: 76, display: 'flex', alignItems: 'center' }}>
            <Link
              component={RouterLink}
              to="/"
              underline="none"
              color="inherit"
              aria-label="Hälsinge Live – startsida"
            >
              <Typography variant="h6" component="span" sx={{ display: 'inline-block', fontWeight: 750, letterSpacing: '-0.03em', fontSize: '1.25rem' }}>
                Hälsinge Live
              </Typography>
            </Link>
          </Box>
        </Container>
      </Box>

      <Container
        component="main"
        maxWidth={false}
        sx={{ width: '100%', maxWidth: 1120, flex: 1, py: { xs: 3, sm: 5 } }}
      >
        <Outlet />
      </Container>

      <Box component="footer" sx={{ borderTop: '1px solid', borderColor: 'divider', bgcolor: 'background.paper' }}>
        <Container maxWidth={false} sx={{ maxWidth: 1120, py: 2.5 }}>
          <Typography variant="body2" color="text.secondary">
            Hälsinge Live
          </Typography>
        </Container>
      </Box>
    </Box>
  )
}
