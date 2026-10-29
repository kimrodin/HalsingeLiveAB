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

const drawerWidth = 220;

const navItems = [
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
    <List>
      {navItems.map((item) => (
        <ListItemButton
          key={item.to}
          component={NavLink}
          to={item.to}
          onClick={() => setMobileNavOpen(false)}
          sx={{ '&.active': { bgcolor: 'action.selected' } }}
        >
          <ListItemText primary={item.label} />
        </ListItemButton>
      ))}
    </List>
  );

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', width: '100%' }}>
      <AppBar position="fixed" sx={{ zIndex: (t) => t.zIndex.drawer + 1 }}>
        <Toolbar sx={{ gap: 1, px: { xs: 1, sm: 3 } }}>
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
          '& .MuiDrawer-paper': { width: drawerWidth, boxSizing: 'border-box' },
        }}
      >
        <Toolbar />
        {navList}
      </Drawer>

      <Drawer
        variant="temporary"
        open={isMobile && mobileNavOpen}
        onClose={() => setMobileNavOpen(false)}
        ModalProps={{ keepMounted: true }}
        sx={{
          display: { xs: 'block', sm: 'none' },
          '& .MuiDrawer-paper': { width: drawerWidth, boxSizing: 'border-box' },
        }}
      >
        <Toolbar />
        {navList}
      </Drawer>

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          minWidth: 0,
          width: { xs: '100%', sm: `calc(100% - ${drawerWidth}px)` },
          p: { xs: 2, sm: 3 },
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
