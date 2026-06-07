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
    return new URL(normalized.startsWith('/') ? normalized : `/${normalized}`, backendOrigin).toString();
  } catch {
    return normalized;
  }
}
