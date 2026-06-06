import { getSurfaceOrigins } from '@/packages/surface-runtime/config';

/**
 * Resolves a relative path to an absolute URL pointing to the landing/student domain.
 * Ensures the user is redirected or linked to the non-admin domain.
 */
export function getAbsoluteLandingUrl(path: string): string {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;

  if (typeof window === 'undefined') {
    try {
      const { landing } = getSurfaceOrigins();
      return `${landing}${normalizedPath}`;
    } catch {
      return `http://localhost:8738${normalizedPath}`;
    }
  }

  const hostname = window.location.hostname;
  const protocol = window.location.protocol;

  // Local development
  if (hostname === 'localhost' || hostname === '127.0.0.1') {
    return `${protocol}//${hostname}:8738${normalizedPath}`;
  }

  // Production - strip 'admin.' subdomain to route to the main landing/student surface
  if (hostname.startsWith('admin.')) {
    const mainHost = hostname.substring(6);
    return `${protocol}//${mainHost}${normalizedPath}`;
  }

  const mainDomain = process.env.NEXT_PUBLIC_APP_DOMAIN || 'bsma-academy.com';
  return `${protocol}//${mainDomain}${normalizedPath}`;
}
