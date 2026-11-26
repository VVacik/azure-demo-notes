import { useHealth } from '../api/hooks';
import { API_URL } from '../config';

// Pokazuje, z którym API rozmawia frontend i czy działa – przydatne po wdrożeniu do Azure.
export function ApiStatus() {
  const { data, isPending, isError } = useHealth();

  let state: 'checking' | 'ok' | 'degraded' | 'down';
  let label: string;

  if (isPending) {
    state = 'checking';
    label = 'Sprawdzam API…';
  } else if (isError) {
    state = 'down';
    label = 'API niedostępne';
  } else if (data.status === 'Healthy') {
    state = 'ok';
    label = 'API działa';
  } else {
    const failing = Object.entries(data.checks)
      .filter(([, status]) => status !== 'Healthy')
      .map(([name]) => name);
    state = 'degraded';
    label = `API: problem (${failing.join(', ')})`;
  }

  return (
    <div className={`api-status api-status--${state}`} title={API_URL}>
      <span className="api-status__dot" aria-hidden="true" />
      <span>{label}</span>
      <small>{new URL(API_URL).host}</small>
    </div>
  );
}
