export type SurfaceName = 'landing' | 'student' | 'admin' | 'teacher' | 'assistant' | 'all';

export type RouteBoundaryAction = 'next' | 'rewrite' | 'redirect';

export interface SurfaceOrigins {
  landing: string;
  student: string;
  admin: string;
  teacher: string;
  assistant: string;
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

export function getSurfaceName(value?: string): SurfaceName {
  const envVal = process.env.APP_SURFACE || process.env.NEXT_PUBLIC_APP_SURFACE;
  if (envVal && envVal !== 'all') {
    return envVal as SurfaceName;
  }

  let host = '';
  if (typeof window !== 'undefined') {
    host = window.location.host;
  } else if (value) {
    host = value;
  }

  if (!host) {
    return 'all';
  }

  // 1. Port detection
  const port = host.split(':')[1];
  if (port === '8738') return 'landing';
  if (port === '8739') return 'student';
  if (port === '8740') return 'admin';
  if (port === '8741') return 'teacher';
  if (port === '8742') return 'assistant';

  // 2. Subdomain detection
  const domainOnly = host.split(':')[0];
  if (domainOnly.startsWith('admin.') || domainOnly.startsWith('super.')) return 'admin';
  if (domainOnly.startsWith('app.') || domainOnly.startsWith('student.')) return 'student';
  if (domainOnly.startsWith('teacher.')) return 'teacher';
  if (domainOnly.startsWith('staff.') || domainOnly.startsWith('assistant.')) return 'assistant';

  return 'landing';
}

export function getSurfaceOrigins(requestHost?: string): SurfaceOrigins {
  const mainDomain = process.env.NEXT_PUBLIC_APP_DOMAIN || 'massar-academy.net';

  let hostname = '';
  let protocol = 'https:';

  if (typeof window !== 'undefined') {
    hostname = window.location.host;
    protocol = window.location.protocol;
  } else if (requestHost) {
    hostname = requestHost;
    protocol = hostname.includes('localhost') || hostname.includes('127.0.0.1') ? 'http:' : 'https:';
  } else {
    return {
      landing: normalizeOrigin(process.env.LANDING_PUBLIC_ORIGIN || 'http://localhost:8738'),
      student: normalizeOrigin(process.env.STUDENT_PUBLIC_ORIGIN || 'http://localhost:8739'),
      admin: normalizeOrigin(process.env.ADMIN_PUBLIC_ORIGIN || 'http://localhost:8740'),
      teacher: normalizeOrigin(process.env.TEACHER_PUBLIC_ORIGIN || 'http://localhost:8741'),
      assistant: normalizeOrigin(process.env.ASSISTANT_PUBLIC_ORIGIN || 'http://localhost:8742'),
      mainDomain,
    };
  }

  const isLocal = hostname.includes('localhost') || hostname.includes('127.0.0.1') || hostname.startsWith('192.168.') || hostname.startsWith('10.');

  if (!isLocal) {
    const domainOnly = hostname.split(':')[0];
    const hostParts = domainOnly.split('.');
    const detectedDomain = hostParts.length >= 2 ? hostParts.slice(-2).join('.') : domainOnly;

    return {
      landing: `${protocol}//${detectedDomain}`,
      student: `${protocol}//app.${detectedDomain}`,
      admin: `${protocol}//admin.${detectedDomain}`,
      teacher: `${protocol}//teacher.${detectedDomain}`,
      assistant: `${protocol}//staff.${detectedDomain}`,
      mainDomain: detectedDomain,
    };
  }

  // Local dev: port matching or subdomain matching
  const port = hostname.split(':')[1];

  if (port && ['8738', '8739', '8740', '8741', '8742'].includes(port)) {
    const hostWithoutPort = hostname.split(':')[0];
    return {
      landing: `${protocol}//${hostWithoutPort}:8738`,
      student: `${protocol}//${hostWithoutPort}:8739`,
      admin: `${protocol}//${hostWithoutPort}:8740`,
      teacher: `${protocol}//${hostWithoutPort}:8741`,
      assistant: `${protocol}//${hostWithoutPort}:8742`,
      mainDomain,
    };
  }

  // Single port (e.g. 3000) with subdomains
  const hostWithoutPort = hostname.split(':')[0];
  const baseHost = hostWithoutPort.replace(/^(admin|super|app|student|teacher|staff|assistant)\./, '');
  const activePort = port ? `:${port}` : '';

  return {
    landing: `${protocol}//${baseHost}${activePort}`,
    student: `${protocol}//app.${baseHost}${activePort}`,
    admin: `${protocol}//admin.${baseHost}${activePort}`,
    teacher: `${protocol}//teacher.${baseHost}${activePort}`,
    assistant: `${protocol}//staff.${baseHost}${activePort}`,
    mainDomain,
  };
}

export function createRedirectUrl(origin: string, pathname: string, search = '') {
  const normalizedPath = pathname.startsWith('/') ? pathname : `/${pathname}`;
  return `${normalizeOrigin(origin)}${normalizedPath}${search}`;
}

export function getRouteBoundaryDecision(input: RouteBoundaryInput): RouteBoundaryDecision {
  const { surface, pathname, search = '', host } = input;
  const origins = getSurfaceOrigins(host);

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

    if (pathname.startsWith('/teacher')) {
      return {
        action: 'redirect',
        destination: createRedirectUrl(origins.teacher, pathname, search),
        surface,
      };
    }

    if (pathname.startsWith('/assistant')) {
      return {
        action: 'redirect',
        destination: createRedirectUrl(origins.assistant, pathname, search),
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

    if (pathname.startsWith('/admin') || pathname.startsWith('/teacher') || pathname.startsWith('/assistant')) {
      return {
        action: 'rewrite',
        destination: '/not-found',
        surface,
      };
    }

    return { action: 'next', surface };
  }

  if (surface === 'admin') {
    if (pathname === '/') {
      return { action: 'rewrite', destination: '/admin', surface };
    }

    if (pathname.startsWith('/student') || pathname.startsWith('/teacher') || pathname.startsWith('/assistant')) {
      return {
        action: 'rewrite',
        destination: '/not-found',
        surface,
      };
    }

    if (pathname === '/register') {
      return {
        action: 'rewrite',
        destination: '/not-found',
        surface,
      };
    }

    return { action: 'next', surface };
  }

  if (surface === 'teacher') {
    if (pathname === '/') {
      return { action: 'rewrite', destination: '/teacher', surface };
    }

    if (pathname.startsWith('/student') || pathname.startsWith('/admin') || pathname.startsWith('/assistant')) {
      return {
        action: 'rewrite',
        destination: '/not-found',
        surface,
      };
    }

    return { action: 'next', surface };
  }

  if (surface === 'assistant') {
    if (pathname === '/') {
      return { action: 'rewrite', destination: '/assistant', surface };
    }

    if (pathname.startsWith('/student') || pathname.startsWith('/teacher')) {
      return {
        action: 'rewrite',
        destination: '/not-found',
        surface,
      };
    }

    return { action: 'next', surface };
  }

  return { action: 'next', surface };
}

export function isValidRedirectUrl(url: string, surface: SurfaceName): boolean {
  if (!url.startsWith('/') || url.startsWith('//')) {
    return false;
  }

  if (surface === 'all' || surface === 'landing') {
    return true;
  }

  if (surface === 'student' && (url.startsWith('/student') || url.startsWith('/onboarding') || url.startsWith('/parent'))) return true;
  if (surface === 'teacher' && url.startsWith('/teacher')) return true;
  if (surface === 'assistant' && url.startsWith('/assistant')) return true;
  if (surface === 'admin' && url.startsWith('/admin')) return true;

  return false;
}
