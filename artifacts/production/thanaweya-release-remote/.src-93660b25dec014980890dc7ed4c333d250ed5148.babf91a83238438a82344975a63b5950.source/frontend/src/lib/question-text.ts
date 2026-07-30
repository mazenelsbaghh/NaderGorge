import { sanitizeRichHtml } from '@/lib/sanitize-html';

function decodeHtmlEntities(value: string) {
  if (!value) return '';
  if (typeof window === 'undefined' || typeof document === 'undefined') {
    return value
      .replaceAll('&lt;', '<')
      .replaceAll('&gt;', '>')
      .replaceAll('&amp;', '&')
      .replaceAll('&quot;', '"')
      .replaceAll('&#039;', "'");
  }

  const textarea = document.createElement('textarea');
  textarea.innerHTML = value;
  return textarea.value;
}

export function normalizeQuestionRichText(value: string | null | undefined) {
  if (!value) return '';
  const decoded = decodeHtmlEntities(value);
  return sanitizeRichHtml(decoded);
}

export function questionTextToPlainText(value: string | null | undefined) {
  const normalized = normalizeQuestionRichText(value);
  if (!normalized) return '';

  if (typeof window === 'undefined' || typeof DOMParser === 'undefined') {
    return normalized.replace(/<[^>]*>/g, ' ').replace(/\s+/g, ' ').trim();
  }

  const parser = new DOMParser();
  const parsed = parser.parseFromString(normalized, 'text/html');
  return (parsed.body.textContent || '').replace(/\s+/g, ' ').trim();
}
