export function publicExamManagementPath(pathname: string): string {
  return pathname === '/assistant/public-exams' || pathname.startsWith('/assistant/public-exams/')
    ? '/assistant/public-exams'
    : '/admin/public-exams';
}
