import { useEffect, useState } from 'react'
import { Box, Container, Link, Typography } from '@mui/material'
import { Link as RouterLink, Outlet } from 'react-router'

export default function CustomerLayout() {
  const [isScrolled, setIsScrolled] = useState(false)

  useEffect(() => {
    const updateScrollState = () => setIsScrolled(window.scrollY > 8)
    updateScrollState()
    window.addEventListener('scroll', updateScrollState, { passive: true })
    return () => window.removeEventListener('scroll', updateScrollState)
  }, [])

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
        sx={{
          position: 'sticky',
          top: 0,
          zIndex: (theme) => theme.zIndex.appBar,
          bgcolor: 'primary.dark',
          color: 'common.white',
          borderBottom: '1px solid',
          borderColor: 'primary.dark',
          boxShadow: isScrolled ? '0 3px 12px rgba(20, 42, 44, 0.12)' : 'none',
          transition: 'box-shadow 180ms ease',
          '@media (prefers-reduced-motion: reduce)': { '&, & *': { transition: 'none' } },
        }}
      >
        <Container maxWidth={false} sx={{ maxWidth: 1120 }}>
          <Box sx={{ minHeight: isScrolled ? 62 : 76, display: 'flex', alignItems: 'center', transition: 'min-height 180ms ease' }}>
            <Link
              component={RouterLink}
              to="/"
              underline="none"
              color="inherit"
              aria-label="Hälsinge Live – startsida"
            >
              <Typography variant="h6" component="span" sx={{ fontWeight: 750, letterSpacing: '-0.03em', fontSize: isScrolled ? '1.15rem' : '1.25rem', transition: 'font-size 180ms ease' }}>
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
