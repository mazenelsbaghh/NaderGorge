// School types — used in student registration form
// 6 types matching backend SchoolType enum

export const SCHOOL_TYPES = [
  { value: 'Government', label: 'حكومية' },
  { value: 'Language', label: 'لغات' },
  { value: 'Experimental', label: 'تجريبية' },
  { value: 'Private', label: 'خاصة' },
  { value: 'Azhari', label: 'أزهرية' },
  { value: 'American', label: 'أمريكية' },
] as const;

export type SchoolTypeValue = (typeof SCHOOL_TYPES)[number]['value'];
