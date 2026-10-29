
import { alpha, createTheme, responsiveFontSizes } from '@mui/material/styles';
import type { Theme } from '@mui/material/styles';

const theme = createTheme({
  // Färger för hela applikationen
  palette: {
    mode: 'dark',

    primary: {
      main: '#00C8DC',
      light: '#72E5EF',
      dark: '#008A9B',
      contrastText: '#04232C',
    },

    secondary: {
      main: '#22D3AE',
      light: '#8BEBD8',
      dark: '#12B995',
      contrastText: '#042B24',
    },

    background: {
      default: '#0B1923',
      paper: '#142630',
    },

    text: {
      primary: '#F1F5F9',
      secondary: '#A8C0D2',
    },

    divider: '#29424F',

    error: {
      main: '#F87171',
    },

    warning: {
      main: '#FBBF24',
    },

    success: {
      main: '#12B995',
    },

    info: {
      main: '#60A5FA',
    },
  },

  // Gemensam typografi
  typography: {
    fontFamily: '"Inter", "Roboto", "Arial", sans-serif',

    h1: {
      fontWeight: 700,
      letterSpacing: '-0.04em',
    },

    h2: {
      fontWeight: 700,
      letterSpacing: '-0.03em',
    },

    h3: {
      fontWeight: 700,
      letterSpacing: '-0.02em',
    },

    h4: {
      fontWeight: 700,
      letterSpacing: '-0.02em',
    },

    h5: {
      fontWeight: 600,
    },

    h6: {
      fontWeight: 600,
    },

    button: {
      fontWeight: 600,
      textTransform: 'none',
    },
  },

  // Mellanstora rundade hörn
  shape: {
    borderRadius: 10,
  },

  // Gemensamma inställningar för MUI-komponenter
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        body: ({ theme }: { theme: Theme }) => ({
          backgroundColor: theme.palette.background.default,
          scrollbarColor: `${alpha(theme.palette.text.secondary, 0.35)} ${theme.palette.background.default}`,
          overflowX: 'hidden',
        }),
      },
    },

    MuiButton: {
      styleOverrides: {
        root: {
          borderRadius: 10,
          padding: '8px 18px',
          boxShadow: 'none',
          transition: 'background-color 150ms ease, filter 150ms ease',
        },

        contained: {
          '&:hover': {
            boxShadow: 'none',
            filter: 'brightness(1.08)',
          },
        },
      },
    },

    MuiPaper: {
      styleOverrides: {
        root: {
          backgroundImage: 'none',
          border: '1px solid #29424F',
          boxShadow: '0 8px 28px rgba(2, 12, 18, 0.12)',
        },

        outlined: ({ theme }) => ({ borderColor: theme.palette.divider }),
      },
    },

    MuiCard: {
      styleOverrides: {
        root: ({ theme }) => ({
          borderRadius: 12,
          border: `1px solid ${theme.palette.divider}`,
          backgroundImage: 'none',
        }),
      },
    },

    MuiOutlinedInput: {
      styleOverrides: {
        root: ({ theme }) => ({
          borderRadius: 10,
          backgroundColor: alpha(theme.palette.background.default, 0.45),
          transition: 'background-color 150ms ease',

          '&:hover:not(.Mui-focused)': {
            backgroundColor: alpha(theme.palette.primary.main, 0.04),
          },

          '&.Mui-focused': {
            backgroundColor: alpha(theme.palette.primary.main, 0.06),
          },

          '& .MuiOutlinedInput-notchedOutline': {
            borderColor: alpha(theme.palette.text.secondary, 0.35),
          },

          '&:hover .MuiOutlinedInput-notchedOutline': {
            borderColor: theme.palette.text.secondary,
          },

          '&.Mui-focused .MuiOutlinedInput-notchedOutline': {
            borderColor: theme.palette.primary.main,
            borderWidth: 2,
          },
        }),
      },
    },

    MuiInputLabel: {
      styleOverrides: {
        root: ({ theme }) => ({ color: theme.palette.text.secondary }),
      },
    },

    MuiAppBar: {
      styleOverrides: {
        root: ({ theme }) => ({
          backgroundImage: 'none',
          backgroundColor: alpha(theme.palette.background.default, 0.96),
          color: theme.palette.text.primary,
          boxShadow: 'none',
          borderBottom: `1px solid ${theme.palette.divider}`,
        }),
      },
    },

    MuiDialog: {
      styleOverrides: {
        paper: ({ theme }) => ({
          border: `1px solid ${theme.palette.divider}`,
          borderRadius: 14,
          backgroundImage: 'none',
          '@media (max-width: 600px)': {
            margin: 16,
            width: 'calc(100% - 32px)',
            maxWidth: 'none',
          },
        }),
      },
    },

    MuiAlert: {
      styleOverrides: {
        root: {
          borderRadius: 10,
        },
      },
    },

    MuiTableCell: {
      styleOverrides: {
        root: ({ theme }) => ({ borderColor: theme.palette.divider }),

        head: ({ theme }) => ({
          fontWeight: 600,
          color: theme.palette.text.secondary,
          backgroundColor: alpha(theme.palette.text.secondary, 0.06),
        }),
      },
    },

    MuiTableContainer: {
      styleOverrides: {
        root: {
          overflowX: 'auto',
          WebkitOverflowScrolling: 'touch',
        },
      },
    },

    MuiTable: {
      styleOverrides: {
        root: {
          minWidth: 640,
        },
      },
    },

    MuiTableRow: {
      styleOverrides: {
        root: ({ theme }) => ({
          '&.MuiTableRow-hover:hover': {
            backgroundColor: alpha(theme.palette.primary.main, 0.08),
          },
        }),
      },
    },

    MuiLink: {
      styleOverrides: {
        root: ({ theme }) => ({
          color: theme.palette.secondary.main,
          textDecorationColor: alpha(theme.palette.secondary.main, 0.45),
          '&:hover': {
            color: theme.palette.primary.light,
            textDecorationColor: 'currentColor',
          },
          '&:focus-visible': {
            outline: `2px solid ${theme.palette.primary.main}`,
            outlineOffset: 2,
            borderRadius: 2,
          },
        }),
      },
    },

    MuiListItemButton: {
      styleOverrides: {
        root: ({ theme }) => ({
          borderLeft: '3px solid transparent',
          '&.active': {
            backgroundColor: alpha(theme.palette.primary.main, 0.12),
            borderLeftColor: theme.palette.primary.main,
            color: theme.palette.primary.light,
          },
          '&:hover': {
            backgroundColor: alpha(theme.palette.secondary.main, 0.08),
          },
        }),
      },
    },

  },
});

export default responsiveFontSizes(theme);
