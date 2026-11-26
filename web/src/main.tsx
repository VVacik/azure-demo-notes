import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { App } from './App';
import './index.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Każde pobranie listy daje nowe adresy SAS, więc przeglądarka pobrałaby zdjęcia od nowa.
      // Nie odświeżamy listy przy powrocie do karty; i tak robimy to po każdej zmianie.
      staleTime: 5 * 60_000,
      refetchOnWindowFocus: false,
    },
  },
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </StrictMode>,
);
