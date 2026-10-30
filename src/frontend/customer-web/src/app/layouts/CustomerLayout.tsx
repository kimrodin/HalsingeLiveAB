import { useEffect, useState } from 'react'
import { Box, Container, Link, Typography } from '@mui/material'
import { Link as RouterLink, Outlet, useLocation } from 'react-router'

export default function CustomerLayout() {
  const [isScrolled, setIsScrolled] = useState(false)
  const isHomePage = useLocation().pathname === '/'

  useEffect(() => {
    const updateScrollState = () => {
      const scrollY = Math.max(0, window.scrollY)
      setIsScrolled(scrollY > 16)
    }
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
        '--customer-header-height': '77px',
        backgroundImage: isHomePage
          ? 'linear-gradient(#062b3c 0 var(--customer-header-height), transparent var(--customer-header-height))'
          : 'none',
      }}
    >
      <Box
        component="header"
        sx={{
          position: 'sticky',
          top: 0,
          zIndex: (theme) => theme.zIndex.appBar,
          bgcolor: isHomePage && !isScrolled ? 'transparent' : 'primary.dark',
          color: 'common.white',
          borderBottom: '1px solid',
          borderColor: isHomePage && !isScrolled ? 'transparent' : 'primary.dark',
          boxShadow: isScrolled ? '0 3px 12px rgba(20, 42, 44, 0.12)' : 'none',
          transition: 'background-color 180ms ease, border-color 180ms ease, box-shadow 180ms ease',
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
              <Typography variant="h6" component="span" sx={{ display: 'inline-block', fontWeight: 750, letterSpacing: '-0.03em', fontSize: '1.25rem', transform: isScrolled ? 'scale(0.92)' : 'scale(1)', transformOrigin: 'left center', transition: 'transform 180ms ease' }}>
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
