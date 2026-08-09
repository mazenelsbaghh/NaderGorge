'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { isAxiosError } from 'axios';
import { AlertCircle, Clock3, Coffee, LogIn, LogOut, MapPin, RefreshCw, Timer } from 'lucide-react';
import toast from 'react-hot-toast';
import { AttendanceSessionDto, hrService } from '@/services/hr-service';
import { createClientId } from '@/lib/client-id';
import { AttendanceCorrectionForm } from './AttendanceCorrectionForm';
import { formatCairoDateTime, parseUtcDateTime } from '@/lib/cairo-time';

const errorMessages: Record<string, string> = {
  OUTSIDE_GEOFENCE: 'أنت خارج نطاق موقع العمل المسموح.', LOCATION_ACCURACY_LOW: 'دقة الموقع غير كافية؛ فعّل GPS وحاول مجددًا.',
  DEVICE_NOT_TRUSTED: 'هذا الجهاز غير مسجل كجهاز موثوق.', NO_SCHEDULE: 'لا يوجد شفت منشور لك اليوم.',
  SESSION_ALREADY_OPEN: 'لديك جلسة حضور مفتوحة بالفعل.', NO_OPEN_SESSION: 'لا توجد جلسة حضور مفتوحة.', BREAK_ALREADY_OPEN: 'لديك استراحة مفتوحة بالفعل.',
  ADMIN_ATTENDANCE_NOT_APPLICABLE: 'الحضور غير مطبق على المدير العام.',
};

function formatDuration(totalMinutes: number) {
  const safeMinutes = Math.max(0, totalMinutes);
  return `${Math.floor(safeMinutes / 60)}س ${safeMinutes % 60}د`;
}

function formatSessionDuration(totalMinutes: number, clockedInAt: string, clockedOutAt?: string | null) {
  if (totalMinutes > 0) return formatDuration(totalMinutes);
  const endedAt = clockedOutAt ? parseUtcDateTime(clockedOutAt).getTime() : Date.now();
  return endedAt > parseUtcDateTime(clockedInAt).getTime() ? 'أقل من دقيقة' : formatDuration(0);
}

export function AttendanceWorkspace() {
  const [isGeneralAdmin, setIsGeneralAdmin] = useState(false);
  const [today, setToday] = useState<AttendanceSessionDto | null>(null);
  const [history, setHistory] = useState<AttendanceSessionDto[]>([]);
  const [loading, setLoading] = useState(true); const [acting, setActing] = useState(false); const [error, setError] = useState<string | null>(null);
  const [now, setNow] = useState(() => Date.now());
  const [serverClockOffsetMs, setServerClockOffsetMs] = useState(0);
  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [current, rows] = await Promise.all([hrService.getAttendanceToday(), hrService.getAttendanceHistory()]);
      const receivedAt = Date.now();
      setIsGeneralAdmin(false);
      setToday(current);
      setHistory(rows);
      setNow(receivedAt);
      if (current?.serverNowUtc) setServerClockOffsetMs(receivedAt - parseUtcDateTime(current.serverNowUtc).getTime());
    } catch (cause: unknown) {
      const code = isAxiosError<{ errors?: string[] }>(cause) ? cause.response?.data?.errors?.[0] : undefined;
      if (code === 'ADMIN_ATTENDANCE_NOT_APPLICABLE') setIsGeneralAdmin(true);
      else setError('تعذر تحميل سجل الحضور.');
    } finally {
      setLoading(false);
    }
  }, []);
  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    if (!today?.clockedInAt || today.clockedOutAt) return;
    const timer = window.setInterval(() => setNow(Date.now()), 60_000);
    return () => window.clearInterval(timer);
  }, [today?.clockedInAt, today?.clockedOutAt]);
  const openBreak = today?.breaks?.find((item) => !item.endedAt);
  const used = (kind: 'Regular' | 'ShortPermission') => today?.breaks?.filter((item) => item.kind === kind && item.endedAt).reduce((sum, item) => sum + Math.floor((parseUtcDateTime(item.endedAt!).getTime() - parseUtcDateTime(item.startedAt).getTime()) / 60000), 0) ?? 0;
  const deviceToken = useMemo(() => { if (typeof window === 'undefined') return ''; const key = 'hr-attendance-device-token'; const existing = localStorage.getItem(key); if (existing) return existing; const next = createClientId(); localStorage.setItem(key, next); return next; }, []);
  const todayElapsedMinutes = today?.clockedInAt
    ? Math.max(today.workedMinutes, Math.floor(((today.clockedOutAt ? parseUtcDateTime(today.clockedOutAt).getTime() : now - serverClockOffsetMs) - parseUtcDateTime(today.clockedInAt).getTime()) / 60_000))
    : 0;
  const run = async (action: () => Promise<unknown>) => {
    setActing(true);
    setError(null);
    try {
      await action();
      await load();
    } catch (cause: unknown) {
      const payload = isAxiosError<{ errors?: string[]; message?: string }>(cause)
        ? cause.response?.data
        : undefined;
      const code = payload?.errors?.[0];
      const message = (code ? errorMessages[code] : undefined) ?? payload?.message ?? 'تعذر تنفيذ العملية.';
      setError(message);
      toast.error(message);
    } finally {
      setActing(false);
    }
  };
  const clockIn = () => run(async () => { const position = await new Promise<GeolocationPosition | null>((resolve) => { if (!navigator.geolocation) return resolve(null); navigator.geolocation.getCurrentPosition(resolve, () => resolve(null), { enableHighAccuracy: true, timeout: 10000 }); }); await hrService.clockInSecure({ latitude: position?.coords.latitude, longitude: position?.coords.longitude, accuracy: position?.coords.accuracy, deviceToken }); toast.success('تم تسجيل الحضور'); });

  if (isGeneralAdmin) return <section className="admin-panel py-14 text-center" dir="rtl"><h2 className="text-xl font-black">الحضور غير مطبق على المدير العام</h2><p className="mt-2 text-sm font-bold text-[var(--admin-muted)]">إدارة الحضور مخصصة للموظفين فقط، ولا تُنشأ للمدير العام ورديات أو سجلات حضور.</p></section>;
  if (loading) return <div className="admin-panel flex min-h-52 items-center justify-center"><RefreshCw className="h-6 w-6 animate-spin text-[var(--admin-primary)]" /><span className="sr-only">جاري التحميل</span></div>;
  return <div className="space-y-6" dir="rtl">
    {error && <div role="alert" className="admin-panel flex items-center gap-3 bg-red-50 text-sm font-bold text-red-700"><AlertCircle className="h-5 w-5" />{error}<button onClick={() => void load()} className="mr-auto underline">إعادة المحاولة</button></div>}
    <section className="admin-panel overflow-hidden"><div className="flex flex-wrap items-center justify-between gap-5"><div><p className="text-xs font-black text-[var(--admin-muted)]">حالة اليوم</p><h2 className="mt-2 text-2xl font-black">{today?.state === 'Open' ? (openBreak ? `${openBreak.kind === 'ShortPermission' ? 'إذن قصير' : 'استراحة'} مفتوح` : 'أنت داخل الشفت الآن') : today ? 'اكتملت وردية اليوم' : 'لم تسجل الحضور بعد'}</h2>{today?.state === 'Open' && today.clockedInAt && <p className="mt-3 inline-flex items-center gap-2 rounded-full bg-[var(--admin-primary-10)] px-3 py-1.5 text-sm font-black text-[var(--admin-primary)]"><Clock3 className="h-4 w-4" />أنت في الشفت منذ {formatSessionDuration(todayElapsedMinutes, today.clockedInAt, today.clockedOutAt)}</p>}<p className="mt-2 flex items-center gap-2 text-sm font-bold text-[var(--admin-muted)]"><MapPin className="h-4 w-4" />يُطبق النظام سياسة الموقع أو الجهاز المخصصة لشفتك.</p>{today?.state === 'Open' && <p className="mt-3 flex items-center gap-2 text-sm font-bold text-[var(--admin-primary)]"><Timer className="h-4 w-4" />البريك: {used('Regular')} / {today.breakAllowanceMinutes ?? 0} د، الإذن: {used('ShortPermission')} / {today.dailyShortPermissionAllowanceMinutes ?? 0} د</p>}</div><div className="flex flex-wrap gap-2">{!today || today.state !== 'Open' ? <button disabled={acting || Boolean(today?.clockedOutAt)} onClick={() => void clockIn()} className="admin-btn-primary inline-flex min-h-12 items-center gap-2 px-6"><LogIn className="h-5 w-5" />تسجيل حضور</button> : <>{openBreak ? <button disabled={acting} onClick={() => void run(() => hrService.endAttendanceBreak(openBreak.id))} className="admin-btn-primary inline-flex min-h-12 items-center gap-2"><Coffee className="h-5 w-5" />إنهاء {openBreak.kind === 'ShortPermission' ? 'الإذن' : 'الاستراحة'}</button> : <><button disabled={acting} onClick={() => void run(() => hrService.startAttendanceBreak('Regular'))} className="admin-btn-secondary inline-flex min-h-12 items-center gap-2"><Coffee className="h-5 w-5" />بدء استراحة</button><button disabled={acting} onClick={() => void run(() => hrService.startAttendanceBreak('ShortPermission'))} className="admin-btn-secondary inline-flex min-h-12 items-center gap-2"><Timer className="h-5 w-5" />إذن قصير</button></>}<button disabled={acting || Boolean(openBreak)} onClick={() => void run(() => hrService.clockOutSecure())} className="admin-btn-primary inline-flex min-h-12 items-center gap-2"><LogOut className="h-5 w-5" />تسجيل انصراف</button></>}</div></div>{today && <div aria-label="تفاصيل وردية اليوم" className="mt-6 grid grid-cols-1 gap-px overflow-hidden rounded-xl border border-[var(--admin-border)] bg-[var(--admin-border)] sm:grid-cols-3"><div className="bg-[var(--admin-card-soft)] px-4 py-3"><p className="text-xs font-bold text-[var(--admin-muted)]">وقت الحضور</p><p className="mt-1 flex items-center gap-2 font-black text-[var(--admin-text)]"><LogIn className="h-4 w-4 text-[var(--admin-primary)]" />{formatCairoDateTime(today.clockedInAt, { hour: '2-digit', minute: '2-digit' })}</p></div><div className="bg-[var(--admin-card-soft)] px-4 py-3"><p className="text-xs font-bold text-[var(--admin-muted)]">وقت الانصراف</p><p className="mt-1 flex items-center gap-2 font-black text-[var(--admin-text)]"><LogOut className="h-4 w-4 text-[var(--admin-primary)]" />{today.clockedOutAt ? formatCairoDateTime(today.clockedOutAt, { hour: '2-digit', minute: '2-digit' }) : 'لم يتم التسجيل بعد'}</p></div><div className="bg-[var(--admin-card-soft)] px-4 py-3"><p className="text-xs font-bold text-[var(--admin-muted)]">{today.clockedOutAt ? 'إجمالي مدة الوردية' : 'المدة حتى الآن'}</p><p className="mt-1 flex items-center gap-2 font-black text-[var(--admin-text)]"><Clock3 className="h-4 w-4 text-[var(--admin-primary)]" />{formatSessionDuration(todayElapsedMinutes, today.clockedInAt, today.clockedOutAt)}</p></div></div>}</section>
    <section className="space-y-3"><div className="flex items-center justify-between"><h2 className="text-lg font-black">السجل الأخير</h2><button onClick={() => void load()} className="admin-btn-ghost inline-flex min-h-11 items-center gap-2"><RefreshCw className="h-4 w-4" />تحديث</button></div>{history.length === 0 ? <div className="admin-panel py-14 text-center text-sm font-bold text-[var(--admin-muted)]">لا توجد جلسات حضور حتى الآن.</div> : <div className="grid gap-3 md:grid-cols-2">{history.map((row) => <article key={row.id} className="admin-panel"><div className="flex justify-between gap-3"><p className="font-black">{row.workDate}</p><span className="admin-badge">{row.state}</span></div><div className="mt-4 grid grid-cols-3 gap-2 text-center"><div><p className="text-xs text-[var(--admin-muted)]">حضور</p><p className="font-black">{formatCairoDateTime(row.clockedInAt, { hour: '2-digit', minute: '2-digit' })}</p></div><div><p className="text-xs text-[var(--admin-muted)]">انصراف</p><p className="font-black">{row.clockedOutAt ? formatCairoDateTime(row.clockedOutAt, { hour: '2-digit', minute: '2-digit' }) : '—'}</p></div><div><p className="text-xs text-[var(--admin-muted)]">المدة</p><p className="font-black">{formatSessionDuration(row.workedMinutes, row.clockedInAt, row.clockedOutAt)}</p></div></div></article>)}</div>}</section>
    {history.length > 0 && <AttendanceCorrectionForm sessions={history} onSubmitted={load} />}
  </div>;
}
