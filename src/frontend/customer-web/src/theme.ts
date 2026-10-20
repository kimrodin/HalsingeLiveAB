import { createTheme } from '@mui/material/styles';

export const theme = createTheme({
  palette: {
    primary:   { main: '#B35A3E', light: '#E2A58B', dark: '#8F4630', contrastText: '#F6EFE3' },
    secondary: { main: '#4A6879', light: '#7FA0B3', dark: '#3A5362', contrastText: '#F6EFE3' },
    success:   { main: '#5E6E4C', light: '#93A27D', contrastText: '#F6EFE3' },
    warning:   { main: '#D9A441', contrastText: '#3B2F2A' },
    error:     { main: '#A63A2C', contrastText: '#F6EFE3' },
    info:      { main: '#4A6879', contrastText: '#F6EFE3' },
    background: { default: '#F6EFE3', paper: '#EADCC6' },
    text:      { primary: '#3B2F2A', secondary: '#6E5E53' },
    divider:   '#D4BF9F',
  },
  shape: { borderRadius: 10 },
  typography: {
    fontFamily: '"DM Sans", "Helvetica Neue", Arial, sans-serif',
    h1: { fontFamily: '"DM Serif Display", Georgia, serif' },
    h2: { fontFamily: '"DM Serif Display", Georgia, serif' },
    h3: { fontFamily: '"DM Serif Display", Georgia, serif' },
    button: { textTransform: 'none', fontWeight: 600 },
  },
  components: {
    MuiButton: { defaultProps: { disableElevation: true } },
    MuiPaper: { styleOverrides: { root: { backgroundImage: 'none' } } },
  },
});
