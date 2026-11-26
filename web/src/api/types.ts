export interface Note {
  id: number;
  title: string;
  content: string | null;
  photoUrl: string | null;
  thumbnailUrl: string | null;
  createdAt: string;
}

export interface NoteInput {
  title: string;
  content: string | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface NotesQuery {
  search: string;
  page: number;
  pageSize: number;
}

export interface HealthReport {
  status: 'Healthy' | 'Degraded' | 'Unhealthy';
  checks: Record<string, string>;
}

// Format błędów ASP.NET Core (RFC 9457), errors tylko przy błędach walidacji.
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}
