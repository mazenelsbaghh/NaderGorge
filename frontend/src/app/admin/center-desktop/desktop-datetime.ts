// Flutter's legacy local DateTime values already contain the center's wall-clock
// time. Only timestamps with an explicit offset are converted to Cairo.
function desktopDate(value: string) {
  const zoned = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value);
  const instant = new Date(zoned ? value : `${value}Z`);
  return { instant, timeZone: zoned ? 'Africa/Cairo' : 'UTC' };
}

export function desktopTimestamp(value: string) {
  const { instant, timeZone } = desktopDate(value);
  if (!Number.isFinite(instant.getTime())) return 'التاريخ غير متوفر';
  return new Intl.DateTimeFormat('ar-EG-u-nu-latn', { timeZone, dateStyle: 'short', timeStyle: 'short', hourCycle: 'h23' }).format(instant);
}

export function desktopClock(value: string) {
  const { instant, timeZone } = desktopDate(value);
  if (!Number.isFinite(instant.getTime())) return 'الوقت غير متوفر';
  return new Intl.DateTimeFormat('ar-EG-u-nu-latn', { timeZone, hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(instant);
}
