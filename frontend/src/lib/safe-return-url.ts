export type ReturnSurface =
  | 'landing'
  | 'student'
  | 'admin'
  | 'teacher'
  | 'assistant'
  | 'all';

const CONTROL_OR_WHITESPACE = /[\u0000-\u0020\u007f]/;
const ENCODED_SEPARATOR = /%(?:2f|5c)/i;
const AUTH_ROUTES = new Set(['/login', '/register', '/forgot-password']);

const SURFACE_ROOTS: Record<Exclude<ReturnSurface, 'all'>, readonly string[]> = {
  landing: [
    '/',
    '/about',
    '/faq',
    '/forms',
    '/packages',
    '/parent',
    '/parent-report',
    '/qr',
    '/teachers',
    '/thanaweya-results',
  ],
  student: ['/student', '/onboarding', '/parent', '/forms', '/qr'],
  admin: ['/admin'],
  teacher: ['/teacher'],
  assistant: ['/assistant', '/employee'],
};

function pathMatchesRoot(pathname: string, root: string): boolean {
  if (root === '/') return pathname === '/';
  return pathname === root || pathname.startsWith(`${root}/`);
}

export function parseSafeReturnUrl(
  candidate: string | null | undefined,
  surface: ReturnSurface
): string | null {
  if (
    !candidate ||
    !candidate.startsWith('/') ||
    candidate.startsWith('//') ||
    candidate.includes('\\') ||
    CONTROL_OR_WHITESPACE.test(candidate) ||
    ENCODED_SEPARATOR.test(candidate)
  ) {
    return null;
  }

  let parsed: URL;
  try {
    parsed = new URL(candidate, 'https://return.invalid');
  } catch {
    return null;
  }

  if (parsed.origin !== 'https://return.invalid') return null;
  if (AUTH_ROUTES.has(parsed.pathname)) return null;

  const allowed =
    surface === 'all' ||
    SURFACE_ROOTS[surface].some((root) =>
      pathMatchesRoot(parsed.pathname, root)
    );
  if (!allowed) return null;

  return `${parsed.pathname}${parsed.search}${parsed.hash}`;
}

export interface ReturnNavigationInput {
  returnUrl?: string | null;
  defaultDestination: string;
  surface: ReturnSurface;
  currentOrigin: string;
}

export interface ReturnNavigation {
  href: string;
  sameOrigin: boolean;
  source: 'return-url' | 'default';
}

export function resolveReturnNavigation({
  returnUrl,
  defaultDestination,
  surface,
  currentOrigin,
}: ReturnNavigationInput): ReturnNavigation {
  const safeReturn = parseSafeReturnUrl(returnUrl, surface);
  if (safeReturn) {
    return {
      href: safeReturn,
      sameOrigin: true,
      source: 'return-url',
    };
  }

  try {
    const destination = new URL(defaultDestination, currentOrigin);
    return {
      href:
        destination.origin === currentOrigin
          ? `${destination.pathname}${destination.search}${destination.hash}`
          : destination.toString(),
      sameOrigin: destination.origin === currentOrigin,
      source: 'default',
    };
  } catch {
    return { href: '/', sameOrigin: true, source: 'default' };
  }
}
