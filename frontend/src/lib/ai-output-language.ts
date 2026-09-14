export const AI_OUTPUT_LANGUAGE_OPTIONS = [
  {
    value: 'Auto',
    label: 'تلقائي',
    description: 'يتبع اللغة الغالبة في شرح المدرس.',
  },
  {
    value: 'Arabic',
    label: 'العربية',
    description: 'ينشئ الملخصات والنصوص داخل الصور بالعربية.',
  },
  {
    value: 'English',
    label: 'English',
    description: 'ينشئ الملخصات والنصوص داخل الصور بالإنجليزية.',
  },
] as const;

export type AiOutputLanguage = (typeof AI_OUTPUT_LANGUAGE_OPTIONS)[number]['value'];

export function normalizeAiOutputLanguage(candidate: unknown): AiOutputLanguage {
  return AI_OUTPUT_LANGUAGE_OPTIONS.some((option) => option.value === candidate)
    ? (candidate as AiOutputLanguage)
    : 'Auto';
}
