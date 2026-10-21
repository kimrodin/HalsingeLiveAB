export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

export async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`/api${path}`, {
    signal,
    // Catalog data can be changed by admins; always request a fresh response.
    cache: 'no-store',
  })

  if (!response.ok) {
    let message = `Kunde inte hämta information (HTTP ${response.status}).`

    try {
      const problem = (await response.json()) as { title?: string; detail?: string }
      message = problem.detail ?? problem.title ?? message
    } catch {
      // Keep the status-based message when the server did not return JSON.
    }

    throw new ApiError(message, response.status)
  }

  return (await response.json()) as T
}
