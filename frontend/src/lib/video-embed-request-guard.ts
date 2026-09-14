export type VideoEmbedNavigationError = 'missing-context' | 'unauthorized-origin';

type HeaderReader = Pick<Headers, 'get'>;

const APPROVED_FORWARDED_APP_ORIGINS = new Set([
  'https://app.massar-academy.net',
  'https://admin.massar-academy.net',
  'https://teacher.massar-academy.net',
  'https://staff.massar-academy.net',
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
  if (destination && destination !== 'iframe') return 'missing-context';
  return validateVideoMediaRequest(requestUrl, headers);
}

export function validateVideoMediaRequest(requestUrl: string, headers: HeaderReader): VideoEmbedNavigationError | null {
  const fetchSite = headers.get('sec-fetch-site');
  if (fetchSite && fetchSite !== 'same-origin' && fetchSite !== 'same-site' && fetchSite !== 'none') {
    return 'unauthorized-origin';
  }

  // Embedded browsers may omit Fetch Metadata and Referer. Authorization remains
  // mandatory in each route through the signed cookie/JWT and backend access check.
  const contexts = [headers.get('origin'), headers.get('referer')].filter(
    (value): value is string => Boolean(value) && value !== 'null',
  );
  try {
    const allowedOrigins = requestOrigins(requestUrl, headers);
    return contexts.every(value => allowedOrigins.has(new URL(value).origin)) ? null : 'unauthorized-origin';
  } catch {
    return 'unauthorized-origin';
  }
}
