
import { createTheme } from '@mui/material/styles';

const theme = createTheme({
  // Färger för hela applikationen
  palette: {
    mode: 'dark',

    primary: {
      main: '#60A5FA',
      light: '#93C5FD',
      dark: '#2563EB',
      contrastText: '#0B1220',
    },

    secondary: {
      main: '#22D3EE',
      light: '#67E8F9',
      dark: '#0891B2',
      contrastText: '#082F49',
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
        body: {
          backgroundColor: '#0B1120',
          scrollbarColor: '#3B4962 #0B1120',
        },
      },
    },

    MuiButton: {
      styleOverrides: {
        root: {
          borderRadius: 10,
          padding: '8px 18px',
          boxShadow: 'none',
          transition: 'background-color 150ms ease',
        },

        contained: {
          '&:hover': {
            boxShadow: 'none',
          },
        },
      },
    },

    MuiPaper: {
      styleOverrides: {
        root: {
          backgroundImage: 'none',
        },

        outlined: {
          borderColor: '#2A3549',
        },
      },
    },

    MuiCard: {
      styleOverrides: {
        root: {
          borderRadius: 12,
          border: '1px solid #2A3549',
          backgroundImage: 'none',
        },
      },
    },

    MuiOutlinedInput: {
      styleOverrides: {
        root: {
          borderRadius: 10,

          '& .MuiOutlinedInput-notchedOutline': {
            borderColor: '#3A4860',
          },

          '&:hover .MuiOutlinedInput-notchedOutline': {
            borderColor: '#8292AA',
          },

          '&.Mui-focused .MuiOutlinedInput-notchedOutline': {
            borderColor: '#60A5FA',
          },
        },
      },
    },

    MuiInputLabel: {
      styleOverrides: {
        root: {
          color: '#A3B0C2',
        },
      },
    },

    MuiAppBar: {
      styleOverrides: {
        root: {
          backgroundImage: 'none',
          borderBottom: '1px solid #2A3549',
        },
      },
    },

    MuiDialog: {
      styleOverrides: {
        paper: {
          border: '1px solid #2A3549',
          borderRadius: 14,
          backgroundImage: 'none',
        },
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
        root: {
          borderColor: '#2A3549',
        },

        head: {
          fontWeight: 600,
          color: '#F1F5F9',
        },
      },
    },
  },
});

export default theme;
