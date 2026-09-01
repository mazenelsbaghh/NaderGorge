export type VideoEmbedNavigationError = 'missing-context' | 'unauthorized-origin';

type HeaderReader = Pick<Headers, 'get'>;

export function validateVideoEmbedNavigation(
  requestUrl: string,
  headers: HeaderReader,
): VideoEmbedNavigationError | null {
  const destination = headers.get('sec-fetch-dest');
  const fetchSite = headers.get('sec-fetch-site');
  const referer = headers.get('referer');

  if (destination !== 'iframe' || !referer || !fetchSite) return 'missing-context';
  if (fetchSite !== 'same-origin') return 'unauthorized-origin';

  try {
    return new URL(referer).origin === new URL(requestUrl).origin
      ? null
      : 'unauthorized-origin';
  } catch {
    return 'unauthorized-origin';
  }
}
