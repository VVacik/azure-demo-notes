import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { createNote, deleteNote, getHealth, getNotes, updateNote, uploadPhoto } from './notes';
import type { NoteInput, NotesQuery } from './types';

const notesKey = ['notes'] as const;

export function useNotes(query: NotesQuery) {
  return useQuery({
    queryKey: [...notesKey, query],
    queryFn: ({ signal }) => getNotes(query, signal),
    // Przy zmianie strony/wyszukiwania pokazujemy poprzednie wyniki, zamiast migać spinnerem.
    placeholderData: keepPreviousData,
  });
}

export function useHealth() {
  return useQuery({
    queryKey: ['health'],
    queryFn: ({ signal }) => getHealth(signal),
    refetchInterval: 30_000,
    retry: false,
  });
}

// Po każdej zmianie odświeżamy listę – API zwraca też nowe adresy zdjęć (SAS).
function useNotesMutation<TArgs, TResult>(fn: (args: TArgs) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: notesKey }),
  });
}

export const useCreateNote = () => useNotesMutation((input: NoteInput) => createNote(input));

export const useUpdateNote = () =>
  useNotesMutation(({ id, input }: { id: number; input: NoteInput }) => updateNote(id, input));

export const useDeleteNote = () => useNotesMutation((id: number) => deleteNote(id));

export const useUploadPhoto = () =>
  useNotesMutation(({ id, file }: { id: number; file: File }) => uploadPhoto(id, file));
