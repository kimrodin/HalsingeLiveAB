import keycloak from '../auth/keycloak';
import { useState } from 'react';
import { NavLink, Outlet } from 'react-router';
import { useTheme } from '@mui/material/styles';
import useMediaQuery from '@mui/material/useMediaQuery';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Alert, AppBar, Box, Button, Drawer, IconButton, List, ListItemButton, ListItemText,
  Snackbar, Toolbar, Typography,
} from '@mui/material';
import { deleteSeedData, seedData } from '../../features/seed/api/seed';
import sidebarLandscape from '../../assets/sidebar-landscape.svg';

const drawerWidth = 260;

const drawerPaperSx = {
  width: drawerWidth,
  boxSizing: 'border-box',
  backgroundColor: '#091b25',
  backgroundImage: `linear-gradient(180deg, rgba(7, 22, 31, .15), rgba(7, 22, 31, .25)), url("${sidebarLandscape}")`,
  backgroundSize: 'cover',
  backgroundPosition: 'center bottom',
};

const navItems = [
  { label: 'Dashboard', to: '/dashboard' },
  { label: 'Evenemang', to: '/events' },
  { label: 'Scener', to: '/venues' },
  { label: 'Bokningar', to: '/bookings' },
];

export default function AdminLayout() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));
  const [mobileNavOpen, setMobileNavOpen] = useState(false);
  const queryClient = useQueryClient();

  async function logout() {
    await queryClient.cancelQueries();
    queryClient.clear();
    await keycloak.logout({ redirectUri: window.location.origin });
  }

  const [notice, setNotice] = useState<{ message: string; severity: 'success' | 'error' } | null>(null);
  const refreshCatalog = async () => Promise.all([
    queryClient.invalidateQueries({ queryKey: ['events'] }),
    queryClient.invalidateQueries({ queryKey: ['venues'] }),
  ]);

  const seedMutation = useMutation({
    mutationFn: seedData,
    onSuccess: async (result) => {
      await refreshCatalog();
      setNotice({
        message: result.events.createdCount === 0
          ? 'Seeddata finns redan.'
          : 'Seeddata skapades.',
        severity: 'success',
      });
    },
    onError: (error) => setNotice({
      message: `Kunde inte skapa seeddata: ${error.message}`,
      severity: 'error',
    }),
  });
  const deleteMutation = useMutation({
    mutationFn: deleteSeedData,
    onSuccess: async (result) => {
      await refreshCatalog();
      setNotice({
        message: `Seeddata borttagen: ${result.deletedEventCount} evenemang, ${result.deletedSectionCount} sektioner och ${result.deletedVenueCount} scener.`,
        severity: 'success',
      });
    },
    onError: (error) => setNotice({
      message: `Kunde inte ta bort seeddata: ${error.message}`,
      severity: 'error',
    }),
  });

  const handleDeleteSeed = () => {
    if (window.confirm('Ta bort seedade evenemang, sektioner och scener?')) {
      deleteMutation.mutate();
    }
  };

  const seedActionPending = seedMutation.isPending || deleteMutation.isPending;

  const navList = (
    <List sx={{ px: 1.5 }}>
      {navItems.map((item) => (
        <ListItemButton
          key={item.to}
          component={NavLink}
          to={item.to}
          onClick={() => setMobileNavOpen(false)}
          sx={{ mb: 1, borderRadius: 1 }}
        >
          <ListItemText primary={item.label} />
        </ListItemButton>
      ))}
    </List>
  );

  const sidebarContent = (
    <>
      <Box sx={{ px: 2.5, py: 3 }}>
        <Typography sx={{ fontFamily: 'Georgia, serif', fontSize: 28, color: '#f1f5f9' }}>
          Hälsinge Live
        </Typography>
        <Typography sx={{ mt: 1, fontSize: 10, letterSpacing: '.2em', color: '#9ecde4' }}>
          EVENEMANG SOM FÖRENAR HÄLSINGLAND
        </Typography>
      </Box>
      {navList}
      <Box sx={{ mt: 'auto', px: 3, py: 4, background: 'linear-gradient(transparent, rgba(6, 25, 35, .85))' }}>
        <Typography sx={{ maxWidth: 190, fontSize: 12, letterSpacing: '.2em', lineHeight: 1.8, color: '#a3d2ee' }}>
          KONST, KULTUR OCH UPPLEVELSER I HÄLSINGLAND
        </Typography>
      </Box>
    </>
  );

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', width: '100%' }}>
      <AppBar position="fixed" sx={{ width: { sm: `calc(100% - ${drawerWidth}px)` }, ml: { sm: `${drawerWidth}px` } }}>
        <Toolbar sx={{ gap: 1, px: { xs: 1, sm: 3 }, flexWrap: 'wrap', py: { xs: 1, sm: 0 } }}>
          {isMobile && (
            <IconButton
              color="inherit"
              edge="start"
              aria-label="Öppna menyn"
              onClick={() => setMobileNavOpen(true)}
            >
              <span aria-hidden="true">☰</span>
            </IconButton>
          )}
          <Typography variant="h6" noWrap>Hälsinge Live – Admin</Typography>
          <Box sx={{ flexGrow: 1 }} />
          {import.meta.env.DEV && (
            <>
              <Button color="inherit" disabled={seedActionPending} onClick={() => seedMutation.mutate()}>
                Skapa seeddata
              </Button>
              <Button color="inherit" disabled={seedActionPending} onClick={handleDeleteSeed}>
                Ta bort seeddata
              </Button>
            </>
          )}
          <Button color="inherit" onClick={() => void logout()}>
            Logga ut
          </Button>
        </Toolbar>
      </AppBar>

      <Drawer
        variant="permanent"
        sx={{
          display: { xs: 'none', sm: 'block' },
          width: drawerWidth,
          flexShrink: 0,
          '& .MuiDrawer-paper': drawerPaperSx,
        }}
      >
        {sidebarContent}
      </Drawer>

      <Drawer
        variant="temporary"
        open={isMobile && mobileNavOpen}
        onClose={() => setMobileNavOpen(false)}
        ModalProps={{ keepMounted: true }}
        sx={{
          display: { xs: 'block', sm: 'none' },
          '& .MuiDrawer-paper': drawerPaperSx,
        }}
      >
        {sidebarContent}
      </Drawer>

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          minWidth: 0,
          width: { xs: '100%', sm: `calc(100% - ${drawerWidth}px)` },
          p: { xs: 2, sm: 3 },
          backgroundImage: 'radial-gradient(ellipse at top right, rgba(0, 200, 220, .04), transparent 65%)',
        }}
      >
        <Toolbar />
        <Outlet />
      </Box>
      <Snackbar
        open={notice !== null}
        autoHideDuration={6000}
        onClose={() => setNotice(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert severity={notice?.severity ?? 'success'} onClose={() => setNotice(null)}>
          {notice?.message ?? ''}
        </Alert>
      </Snackbar>
    </Box>
  );
}
