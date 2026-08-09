'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Activity, Clock3, Coffee, RefreshCw, Search, TimerOff, UsersRound } from 'lucide-react';
import toast from 'react-hot-toast';
import { AdminColumn, AdminDataTable, AdminPage, AdminStatCard } from '@/components/admin';
import { AdminBreakSessionDto, AdminDailyAttendanceReportDto, hrService } from '@/services/hr-service';
import { formatCairoDateTime, parseUtcDateTime } from '@/lib/cairo-time';

const cairoToday = () => new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Africa/Cairo', year: 'numeric', month: '2-digit', day: '2-digit',
}).format(new Date());

export default function HrAdminPageClient() {
  const today = useMemo(cairoToday, []);
  const [sessions, setSessions] = useState<AdminBreakSessionDto[]>([]);
  const [dailyReport, setDailyReport] = useState<AdminDailyAttendanceReportDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [from, setFrom] = useState(today);
  const [to, setTo] = useState(today);
  const [now, setNow] = useState(() => Date.now());
  const [serverClockOffsetMs, setServerClockOffsetMs] = useState(0);
  const [lastUpdatedAt, setLastUpdatedAt] = useState<Date | null>(null);

  const load = useCallback(async (silent = false) => {
    if (!silent) setLoading(true);
    try {
      const [sessionRows, dailyRows] = await Promise.all([
        hrService.listAdminBreakSessions(from || undefined, to || undefined),
        hrService.getDailyAttendanceReport(from || undefined, to || undefined),
      ]);
      setSessions(sessionRows);
      setDailyReport(dailyRows);
      setLastUpdatedAt(new Date());
      const receivedAt = Date.now();
      const serverNowUtc = sessionRows[0]?.serverNowUtc ?? dailyRows[0]?.serverNowUtc;
      if (serverNowUtc) setServerClockOffsetMs(receivedAt - parseUtcDateTime(serverNowUtc).getTime());
      setNow(receivedAt);
    } catch {
      if (!silent) toast.error('تعذر تحميل الحضور اللحظي');
    } finally {
      if (!silent) setLoading(false);
    }
  }, [from, to]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    const timer = window.setInterval(() => {
      if (document.visibilityState === 'visible') void load(true);
    }, 15_000);
    return () => window.clearInterval(timer);
  }, [load]);
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 30_000);
    return () => window.clearInterval(timer);
  }, []);

  const visibleSessions = useMemo(() => {
    const query = search.trim().toLocaleLowerCase('ar');
    if (!query) return sessions;
    return sessions.filter((item) => item.employee.toLocaleLowerCase('ar').includes(query) || item.employeePhone.includes(query));
  }, [search, sessions]);
  const active = sessions.filter((item) => !item.clockedOutAt);
  const onBreak = active.filter((item) => item.openBreak);
  const late = sessions.filter((item) => item.lateMinutes > 0);
  const visibleDailyReport = useMemo(() => {
    const query = search.trim().toLocaleLowerCase('ar');
    if (!query) return dailyReport;
    return dailyReport.filter((item) => item.employee.toLocaleLowerCase('ar').includes(query) || item.employeePhone.includes(query));
  }, [dailyReport, search]);

  const elapsedTodayMinutes = (clockedInAt: string) =>
    Math.max(0, Math.floor((now - serverClockOffsetMs - parseUtcDateTime(clockedInAt).getTime()) / 60_000));
  const durationMinutes = (row: AdminBreakSessionDto) => row.clockedOutAt
    ? row.workedMinutes
    : elapsedTodayMinutes(row.clockedInAt);
  const dailyWorkedMinutes = (row: AdminDailyAttendanceReportDto) => row.workedMinutes
    + (row.openClockedInAt ? elapsedTodayMinutes(row.openClockedInAt) : 0);
  const formatDuration = (minutes: number) => `${Math.floor(minutes / 60)} س ${Math.max(0, minutes % 60)} د`;
  const formatTime = (value: string) => formatCairoDateTime(value, { hour: '2-digit', minute: '2-digit' });

  const columns: AdminColumn<AdminBreakSessionDto>[] = [
    { key: 'employee', label: 'الموظف', render: (row) => <div><p className="font-black">{row.employee}</p><p className="mt-0.5 text-xs text-[var(--admin-muted)]" dir="ltr">{row.employeePhone}</p></div> },
    { key: 'workDate', label: 'اليوم', render: (row) => <span className="font-bold" dir="ltr">{row.workDate}</span> },
    { key: 'clockedInAt', label: 'حضور', render: (row) => <span className="font-bold">{formatTime(row.clockedInAt)}</span> },
    { key: 'clockedOutAt', label: 'الحالة الآن', render: (row) => row.clockedOutAt
      ? <span className="inline-flex rounded-full bg-[var(--admin-card-soft)] px-3 py-1 text-xs font-bold text-[var(--admin-muted)]">انصرف {formatTime(row.clockedOutAt)}</span>
      : row.openBreak
        ? <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-3 py-1 text-xs font-black text-amber-800"><Coffee className="h-3.5 w-3.5" />في استراحة</span>
        : <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 px-3 py-1 text-xs font-black text-emerald-800"><Activity className="h-3.5 w-3.5" />يعمل الآن</span> },
    { key: 'workedMinutes', label: 'المدة المباشرة', render: (row) => <span className="font-black">{formatDuration(durationMinutes(row))}</span> },
    { key: 'lateMinutes', label: 'التأخير', render: (row) => row.lateMinutes > 0 ? <span className="font-black text-rose-700">{row.lateMinutes} د</span> : '—' },
    { key: 'overtimeMinutes', label: 'الإضافي', render: (row) => row.overtimeMinutes > 0 ? <span className="font-black text-emerald-700">{row.overtimeMinutes} د</span> : '—' },
  ];
  const dailyColumns: AdminColumn<AdminDailyAttendanceReportDto>[] = [
    { key: 'employee', label: 'الموظف', render: (row) => <div><p className="font-black">{row.employee}</p><p className="mt-0.5 text-xs text-[var(--admin-muted)]" dir="ltr">{row.employeePhone}</p></div> },
    { key: 'workDate', label: 'اليوم', render: (row) => <span className="font-bold" dir="ltr">{row.workDate}</span> },
    { key: 'clockedInAt', label: 'الحضور', render: (row) => <span className="font-black">{formatTime(row.clockedInAt)}</span> },
    { key: 'clockedOutAt', label: 'الانصراف', render: (row) => row.hasOpenSession ? <span className="inline-flex rounded-full bg-emerald-100 px-3 py-1 text-xs font-black text-emerald-800">ما زال يعمل</span> : row.clockedOutAt ? <span className="font-black">{formatTime(row.clockedOutAt)}</span> : '—' },
    { key: 'workedMinutes', label: 'صافي مدة العمل', render: (row) => <span className="font-black text-[var(--admin-primary)]">{formatDuration(dailyWorkedMinutes(row))}</span> },
    { key: 'lateMinutes', label: 'التأخير', render: (row) => row.lateMinutes > 0 ? <span className="font-black text-rose-700">{row.lateMinutes} د</span> : '—' },
    { key: 'earlyLeaveMinutes', label: 'خروج مبكر', render: (row) => row.earlyLeaveMinutes > 0 ? <span className="font-black text-amber-700">{row.earlyLeaveMinutes} د</span> : '—' },
    { key: 'overtimeMinutes', label: 'إضافي', render: (row) => row.overtimeMinutes > 0 ? <span className="font-black text-emerald-700">{row.overtimeMinutes} د</span> : '—' },
  ];

  return <AdminPage activePath="/admin/hr" sectionLabel="الموارد البشرية" pageTitle="الحضور المباشر" subtitle="متابعة لحظية لمن يعمل الآن، الاستراحات، التأخير والانصراف. تتحدث البيانات تلقائياً كل 15 ثانية." action={<button type="button" onClick={() => void load()} disabled={loading} className="admin-btn-primary inline-flex min-h-11 items-center gap-2"><RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />تحديث الآن</button>}>
    <div className="space-y-5">
      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <AdminStatCard icon={UsersRound} label="يعملون الآن" value={active.length} variant="accent" subtitle="جلسات مفتوحة" />
        <AdminStatCard icon={Coffee} label="في استراحة" value={onBreak.length} variant="light" subtitle="ضمن الحاضرين" />
        <AdminStatCard icon={TimerOff} label="متأخرون" value={late.length} variant="light" subtitle="داخل الفترة المحددة" />
        <AdminStatCard icon={Clock3} label="إجمالي السجلات" value={sessions.length} variant="light" subtitle={lastUpdatedAt ? `آخر تحديث ${lastUpdatedAt.toLocaleTimeString('ar-EG-u-nu-latn', { timeZone: 'Africa/Cairo', hour: '2-digit', minute: '2-digit' })}` : 'جارٍ التحديث'} />
      </section>
      <section className="admin-panel flex flex-wrap items-end gap-3">
        <label className="min-w-56 flex-1 text-sm font-bold">بحث<div className="admin-input mt-1 flex items-center gap-2"><Search className="h-4 w-4 text-[var(--admin-muted)]" /><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="اسم الموظف أو الهاتف" className="w-full bg-transparent outline-none" /></div></label>
        <label className="text-sm font-bold">من<input type="date" value={from} onChange={(event) => setFrom(event.target.value)} className="admin-input mt-1" /></label>
        <label className="text-sm font-bold">إلى<input type="date" value={to} onChange={(event) => setTo(event.target.value)} className="admin-input mt-1" /></label>
        <span className="inline-flex min-h-11 items-center rounded-full bg-emerald-100 px-4 text-xs font-black text-emerald-800"><span className="ml-2 h-2 w-2 rounded-full bg-emerald-600" />تحديث تلقائي مباشر</span>
      </section>
      <section className="space-y-3"><div><h2 className="text-lg font-black">تقرير دوام الموظفين اليومي</h2><p className="mt-1 text-sm font-bold text-[var(--admin-muted)]">وقت الحضور والانصراف وصافي مدة العمل لكل موظف في كل يوم.</p></div><AdminDataTable data={visibleDailyReport} columns={dailyColumns} loading={loading} rowKey={(row) => `${row.employeeId}-${row.workDate}`} emptyMessage="لا توجد بيانات دوام في الفترة المحددة." /></section>
      <section className="space-y-3"><div><h2 className="text-lg font-black">المتابعة اللحظية والجلسات</h2><p className="mt-1 text-sm font-bold text-[var(--admin-muted)]">تتحدث تلقائيًا لمتابعة الموظفين الموجودين حاليًا داخل الشفت أو الاستراحة.</p></div><AdminDataTable data={visibleSessions} columns={columns} loading={loading} rowKey={(row) => row.id} emptyMessage="لا توجد جلسات حضور في الفترة المحددة." /></section>
    </div>
  </AdminPage>;
}
