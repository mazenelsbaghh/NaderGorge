const DEFAULT_API_BASE_URL = 'http://localhost:5245/api';

export function resolveMediaUrl(url?: string | null): string {
  if (!url) return '';

  let normalized = url.trim();
  if (!normalized) return '';

  // Strip trailing slash if it ends with one
  if (normalized.endsWith('/')) {
    normalized = normalized.slice(0, -1);
  }

  if (/^(https?:|data:|blob:)/i.test(normalized)) {
    return normalized;
  }

  const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL || DEFAULT_API_BASE_URL;

  try {
    const backendOrigin = new URL(apiBaseUrl).origin;
    const isProduction =
      !backendOrigin.includes('localhost') &&
      !backendOrigin.includes('127.0.0.1') &&
      !backendOrigin.includes('backend:5245');

    const base = isProduction ? 'https://assets.massar-academy.net' : backendOrigin;
    const path = normalized.startsWith('/') ? normalized : `/${normalized}`;
    return `${base}${path}`;
  } catch {
    return normalized;
  }
}
