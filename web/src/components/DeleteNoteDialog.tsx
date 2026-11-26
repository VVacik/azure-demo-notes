import { useDeleteNote } from '../api/hooks';
import type { Note } from '../api/types';
import { Modal } from './Modal';

interface Props {
  note: Note;
  onClose: () => void;
  onDeleted: () => void;
}

export function DeleteNoteDialog({ note, onClose, onDeleted }: Props) {
  const deleteNote = useDeleteNote();

  function handleConfirm() {
    deleteNote.mutate(note.id, {
      onSuccess: () => {
        onDeleted();
        onClose();
      },
    });
  }

  return (
    <Modal title="Usunąć notatkę?" onClose={onClose}>
      <p>
        Notatka „{note.title}” {note.photoUrl && 'razem ze zdjęciem '}zostanie trwale usunięta.
      </p>
      {deleteNote.error && <p className="error">{deleteNote.error.message}</p>}
      <div className="form-actions">
        <button className="danger" onClick={handleConfirm} disabled={deleteNote.isPending}>
          {deleteNote.isPending ? 'Usuwam…' : 'Usuń'}
        </button>
        <button onClick={onClose} disabled={deleteNote.isPending}>
          Anuluj
        </button>
      </div>
    </Modal>
  );
}
