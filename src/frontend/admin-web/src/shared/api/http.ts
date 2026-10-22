import keycloak from '../../app/auth/keycloak';
// Gemensam fetch-hjälpare för alla anrop mot EventCatalog.
//
// I utveckling går anropen till /api/... och Vite-servern skickar dem vidare till
// catalog-API:t (se vite.config.ts). Då behövs ingen CORS och inga hårdkodade portar.
// Sätt VITE_CATALOG_API_URL om API:t ska anropas på en annan adress.
export const BASE_URL: string = import.meta.env.VITE_CATALOG_API_URL ?? '/api';

export class ApiError extends Error {
  status: number;

  // erasableSyntaxOnly är på i tsconfig, så inga parameter properties här
  constructor(status: number, message: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

// Backend svarar på olika sätt vid fel:
//  - [ApiController]-validering: { errors: { Fält: ["meddelande"] } }
//  - BadRequest("text") / NotFound("text"): en ren sträng
//  - Problem(...): { title, detail, status }
async function readErrorMessage(res: Response): Promise<string> {
  const text = await res.text();
  if (!text) return `${res.status} ${res.statusText}`;

  try {
    const body: unknown = JSON.parse(text);
    if (typeof body === 'string') return body;
    if (body && typeof body === 'object') {
      const obj = body as Record<string, unknown>;
      if (obj.errors && typeof obj.errors === 'object') {
        return Object.values(obj.errors as Record<string, string[]>).flat().join(' ');
      }
      if (typeof obj.detail === 'string') return obj.detail;
      if (typeof obj.title === 'string') return obj.title;
    }
    return text;
  } catch {
    return text;
  }
}

export async function request<T = void>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
  if (!keycloak.authenticated) {
    throw new ApiError(401, 'Du behöver logga in.');
  }

  try {
    await keycloak.updateToken(30);
  } catch {
    keycloak.clearToken();
    throw new ApiError(401, 'Din session har gått ut. Logga in igen.');
  }

  if (!keycloak.token) {
    throw new ApiError(401, 'Ingen giltig inloggning finns.');
  }

  const headers = new Headers(init.headers);
  headers.set('Authorization', `Bearer ${keycloak.token}`);

  const res = await fetch(`${BASE_URL}${path}`, {
    ...init,
    headers,
  });

  if (!res.ok) {
    throw new ApiError(res.status, await readErrorMessage(res));
  }

  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export function sendJson<T = void>(
  method: 'POST' | 'PUT',
  path: string,
  body: unknown,
): Promise<T> {
  return request<T>(path, {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}
