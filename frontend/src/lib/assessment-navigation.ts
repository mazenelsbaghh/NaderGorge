/** Keep shared assessment pages inside the workspace that opened them. */
export function assessmentContentPath(pathname: string, surface: 'admin' | 'teacher' = 'admin'): string {
  if (pathname === '/assistant/content' || pathname.startsWith('/assistant/content/')) {
    return '/assistant/content';
  }
  return surface === 'teacher' ? '/teacher/packages' : '/admin/content';
}
