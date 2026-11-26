import { useState, type ChangeEvent } from 'react';
import { useUploadPhoto } from '../api/hooks';
import { ALLOWED_PHOTO_TYPES, MAX_PHOTO_BYTES } from '../api/notes';
import type { Note } from '../api/types';
import { Modal } from './Modal';

interface Props {
  note: Note;
  onClose: () => void;
}

export function PhotoUploadDialog({ note, onClose }: Props) {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);
  const upload = useUploadPhoto();

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const selected = event.target.files?.[0] ?? null;
    upload.reset();
    setFile(null);
    setPreview(null);
    setFileError(null);

    if (!selected) return;

    // Te same reguły sprawdza API; tutaj tylko po to, żeby nie wysyłać pliku na próżno.
    if (!ALLOWED_PHOTO_TYPES.includes(selected.type)) {
      setFileError('Dozwolone formaty: JPEG, PNG, WebP.');
      return;
    }
    if (selected.size > MAX_PHOTO_BYTES) {
      setFileError('Zdjęcie może mieć maksymalnie 5 MB.');
      return;
    }

    setFile(selected);
    // Podgląd lokalny – plik nie opuszcza jeszcze przeglądarki.
    const reader = new FileReader();
    reader.onload = () => setPreview(reader.result as string);
    reader.readAsDataURL(selected);
  }

  function handleUpload() {
    if (file) {
      upload.mutate({ id: note.id, file }, { onSuccess: onClose });
    }
  }

  const error = fileError ?? upload.error?.message;

  return (
    <Modal title={`Zdjęcie do „${note.title}”`} onClose={onClose}>
      <input
        type="file"
        accept={ALLOWED_PHOTO_TYPES.join(',')}
        onChange={handleFileChange}
        disabled={upload.isPending}
      />
      <p className="hint">JPEG, PNG lub WebP, maksymalnie 5 MB.</p>

      {preview && <img className="preview" src={preview} alt="Podgląd wybranego zdjęcia" />}
      {error && <p className="error">{error}</p>}

      <div className="form-actions">
        <button className="primary" onClick={handleUpload} disabled={!file || upload.isPending}>
          {upload.isPending ? 'Wysyłam…' : 'Wyślij zdjęcie'}
        </button>
        <button onClick={onClose} disabled={upload.isPending}>
          Anuluj
        </button>
      </div>
    </Modal>
  );
}
