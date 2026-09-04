export interface SupportScheduleWindowLike {
  dayOfWeek: number;
  startLocalTime: string;
  endLocalTime: string;
}

function parseLocalTime(value: string): number | null {
  const match = /^(\d{2}):(\d{2})(?::(\d{2}))?$/.exec(value);
  if (!match) return null;

  const hours = Number(match[1]);
  const minutes = Number(match[2]);
  const seconds = Number(match[3] ?? 0);
  if (hours > 23 || minutes > 59 || seconds > 59) return null;

  return hours * 3600 + minutes * 60 + seconds;
}

export function isValidSupportScheduleWindow(window: SupportScheduleWindowLike): boolean {
  const start = parseLocalTime(window.startLocalTime);
  const end = parseLocalTime(window.endLocalTime);

  return Number.isInteger(window.dayOfWeek)
    && window.dayOfWeek >= 0
    && window.dayOfWeek <= 6
    && start !== null
    && end !== null
    && start !== end;
}
