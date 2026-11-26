import { API_URL } from '../config';
import type { ProblemDetails } from './types';

export class ApiError extends Error {
  readonly status: number;
  readonly problem?: ProblemDetails;

  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }

  // Błędy walidacji z API mają klucze w PascalCase ("Title"), szukamy bez względu na wielkość liter.
  fieldError(field: string): string | undefined {
    const entry = Object.entries(this.problem?.errors ?? {}).find(
      ([key]) => key.toLowerCase() === field.toLowerCase(),
    );
    return entry?.[1][0];
  }
}

interface RequestOptions {
  method?: string;
  json?: unknown;
  body?: FormData;
  signal?: AbortSignal;
}

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', json, body, signal } = options;

  let response: Response;
  try {
    response = await fetch(`${API_URL}${path}`, {
      method,
      signal,
      // Przy FormData nie ustawiamy Content-Type – przeglądarka sama doda boundary.
      headers: json !== undefined ? { 'Content-Type': 'application/json' } : undefined,
      body: json !== undefined ? JSON.stringify(json) : body,
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error;
    }
    throw new ApiError(0, 'Brak połączenia z API. Sprawdź, czy serwer działa.');
  }

  if (!response.ok) {
    throw await toApiError(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

async function toApiError(response: Response): Promise<ApiError> {
  let problem: ProblemDetails | undefined;
  try {
    problem = (await response.json()) as ProblemDetails;
  } catch {
    // Odpowiedź bez treści JSON – zostaje sam kod statusu.
  }

  const firstFieldError = Object.values(problem?.errors ?? {})[0]?.[0];
  const message =
    firstFieldError ?? problem?.detail ?? problem?.title ?? `Błąd API (${response.status})`;

  return new ApiError(response.status, message, problem);
}
