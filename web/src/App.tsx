import { useState } from 'react';
import type { Note } from './api/types';
import { ApiStatus } from './components/ApiStatus';
import { DeleteNoteDialog } from './components/DeleteNoteDialog';
import { NoteForm } from './components/NoteForm';
import { NoteList } from './components/NoteList';
import { PhotoUploadDialog } from './components/PhotoUploadDialog';
import { useDebouncedValue } from './hooks/useDebouncedValue';

const PAGE_SIZE = 9;

export function App() {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<Note | null>(null);
  const [uploadingFor, setUploadingFor] = useState<Note | null>(null);
  const [deleting, setDeleting] = useState<Note | null>(null);

  // Nie wysyłamy zapytania przy każdym naciśnięciu klawisza.
  const debouncedSearch = useDebouncedValue(search, 300);

  return (
    <div className="app">
      <header className="app-header">
        <h1>Notatki ze zdjęciami</h1>
        <ApiStatus />
      </header>

      <main className="layout">
        <section className="panel">
          <h2>{editing ? 'Edycja notatki' : 'Nowa notatka'}</h2>
          {/* key resetuje formularz przy przełączeniu między dodawaniem a edycją */}
          <NoteForm key={editing?.id ?? 'new'} note={editing} onDone={() => setEditing(null)} />
        </section>

        <section className="notes">
          <input
            type="search"
            className="search"
            placeholder="Szukaj po tytule…"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
          <NoteList
            query={{ search: debouncedSearch, page, pageSize: PAGE_SIZE }}
            onPageChange={setPage}
            onEdit={setEditing}
            onDelete={setDeleting}
            onPhoto={setUploadingFor}
          />
        </section>
      </main>

      {uploadingFor && (
        <PhotoUploadDialog note={uploadingFor} onClose={() => setUploadingFor(null)} />
      )}
      {deleting && (
        <DeleteNoteDialog
          note={deleting}
          onClose={() => setDeleting(null)}
          onDeleted={() => {
            if (editing?.id === deleting.id) setEditing(null);
          }}
        />
      )}
    </div>
  );
}
