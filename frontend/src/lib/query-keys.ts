export const queryKeys = {
  session: ['session'] as const,
  student: {
    all: ['student'] as const,
    shell: (userBoundary: string) => ['student', 'shell', userBoundary] as const,
    dashboard: (userBoundary: string) => ['student', 'dashboard', userBoundary] as const,
    quickAccess: (userBoundary: string) => ['student', 'quick-access', userBoundary] as const,
    packages: (userBoundary: string, filters: Record<string, unknown> = {}) =>
      ['student', 'packages', userBoundary, normalizeQueryParameters(filters)] as const,
    teachers: (userBoundary: string, filters: Record<string, unknown> = {}) =>
      ['student', 'teachers', userBoundary, normalizeQueryParameters(filters)] as const,
  },
  admin: {
    all: ['admin'] as const,
    students: (parameters: {
      page: number;
      pageSize: number;
      search?: string;
      sort?: string;
    }) => ['admin', 'students', normalizeQueryParameters(parameters)] as const,
  },
  support: {
    all: ['support'] as const,
    dashboard: ['support', 'dashboard'] as const,
    staff: ['support', 'staff'] as const,
    conversation: (conversationId: string) =>
      ['support', 'conversation', conversationId] as const,
    studentHistory: (studentId: string, cursor?: string) =>
      ['support', 'student-history', studentId, cursor ?? null] as const,
  },
  employees: {
    all: ['employees'] as const,
    list: (filter = 'all') => ['employees', 'list', filter] as const,
    detail: (id: string) => ['employees', 'detail', id] as const,
  },
  hr: {
    all: ['hr'] as const,
    employees: ['hr', 'employees'] as const,
    organization: ['hr', 'organization'] as const,
    contracts: ['hr', 'contracts'] as const,
    shifts: ['hr', 'shifts'] as const,
    attendance: ['hr', 'attendance'] as const,
    corrections: ['hr', 'corrections'] as const,
    leave: ['hr', 'leave'] as const,
    approvals: ['hr', 'approvals'] as const,
    payroll: ['hr', 'payroll'] as const,
    financialRequests: ['hr', 'financial-requests'] as const,
    documents: ['hr', 'documents'] as const,
    assets: ['hr', 'assets'] as const,
    performance: ['hr', 'performance'] as const,
    cases: ['hr', 'cases'] as const,
    recruitment: ['hr', 'recruitment'] as const,
    lifecycle: ['hr', 'lifecycle'] as const,
    migration: ['hr', 'migration'] as const,
    reports: ['hr', 'reports'] as const,
  },
  content: {
    packages: ['content', 'packages'] as const,
    lessons: (id?: string) => id ? ['content', 'lessons', id] as const : ['content', 'lessons'] as const,
  },
  finance: ['finance'] as const,
  assessments: ['assessments'] as const,
  community: ['community', 'posts'] as const,
  notifications: ['notifications'] as const,
} as const;

export type PlatformQueryKey = readonly unknown[];

export function normalizeQueryParameters<T extends Record<string, unknown>>(value: T) {
  return Object.fromEntries(
    Object.entries(value)
      .filter(([, entry]) => entry !== undefined)
      .map(
        ([key, entry]): [string, unknown] => [
          key,
          typeof entry === 'string' ? entry.trim() : entry,
        ]
      )
      .sort(([left], [right]) => left.localeCompare(right))
  ) as Partial<T>;
}

export function queryKeyStartsWith(
  candidate: PlatformQueryKey,
  prefix: PlatformQueryKey
) {
  if (prefix.length > candidate.length) return false;
  return prefix.every(
    (segment, index) =>
      stableSerializeQueryKey([segment]) ===
      stableSerializeQueryKey([candidate[index]])
  );
}

export function stableSerializeQueryKey(queryKey: PlatformQueryKey): string {
  return JSON.stringify(queryKey, (_key, value) => {
    if (!value || typeof value !== 'object' || Array.isArray(value)) return value;
    return Object.fromEntries(
      Object.entries(value as Record<string, unknown>).sort(([left], [right]) =>
        left.localeCompare(right)
      )
    );
  });
}
