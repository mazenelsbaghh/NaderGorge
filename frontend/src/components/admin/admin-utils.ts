export function formatCompactNumber(value: number) {
  return new Intl.NumberFormat('en-US').format(value);
}

export function formatDate(value: string | Date, options?: Intl.DateTimeFormatOptions) {
  let dateStr = value instanceof Date ? value.toISOString() : value;
  if (typeof dateStr === 'string' && !dateStr.endsWith('Z') && !dateStr.includes('+') && !/[-+]\d{2}:\d{2}$/.test(dateStr)) {
    dateStr += 'Z';
  }
  return new Intl.DateTimeFormat('en-GB', {
    dateStyle: 'medium',
    ...options
  }).format(new Date(dateStr));
}

export function formatRelativeDate(value: string | Date) {
  let dateStr = value instanceof Date ? value.toISOString() : value;
  if (typeof dateStr === 'string' && !dateStr.endsWith('Z') && !dateStr.includes('+') && !/[-+]\d{2}:\d{2}$/.test(dateStr)) {
    dateStr += 'Z';
  }
  const diffMs = Date.now() - new Date(dateStr).getTime();
  const minutes = Math.max(1, Math.floor(diffMs / 60000));

  if (minutes < 60) return `منذ ${formatCompactNumber(minutes)} دقيقة`;

  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `منذ ${formatCompactNumber(hours)} ساعة`;

  const days = Math.floor(hours / 24);
  if (days === 1) return 'أمس';
  if (days < 30) return `منذ ${formatCompactNumber(days)} يوم`;

  return formatDate(dateStr);
}

export function getInitials(name: string) {
  if (!name) return '';
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join('');
}
