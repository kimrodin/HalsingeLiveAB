import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider } from 'react-router/dom';
import { CssBaseline } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { router } from './router';
import keycloak from './auth/keycloak';

const queryClient = new QueryClient();
const root = createRoot(document.getElementById('root')!);

async function start() {
  try {
    const authenticated = await keycloak.init({
      onLoad: 'check-sso',
      pkceMethod: 'S256',
      checkLoginIframe: false,
    });

    if (!authenticated) {
      root.render(
        <main>
          <h1>Hälsinge Live – Admin</h1>
          <button onClick={() => void keycloak.login()}>
            Logga in
          </button>
        </main>,
      );
      return;
    }

    if (!keycloak.hasRealmRole('admin')) {
      root.render(
        <main>
          <p>Du saknar administratörsbehörighet.</p>
          <button onClick={() => void keycloak.logout({
            redirectUri: window.location.origin,
          })}>
            Logga ut
          </button>
        </main>,
      );
      return;
    }

    root.render(
      <StrictMode>
        <CssBaseline />
        <QueryClientProvider client={queryClient}>
          <RouterProvider router={router} />
        </QueryClientProvider>
      </StrictMode>,
    );
  } catch {
    root.render(
      <p>Inloggningstjänsten kunde inte nås. Ladda om och försök igen.</p>,
    );
  }
}

void start();