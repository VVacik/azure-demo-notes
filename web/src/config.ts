// Vite podmienia import.meta.env na stałe w trakcie budowania – adres API trafia do plików JS w dist.
const apiUrl = import.meta.env.VITE_API_URL;

if (!apiUrl) {
  throw new Error(
    'Brak VITE_API_URL. Skopiuj .env.example do .env.local albo ustaw zmienną przed buildem.',
  );
}

export const API_URL = apiUrl.replace(/\/+$/, '');
