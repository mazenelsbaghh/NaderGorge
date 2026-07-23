'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { isAxiosError } from 'axios';
import { AlertCircle, Coffee, LogIn, LogOut, MapPin, RefreshCw } from 'lucide-react';
import toast from 'react-hot-toast';
import { AttendanceSessionDto, hrService } from '@/services/hr-service';
import { createClientId } from '@/lib/client-id';
import { AttendanceCorrectionForm } from './AttendanceCorrectionForm';

const errorMessages: Record<string, string> = {
  OUTSIDE_GEOFENCE: 'أنت خارج نطاق موقع العمل المسموح.', LOCATION_ACCURACY_LOW: 'دقة الموقع غير كافية؛ فعّل GPS وحاول مجددًا.',
  DEVICE_NOT_TRUSTED: 'هذا الجهاز غير مسجل كجهاز موثوق.', NO_SCHEDULE: 'لا يوجد شفت منشور لك اليوم.',
  SESSION_ALREADY_OPEN: 'لديك جلسة حضور مفتوحة بالفعل.', NO_OPEN_SESSION: 'لا توجد جلسة حضور مفتوحة.', BREAK_ALREADY_OPEN: 'لديك استراحة مفتوحة بالفعل.',
};

export function AttendanceWorkspace() {
  const [today, setToday] = useState<AttendanceSessionDto | null>(null);
  const [history, setHistory] = useState<AttendanceSessionDto[]>([]);
  const [loading, setLoading] = useState(true); const [acting, setActing] = useState(false); const [error, setError] = useState<string | null>(null);
  const load = useCallback(async () => { setLoading(true); setError(null); try { const [current, rows] = await Promise.all([hrService.getAttendanceToday(), hrService.getAttendanceHistory()]); setToday(current); setHistory(rows); } catch { setError('تعذر تحميل سجل الحضور.'); } finally { setLoading(false); } }, []);
  useEffect(() => { void load(); }, [load]);
  const openBreak = today?.breaks?.find((item) => !item.endedAt);
  const deviceToken = useMemo(() => { if (typeof window === 'undefined') return ''; const key = 'hr-attendance-device-token'; const existing = localStorage.getItem(key); if (existing) return existing; const next = createClientId(); localStorage.setItem(key, next); return next; }, []);
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

  if (loading) return <div className="admin-panel flex min-h-52 items-center justify-center"><RefreshCw className="h-6 w-6 animate-spin text-[var(--admin-primary)]" /><span className="sr-only">جاري التحميل</span></div>;
  return <div className="space-y-6" dir="rtl">
    {error && <div role="alert" className="admin-panel flex items-center gap-3 bg-red-50 text-sm font-bold text-red-700"><AlertCircle className="h-5 w-5" />{error}<button onClick={() => void load()} className="mr-auto underline">إعادة المحاولة</button></div>}
    <section className="admin-panel overflow-hidden"><div className="flex flex-wrap items-center justify-between gap-5"><div><p className="text-xs font-black text-[var(--admin-muted)]">حالة اليوم</p><h2 className="mt-2 text-2xl font-black">{today?.state === 'Open' ? 'أنت داخل الشفت الآن' : today ? 'اكتملت وردية اليوم' : 'لم تسجل الحضور بعد'}</h2><p className="mt-2 flex items-center gap-2 text-sm font-bold text-[var(--admin-muted)]"><MapPin className="h-4 w-4" />يُطبق النظام سياسة الموقع أو الجهاز المخصصة لشفتك.</p></div><div className="flex flex-wrap gap-2">{!today || today.state !== 'Open' ? <button disabled={acting || Boolean(today?.clockedOutAt)} onClick={() => void clockIn()} className="admin-btn-primary inline-flex min-h-12 items-center gap-2 px-6"><LogIn className="h-5 w-5" />تسجيل حضور</button> : <>{openBreak ? <button disabled={acting} onClick={() => void run(() => hrService.endAttendanceBreak(openBreak.id))} className="admin-btn-secondary inline-flex min-h-12 items-center gap-2"><Coffee className="h-5 w-5" />إنهاء الاستراحة</button> : <button disabled={acting} onClick={() => void run(() => hrService.startAttendanceBreak())} className="admin-btn-secondary inline-flex min-h-12 items-center gap-2"><Coffee className="h-5 w-5" />بدء استراحة</button>}<button disabled={acting || Boolean(openBreak)} onClick={() => void run(() => hrService.clockOutSecure())} className="admin-btn-primary inline-flex min-h-12 items-center gap-2"><LogOut className="h-5 w-5" />تسجيل انصراف</button></>}</div></div></section>
    <section className="space-y-3"><div className="flex items-center justify-between"><h2 className="text-lg font-black">السجل الأخير</h2><button onClick={() => void load()} className="admin-btn-ghost inline-flex min-h-11 items-center gap-2"><RefreshCw className="h-4 w-4" />تحديث</button></div>{history.length === 0 ? <div className="admin-panel py-14 text-center text-sm font-bold text-[var(--admin-muted)]">لا توجد جلسات حضور حتى الآن.</div> : <div className="grid gap-3 md:grid-cols-2">{history.map((row) => <article key={row.id} className="admin-panel"><div className="flex justify-between gap-3"><p className="font-black">{row.workDate}</p><span className="admin-badge">{row.state}</span></div><div className="mt-4 grid grid-cols-3 gap-2 text-center"><div><p className="text-xs text-[var(--admin-muted)]">حضور</p><p className="font-black">{new Date(row.clockedInAt).toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit' })}</p></div><div><p className="text-xs text-[var(--admin-muted)]">انصراف</p><p className="font-black">{row.clockedOutAt ? new Date(row.clockedOutAt).toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit' }) : '—'}</p></div><div><p className="text-xs text-[var(--admin-muted)]">المدة</p><p className="font-black">{Math.floor(row.workedMinutes / 60)}س {row.workedMinutes % 60}د</p></div></div></article>)}</div>}</section>
    {history.length > 0 && <AttendanceCorrectionForm sessions={history} onSubmitted={load} />}
  </div>;
}
