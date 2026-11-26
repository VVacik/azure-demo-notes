import { useState } from 'react';
import type { Note } from '../api/types';

const dateFormat = new Intl.DateTimeFormat('pl-PL', { dateStyle: 'medium', timeStyle: 'short' });

interface Props {
  note: Note;
  onEdit: () => void;
  onDelete: () => void;
  onPhoto: () => void;
}

export function NoteCard({ note, onEdit, onDelete, onPhoto }: Props) {
  return (
    <article className="card">
      {note.photoUrl ? (
        // key: gdy pojawi się nowa miniatura, zaczynamy od niej zamiast od pełnego zdjęcia
        <NotePhoto key={note.thumbnailUrl} note={note} photoUrl={note.photoUrl} />
      ) : (
        <div className="card__photo card__photo--empty">Brak zdjęcia</div>
      )}

      <div className="card__body">
        <h3>{note.title}</h3>
        {note.content && <p>{note.content}</p>}
        <time dateTime={note.createdAt}>{dateFormat.format(new Date(note.createdAt))}</time>
      </div>

      <div className="card__actions">
        <button onClick={onEdit}>Edytuj</button>
        <button onClick={onPhoto}>{note.photoUrl ? 'Zmień zdjęcie' : 'Dodaj zdjęcie'}</button>
        <button className="danger" onClick={onDelete}>
          Usuń
        </button>
      </div>
    </article>
  );
}

function NotePhoto({ note, photoUrl }: { note: Note; photoUrl: string }) {
  // Miniaturę generuje Azure Function chwilę po uploadzie – do tego czasu pokazujemy oryginał.
  const [thumbnailFailed, setThumbnailFailed] = useState(false);
  const src = note.thumbnailUrl && !thumbnailFailed ? note.thumbnailUrl : photoUrl;

  return (
    <a href={photoUrl} target="_blank" rel="noreferrer" className="card__photo">
      <img src={src} alt={note.title} loading="lazy" onError={() => setThumbnailFailed(true)} />
    </a>
  );
}
