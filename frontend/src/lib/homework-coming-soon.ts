import { cairoDateAfterDays } from './cairo-time.ts';

const dateOnlyPattern = /^(\d{4})-(\d{2})-(\d{2})$/;
const futureDateFormatter = new Intl.DateTimeFormat('ar-EG-u-nu-latn', {
  year: 'numeric',
  month: 'long',
  day: 'numeric',
  timeZone: 'UTC',
});

function parseDateOnly(dateOnly: string) {
  const dateMatch = dateOnlyPattern.exec(dateOnly);
  if (!dateMatch) return null;

  const [, yearText, monthText, dayText] = dateMatch;
  const year = Number(yearText);
  const month = Number(monthText);
  const day = Number(dayText);
  const calendarDate = new Date(0);
  calendarDate.setUTCFullYear(year, month - 1, day);
  calendarDate.setUTCHours(12, 0, 0, 0);

  return year > 0 &&
    calendarDate.getUTCFullYear() === year &&
    calendarDate.getUTCMonth() === month - 1 &&
    calendarDate.getUTCDate() === day
    ? calendarDate
    : null;
}

export function getDefaultHomeworkComingSoonDate(now = new Date()) {
  return cairoDateAfterDays(1, now);
}

export function getHomeworkComingSoonLabel(
  expectedOn?: string | null,
  now = new Date()
) {
  const normalizedExpectedOn = expectedOn?.trim();
  if (!normalizedExpectedOn) return null;

  const expectedDate = parseDateOnly(normalizedExpectedOn);
  const today = cairoDateAfterDays(0, now);
  if (!expectedDate || normalizedExpectedOn < today) return 'سيظهر قريبًا';
  if (normalizedExpectedOn === today) return 'سيظهر اليوم';
  if (normalizedExpectedOn === cairoDateAfterDays(1, now)) return 'سيظهر غدًا';

  return `سيظهر يوم ${futureDateFormatter.format(expectedDate)}`;
}
