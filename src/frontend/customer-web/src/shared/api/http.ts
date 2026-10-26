export class ApiError extends Error {
  readonly status: number
  readonly errors?: Record<string, string[]>

  constructor(message: string, status: number, errors?: Record<string, string[]>) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.errors = errors
  }
}

interface ApiProblem {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

function toApiError(problem: ApiProblem | null, status: number, fallback: string): ApiError {
  const errors = problem?.errors
  const fieldMessages = errors ? Object.values(errors).flat().join(' ') : undefined
  return new ApiError(problem?.detail ?? fieldMessages ?? problem?.title ?? fallback, status, errors)
}

async function readError(response: Response, fallback: string): Promise<ApiError> {
  try {
    return toApiError((await response.json()) as ApiProblem, response.status, fallback)
  } catch {
    return toApiError(null, response.status, fallback)
  }
}

export async function getJson<T>(path: string, signal?: AbortSignal, base = '/api'): Promise<T> {
  const response = await fetch(`${base}${path}`, {
    signal,
    // Catalog data can be changed by admins; always request a fresh response.
    cache: 'no-store',
  })

  if (!response.ok) {
    throw await readError(response, `Kunde inte hämta information (HTTP ${response.status}).`)
  }

  return (await response.json()) as T
}

export async function postJson<T>(path: string, body?: unknown, base = '/api'): Promise<T> {
  const response = await fetch(`${base}${path}`, {
    method: 'POST',
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
    cache: 'no-store',
  })

  if (!response.ok) {
    throw await readError(response, `Begäran kunde inte genomföras (HTTP ${response.status}).`)
  }

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}
