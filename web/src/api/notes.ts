import { API_URL } from '../config';
import { apiRequest } from './client';
import type { HealthReport, Note, NoteInput, NotesQuery, PagedResult } from './types';

export const MAX_PHOTO_BYTES = 5 * 1024 * 1024;
export const ALLOWED_PHOTO_TYPES = ['image/jpeg', 'image/png', 'image/webp'];

export function getNotes(query: NotesQuery, signal?: AbortSignal) {
  const params = new URLSearchParams({
    page: String(query.page),
    pageSize: String(query.pageSize),
  });
  if (query.search.trim()) {
    params.set('search', query.search.trim());
  }
  return apiRequest<PagedResult<Note>>(`/api/notes?${params}`, { signal });
}

export function createNote(input: NoteInput) {
  return apiRequest<Note>('/api/notes', { method: 'POST', json: input });
}

export function updateNote(id: number, input: NoteInput) {
  return apiRequest<Note>(`/api/notes/${id}`, { method: 'PUT', json: input });
}

export function deleteNote(id: number) {
  return apiRequest<void>(`/api/notes/${id}`, { method: 'DELETE' });
}

export function uploadPhoto(id: number, file: File) {
  const form = new FormData();
  form.append('file', file);
  return apiRequest<Note>(`/api/notes/${id}/photo`, { method: 'POST', body: form });
}

// Health check zwraca 503 z poprawnym JSON-em, gdy coś nie działa – czytamy treść niezależnie od statusu.
export async function getHealth(signal?: AbortSignal): Promise<HealthReport> {
  const response = await fetch(`${API_URL}/api/health`, { signal });
  return (await response.json()) as HealthReport;
}
