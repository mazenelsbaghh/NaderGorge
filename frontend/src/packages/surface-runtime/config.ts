export type SurfaceName = 'landing' | 'student' | 'admin' | 'all';

export type RouteBoundaryAction = 'next' | 'rewrite' | 'redirect';

export interface SurfaceOrigins {
  landing: string;
  student: string;
  admin: string;
  mainDomain: string;
}

export interface RouteBoundaryInput {
  surface: SurfaceName;
  pathname: string;
  search?: string;
  host?: string;
}

export interface RouteBoundaryDecision {
  action: RouteBoundaryAction;
  destination?: string;
  surface: SurfaceName;
}

const AUTH_PATHS = new Set(['/login', '/register', '/forgot-password']);

function normalizeOrigin(origin: string) {
  return origin.endsWith('/') ? origin.slice(0, -1) : origin;
}

export function getSurfaceName(value = process.env.APP_SURFACE || process.env.NEXT_PUBLIC_APP_SURFACE): SurfaceName {
  if (value === 'landing' || value === 'student' || value === 'admin' || value === 'all') {
    return value;
  }

  return 'all';
}

export function getSurfaceOrigins(): SurfaceOrigins {
  return {
    landing: normalizeOrigin(process.env.LANDING_PUBLIC_ORIGIN || 'http://localhost:8738'),
    student: normalizeOrigin(process.env.STUDENT_PUBLIC_ORIGIN || 'http://localhost:8739'),
    admin: normalizeOrigin(process.env.ADMIN_PUBLIC_ORIGIN || 'http://localhost:8740'),
    mainDomain: process.env.NEXT_PUBLIC_APP_DOMAIN || 'masarplatform.com',
  };
}

export function createRedirectUrl(origin: string, pathname: string, search = '') {
  const normalizedPath = pathname.startsWith('/') ? pathname : `/${pathname}`;
  return `${normalizeOrigin(origin)}${normalizedPath}${search}`;
}

export function getRouteBoundaryDecision(input: RouteBoundaryInput): RouteBoundaryDecision {
  const { surface, pathname, search = '' } = input;
  const origins = getSurfaceOrigins();

  if (surface === 'landing') {
    if (pathname.startsWith('/student')) {
      return {
        action: 'redirect',
        destination: createRedirectUrl(origins.student, pathname, search),
        surface,
      };
    }

    if (pathname.startsWith('/admin')) {
      return {
        action: 'redirect',
        destination: createRedirectUrl(origins.admin, pathname, search),
        surface,
      };
    }

    if (AUTH_PATHS.has(pathname)) {
      return {
        action: 'redirect',
        destination: createRedirectUrl(origins.student, pathname, search),
        surface,
      };
    }

    return { action: 'next', surface };
  }

  if (surface === 'student') {
    if (pathname === '/') {
      return { action: 'rewrite', destination: '/student', surface };
    }

    if (pathname.startsWith('/admin')) {
      return {
        action: 'redirect',
        destination: createRedirectUrl(origins.admin, pathname, search),
        surface,
      };
    }

    return { action: 'next', surface };
  }

  if (surface === 'admin') {
    if (pathname === '/') {
      return { action: 'rewrite', destination: '/admin', surface };
    }

    if (pathname.startsWith('/student')) {
      return {
        action: 'redirect',
        destination: createRedirectUrl(origins.student, pathname, search),
        surface,
      };
    }

    if (pathname === '/register') {
      return {
        action: 'redirect',
        destination: createRedirectUrl(origins.student, pathname, search),
        surface,
      };
    }

    return { action: 'next', surface };
  }

  return { action: 'next', surface };
}
