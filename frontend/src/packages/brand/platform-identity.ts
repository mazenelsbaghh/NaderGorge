export const PLATFORM_IDENTITY = {
  arabicName: 'منصة مسار',
  englishName: 'Massar Platform',
  logo: {
    full: '/images/logo.svg',
    mark: '/images/logo-mark.svg',
    markLight: '/images/logo-mark-light.svg',
    alt: 'شعار منصة مسار',
  },
} as const;

export const ROLE_TRANSLATIONS: Record<string, string> = {
  'Admin': 'مدير',
  'Teacher': 'معلم',
  'Assistant': 'مساعد تعليمي عام',
  'Student': 'طالب',
  'AssistantReviewer': 'مساعد مصحح',
  'AssistantAcademic': 'مساعد أكاديمي',
  'Supervisor': 'مشرف',
  'Staff': 'موظف',
};

export function translateRole(role: string): string {
  return ROLE_TRANSLATIONS[role] || role;
}

