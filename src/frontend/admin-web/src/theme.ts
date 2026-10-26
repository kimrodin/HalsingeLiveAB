
import { alpha, createTheme, lighten, responsiveFontSizes } from '@mui/material/styles';
import type { Theme } from '@mui/material/styles';

const theme = createTheme({
  // Färger för hela applikationen
  palette: {
    mode: 'dark',

    primary: {
      main: '#A78BFA',
      light: '#C4B5FD',
      dark: '#7C3AED',
      contrastText: '#1C1233',
    },

    secondary: {
      main: '#6EE7B7',
      light: '#A7F3D0',
      dark: '#34D399',
      contrastText: '#06281B',
    },

    background: {
      default: '#0B1120',
      paper: '#151D2E',
    },

    text: {
      primary: '#F1F5F9',
      secondary: '#A3B0C2',
    },

    divider: '#2A3549',

    error: {
      main: '#F87171',
    },

    warning: {
      main: '#FBBF24',
    },

    success: {
      main: '#34D399',
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
          backgroundColor: lighten(theme.palette.background.paper, 0.08),
          transition: 'background-color 150ms ease',

          '&:hover:not(.Mui-focused)': {
            backgroundColor: lighten(theme.palette.background.paper, 0.11),
          },

          '&.Mui-focused': {
            backgroundColor: lighten(theme.palette.background.paper, 0.09),
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
          color: theme.palette.text.primary,
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
