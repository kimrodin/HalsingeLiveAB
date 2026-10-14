import keycloak from '../auth/keycloak';
import { Button } from '@mui/material';
import { useQueryClient } from '@tanstack/react-query';
import { NavLink, Outlet } from 'react-router';
import {
  AppBar, Box, Drawer, List, ListItemButton, ListItemText, Toolbar, Typography,
} from '@mui/material';

const drawerWidth = 220;

const navItems = [
  { label: 'Evenemang', to: '/events' },
  { label: 'Scener', to: '/venues' },
];

export default function AdminLayout() {
  const queryClient = useQueryClient();

  async function logout() {
    await queryClient.cancelQueries();
    queryClient.clear();

    await keycloak.logout({
      redirectUri: window.location.origin,
    });
  }
  return (
    <Box sx={{ display: 'flex' }}>
      <AppBar position="fixed" sx={{ zIndex: (t) => t.zIndex.drawer + 1 }}>
        <Toolbar>
          <Typography variant="h6">Hälsinge Live – Admin</Typography>
          <Button
            color="inherit"
            sx={{ ml: 'auto' }}
            onClick={() => void logout()}
          >
            Logga ut
          </Button>
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
    </Box>
  );
}