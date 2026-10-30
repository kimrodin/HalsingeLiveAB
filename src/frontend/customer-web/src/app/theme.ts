import { alpha, createTheme } from '@mui/material/styles'

// Kundwebben delar adminens blågröna färgfamilj, med ljusa ytor för en
// lättläst evenemangs- och köpupplevelse.
const colors = {
  teal: '#397C83',
  tealLight: '#5B969A',
  tealDark: '#285B61',
  sage: '#587568',
  sageLight: '#789184',
  sageDark: '#40584E',
  canvas: '#F4F5F1',
  surface: '#FFFFFF',
  text: '#263638',
  textMuted: '#596A6B',
  divider: '#D8E0DC',
}

export const theme = createTheme({
  palette: {
    mode: 'light',
    primary: {
      main: colors.teal,
      light: colors.tealLight,
      dark: colors.tealDark,
      contrastText: '#FFFFFF',
    },
    secondary: {
      main: colors.sage,
      light: colors.sageLight,
      dark: colors.sageDark,
      contrastText: '#FFFFFF',
    },
    success: { main: '#55734F', light: '#789071', contrastText: '#FFFFFF' },
    warning: { main: '#A76F20', contrastText: '#FFFFFF' },
    error: { main: '#A64239', contrastText: '#FFFFFF' },
    info: { main: colors.teal, contrastText: '#FFFFFF' },
    background: { default: colors.canvas, paper: colors.surface },
    text: { primary: colors.text, secondary: colors.textMuted },
    divider: colors.divider,
  },
  shape: { borderRadius: 10 },
  typography: {
    fontFamily: '"DM Sans", "Helvetica Neue", Arial, sans-serif',
    h1: { fontFamily: '"DM Serif Display", Georgia, serif', fontWeight: 400, letterSpacing: '-0.025em' },
    h2: { fontFamily: '"DM Serif Display", Georgia, serif', fontWeight: 400, letterSpacing: '-0.02em' },
    h3: { fontFamily: '"DM Serif Display", Georgia, serif', fontWeight: 400, letterSpacing: '-0.015em' },
    h4: { fontFamily: '"DM Serif Display", Georgia, serif', fontWeight: 400 },
    button: { textTransform: 'none', fontWeight: 650 },
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        body: {
          backgroundColor: colors.canvas,
          overflowX: 'hidden',
        },
        '::selection': { backgroundColor: alpha(colors.teal, 0.2) },
      },
    },
    MuiButton: {
      defaultProps: { disableElevation: true },
      styleOverrides: {
        root: {
          minHeight: 40,
          borderRadius: 8,
          transition: 'background-color 150ms ease, border-color 150ms ease, color 150ms ease',
          '&:focus-visible': { outline: `3px solid ${alpha(colors.teal, 0.38)}`, outlineOffset: 2 },
          '&.MuiButton-containedPrimary:hover': { backgroundColor: colors.tealDark },
          '&.MuiButton-outlinedPrimary': {
            borderColor: alpha(colors.teal, 0.55),
            '&:hover': { borderColor: colors.tealDark, backgroundColor: alpha(colors.teal, 0.06) },
          },
        },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: { backgroundImage: 'none' },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: {
          border: `1px solid ${colors.divider}`,
          boxShadow: '0 2px 10px rgba(28, 48, 48, 0.045)',
          backgroundImage: 'none',
        },
      },
    },
    MuiOutlinedInput: {
      styleOverrides: {
        root: {
          backgroundColor: colors.surface,
          '& .MuiOutlinedInput-notchedOutline': { borderColor: colors.divider },
          '&:hover .MuiOutlinedInput-notchedOutline': { borderColor: colors.tealLight },
          '&.Mui-focused .MuiOutlinedInput-notchedOutline': { borderColor: colors.teal, borderWidth: 2 },
        },
      },
    },
    MuiInputLabel: {
      styleOverrides: { root: { color: colors.textMuted } },
    },
    MuiLink: {
      styleOverrides: {
        root: {
          color: colors.tealDark,
          '&:hover': { color: colors.teal },
          '&:focus-visible': { outline: `2px solid ${colors.teal}`, outlineOffset: 2, borderRadius: 2 },
        },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: { borderRadius: 7, fontWeight: 600 },
        colorSuccess: {
          backgroundColor: '#E7EEE4',
          color: '#3F5D3B',
        },
      },
    },
    MuiDivider: {
      styleOverrides: { root: { borderColor: colors.divider } },
    },
  },
})
