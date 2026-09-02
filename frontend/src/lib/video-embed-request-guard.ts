export type VideoEmbedNavigationError = 'missing-context' | 'unauthorized-origin';

type HeaderReader = Pick<Headers, 'get'>;

const APPROVED_FORWARDED_APP_ORIGINS = new Set([
  'https://app.massar-academy.net',
  'https://admin.massar-academy.net',
]);

function requestOrigins(requestUrl: string, headers: HeaderReader) {
  const origins = new Set([new URL(requestUrl).origin]);
  const forwardedHost = headers.get('x-forwarded-host')?.split(',')[0]?.trim();
  const forwardedProto = headers.get('x-forwarded-proto')?.split(',')[0]?.trim();

  if (forwardedHost && forwardedProto) {
    const forwardedOrigin = new URL(`${forwardedProto}://${forwardedHost}`).origin;
    if (APPROVED_FORWARDED_APP_ORIGINS.has(forwardedOrigin)) origins.add(forwardedOrigin);
  }

  return origins;
}

export function validateVideoEmbedNavigation(
  requestUrl: string,
  headers: HeaderReader,
): VideoEmbedNavigationError | null {
  const destination = headers.get('sec-fetch-dest');
  const fetchSite = headers.get('sec-fetch-site');
  const referer = headers.get('referer');

  if (destination !== 'iframe' || !referer || !fetchSite) return 'missing-context';
  if (fetchSite !== 'same-origin' && fetchSite !== 'same-site') {
    return 'unauthorized-origin';
  }

  try {
    return requestOrigins(requestUrl, headers).has(new URL(referer).origin)
      ? null
      : 'unauthorized-origin';
  } catch {
    return 'unauthorized-origin';
  }
}
