import { useState } from 'react';
import { NavLink, Outlet } from 'react-router';
import { useTheme } from '@mui/material/styles';
import useMediaQuery from '@mui/material/useMediaQuery';
import {
  AppBar, Box, Drawer, IconButton, List, ListItemButton, ListItemText, Toolbar, Typography,
} from '@mui/material';

const drawerWidth = 220;

const navItems = [
  { label: 'Evenemang', to: '/events' },
  { label: 'Scener', to: '/venues' },
];

export default function AdminLayout() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));
  const [mobileNavOpen, setMobileNavOpen] = useState(false);

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
    </Box>
  );
}
