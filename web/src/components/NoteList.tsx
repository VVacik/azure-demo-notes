import { useEffect } from 'react';
import { useNotes } from '../api/hooks';
import type { Note, NotesQuery } from '../api/types';
import { NoteCard } from './NoteCard';

interface Props {
  query: NotesQuery;
  onPageChange: (page: number) => void;
  onEdit: (note: Note) => void;
  onDelete: (note: Note) => void;
  onPhoto: (note: Note) => void;
}

export function NoteList({ query, onPageChange, onEdit, onDelete, onPhoto }: Props) {
  const { data, isPending, isError, error, refetch, isFetching } = useNotes(query);
  const lastPage = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  // Po usunięciu ostatniej notatki na stronie cofamy się na ostatnią istniejącą stronę.
  useEffect(() => {
    if (data && data.items.length === 0 && query.page > lastPage) {
      onPageChange(lastPage);
    }
  }, [data, query.page, lastPage, onPageChange]);

  if (isPending) {
    return <p className="state">Ładowanie notatek…</p>;
  }

  if (isError) {
    return (
      <div className="state state--error">
        <p>Nie udało się pobrać notatek: {error.message}</p>
        <button onClick={() => refetch()}>Spróbuj ponownie</button>
      </div>
    );
  }

  if (data.items.length === 0) {
    return (
      <p className="state">
        {query.search ? `Brak notatek pasujących do „${query.search}”.` : 'Nie ma jeszcze notatek.'}
      </p>
    );
  }

  return (
    <>
      <div className={`grid ${isFetching ? 'grid--refreshing' : ''}`}>
        {data.items.map((note) => (
          <NoteCard
            key={note.id}
            note={note}
            onEdit={() => onEdit(note)}
            onDelete={() => onDelete(note)}
            onPhoto={() => onPhoto(note)}
          />
        ))}
      </div>

      {lastPage > 1 && (
        <nav className="pagination" aria-label="Strony">
          <button disabled={data.page <= 1} onClick={() => onPageChange(data.page - 1)}>
            ‹ Poprzednia
          </button>
          <span>
            Strona {data.page} z {lastPage}
          </span>
          <button disabled={data.page >= lastPage} onClick={() => onPageChange(data.page + 1)}>
            Następna ›
          </button>
        </nav>
      )}
    </>
  );
}
