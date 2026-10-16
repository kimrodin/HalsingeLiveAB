import { useState } from 'react';
import { NavLink, Outlet } from 'react-router';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Alert, AppBar, Box, Button, Drawer, List, ListItemButton, ListItemText, Snackbar,
  Toolbar, Typography,
} from '@mui/material';
import { deleteSeedData, seedData } from '../api/catalog';

const drawerWidth = 220;

const navItems = [
  { label: 'Evenemang', to: '/events' },
  { label: 'Scener', to: '/venues' },
];

export default function AdminLayout() {
  const queryClient = useQueryClient();
  const [notice, setNotice] = useState<{ message: string; severity: 'success' | 'error' } | null>(null);
  const refreshCatalog = async () => Promise.all([
    queryClient.invalidateQueries({ queryKey: ['events'] }),
    queryClient.invalidateQueries({ queryKey: ['venues'] }),
  ]);

  const seedMutation = useMutation({
    mutationFn: seedData,
    onSuccess: async () => {
      await refreshCatalog();
      setNotice({ message: 'Seeddata skapades.', severity: 'success' });
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

  return (
    <Box sx={{ display: 'flex' }}>
      <AppBar position="fixed" sx={{ zIndex: (t) => t.zIndex.drawer + 1 }}>
        <Toolbar>
          <Typography variant="h6">Hälsinge Live – Admin</Typography>
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
        </Toolbar>
      </AppBar>

      <Drawer
        variant="permanent"
        sx={{ width: drawerWidth, '& .MuiDrawer-paper': { width: drawerWidth } }}
      >
        <Toolbar />
        <List>
          {navItems.map((item) => (
            <ListItemButton
              key={item.to}
              component={NavLink}
              to={item.to}
              sx={{ '&.active': { bgcolor: 'action.selected' } }}
            >
              <ListItemText primary={item.label} />
            </ListItemButton>
          ))}
        </List>
      </Drawer>

      <Box component="main" sx={{ flexGrow: 1, p: 3 }}>
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
