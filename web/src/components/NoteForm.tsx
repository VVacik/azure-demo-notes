import { useState, type FormEvent } from 'react';
import { ApiError } from '../api/client';
import { useCreateNote, useUpdateNote } from '../api/hooks';
import type { Note } from '../api/types';

const TITLE_MAX = 200;

interface Props {
  note: Note | null;
  onDone: () => void;
}

export function NoteForm({ note, onDone }: Props) {
  const [title, setTitle] = useState(note?.title ?? '');
  const [content, setContent] = useState(note?.content ?? '');
  const createNote = useCreateNote();
  const updateNote = useUpdateNote();
  const mutation = note ? updateNote : createNote;

  // Walidację robi API – pokazujemy jego komunikaty (ProblemDetails) przy polach.
  const apiError = mutation.error instanceof ApiError ? mutation.error : null;
  const titleError = apiError?.fieldError('title');
  const generalError = mutation.error && !titleError ? mutation.error.message : null;

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const input = { title, content: content.trim() || null };

    if (note) {
      updateNote.mutate({ id: note.id, input }, { onSuccess: onDone });
    } else {
      createNote.mutate(input, {
        onSuccess: () => {
          setTitle('');
          setContent('');
        },
      });
    }
  }

  return (
    <form className="note-form" onSubmit={handleSubmit} noValidate>
      <label>
        Tytuł
        <input
          value={title}
          maxLength={TITLE_MAX}
          onChange={(e) => setTitle(e.target.value)}
          aria-invalid={Boolean(titleError)}
        />
        <span className="hint">
          {title.length}/{TITLE_MAX}
        </span>
        {titleError && <span className="field-error">{titleError}</span>}
      </label>

      <label>
        Treść
        <textarea rows={5} value={content} onChange={(e) => setContent(e.target.value)} />
      </label>

      {generalError && <p className="error">{generalError}</p>}

      <div className="form-actions">
        <button type="submit" className="primary" disabled={mutation.isPending}>
          {mutation.isPending ? 'Zapisuję…' : note ? 'Zapisz zmiany' : 'Dodaj notatkę'}
        </button>
        {note && (
          <button type="button" onClick={onDone}>
            Anuluj
          </button>
        )}
      </div>
    </form>
  );
}
