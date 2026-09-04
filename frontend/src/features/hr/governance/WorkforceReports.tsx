'use client';

import Link from 'next/link';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertTriangle, CalendarClock, Download, ExternalLink, Filter, Loader2, MessageCircleMore, Search, Star, Timer, UserRoundCog } from 'lucide-react';
import toast from 'react-hot-toast';
import { hrGovernanceService, WorkforceRowDto } from '@/services/hr-governance-service';

const formatter = new Intl.NumberFormat('ar-EG');

function currentMonthRange() {
  const today = new Date();
  const localDate = new Date(today.getTime() - today.getTimezoneOffset() * 60_000);
  const to = localDate.toISOString().slice(0, 10);
  return { from: `${to.slice(0, 8)}01`, to, search: '' };
}

function percentage(numerator: number, denominator: number) {
  return denominator === 0 ? null : Math.round((numerator / denominator) * 1000) / 10;
}

function minutesLabel(minutes?: number | null) {
  if (minutes == null) return 'لا توجد ردود';
  if (minutes < 1) return `${formatter.format(Math.round(minutes * 60))} ث`;
  return `${formatter.format(Math.round(minutes * 10) / 10)} د`;
}

function MetricBar({ value, label }: { value: number | null; label: string }) {
  return <div className="min-w-32">
    <div className="flex items-center justify-between gap-3 text-xs font-bold">
      <span>{label}</span><span>{value == null ? '—' : `${formatter.format(value)}%`}</span>
    </div>
    <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-[var(--admin-bg)]">
      <div className="h-full rounded-full bg-[var(--admin-primary)]" style={{ width: `${Math.min(100, value ?? 0)}%` }} />
    </div>
  </div>;
}

function SupportEmployeeRow({ row }: { row: WorkforceRowDto }) {
  const closeRate = percentage(row.closedSupportConversations, row.supportConversations);
  const responseCoverage = percentage(row.respondedSupportConversations, row.supportConversations);
  const suspiciousAttendance = row.attendanceDays > 0 && row.lateMinutes / row.attendanceDays > 120;

  return <tr className="border-t border-[var(--admin-border)] align-top transition-colors hover:bg-[var(--admin-bg)]">
    <td className="sticky right-0 z-[1] bg-[var(--admin-surface)] p-4">
      <Link href={`/admin/hr/employees/${row.employeeId}`} className="inline-flex items-center gap-2 font-black text-[var(--admin-text)] hover:text-[var(--admin-primary)]">
        {row.fullName}<ExternalLink className="h-3.5 w-3.5" />
      </Link>
      <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">{row.employeeNumber} · {row.status}</p>
      <p className="mt-2 text-xs text-[var(--admin-muted)]">{row.organizationUnit ?? 'بلا وحدة تنظيمية'}</p>
    </td>
    <td className="p-4">
      <p className="font-black">{row.shiftName ?? 'لا يوجد شفت'}</p>
      <Link href={`/admin/hr/shifts?employeeId=${row.employeeId}`} className="mt-2 inline-flex min-h-9 items-center gap-1 text-xs font-black text-[var(--admin-primary)] hover:underline"><CalendarClock className="h-4 w-4" />ضبط الوردية</Link>
    </td>
    <td className="p-4">
      <p className="font-black">{formatter.format(row.completedAttendanceDays)} / {formatter.format(row.attendanceDays)} يوم مكتمل</p>
      <p className="mt-1 text-xs text-[var(--admin-muted)]">عمل فعلي: {formatter.format(Math.round((row.workedMinutes / 60) * 10) / 10)} س</p>
      <p className={`mt-2 text-xs font-bold ${suspiciousAttendance ? 'text-amber-700' : 'text-[var(--admin-muted)]'}`}>تأخير {formatter.format(row.lateMinutes)} د · انصراف مبكر {formatter.format(row.earlyLeaveMinutes)} د</p>
      {suspiciousAttendance && <span className="mt-2 inline-flex items-center gap-1 rounded-full bg-amber-100 px-2 py-1 text-[11px] font-black text-amber-800"><AlertTriangle className="h-3.5 w-3.5" />راجع إعداد الشفت</span>}
    </td>
    <td className="p-4">
      <p className="font-black">{formatter.format(row.closedSupportConversations)} مغلقة</p>
      <p className="mt-1 text-xs text-[var(--admin-muted)]">من {formatter.format(row.supportConversations)} محادثة مشاركة</p>
      <div className="mt-3"><MetricBar value={closeRate} label="نسبة الإغلاق" /></div>
    </td>
    <td className="p-4">
      <p className="font-black">{minutesLabel(row.averageFirstResponseMinutes)}</p>
      <p className="mt-1 text-xs text-[var(--admin-muted)]">متوسط أول رد بعد الاستلام</p>
      <div className="mt-3"><MetricBar value={responseCoverage} label="تغطية الردود" /></div>
    </td>
    <td className="p-4">
      <p className="inline-flex items-center gap-1 font-black text-amber-700"><Star className="h-4 w-4 fill-current" />{row.averageStudentRating?.toFixed(2) ?? '—'}</p>
      <p className="mt-1 text-xs text-[var(--admin-muted)]">{formatter.format(row.ratingCount)} تقييم</p>
    </td>
  </tr>;
}

export function WorkforceReports() {
  const [rows, setRows] = useState<WorkforceRowDto[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [filters, setFilters] = useState(currentMonthRange);

  const load = useCallback(async (silent = false) => {
    if (!silent) setLoading(true);
    try {
      const page = await hrGovernanceService.workforce(filters);
      setRows(page.items);
      setTotal(page.total);
    } catch {
      if (!silent) toast.error('غير مصرح أو تعذر تحميل تقرير الأداء');
    } finally {
      if (!silent) setLoading(false);
    }
  }, [filters]);

  useEffect(() => {
    const initialLoad = window.setTimeout(() => void load(), 250);
    const refresh = window.setInterval(() => { if (document.visibilityState === 'visible') void load(true); }, 30_000);
    return () => { window.clearTimeout(initialLoad); window.clearInterval(refresh); };
  }, [load]);

  const summary = useMemo(() => ({
    conversations: rows.reduce((sum, row) => sum + row.supportConversations, 0),
    closed: rows.reduce((sum, row) => sum + row.closedSupportConversations, 0),
    ratings: rows.reduce((sum, row) => sum + row.ratingCount, 0),
  }), [rows]);

  async function exportCsv() {
    try {
      const blob = await hrGovernanceService.exportWorkforce({ ...filters, reason: 'تصدير تقرير أداء فريق الدعم' });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `support-performance-${filters.from}-${filters.to}.csv`;
      anchor.click();
      URL.revokeObjectURL(url);
      toast.success('تم تسجيل وتصدير التقرير');
    } catch {
      toast.error('تعذر تصدير التقرير');
    }
  }

  return <div className="space-y-5">
    <section className="admin-panel">
      <div className="flex flex-wrap items-start justify-between gap-5">
        <div><h2 className="text-lg font-black text-[var(--admin-text)]">لوحة قياس فريق الدعم</h2><p className="mt-1 max-w-2xl text-sm text-[var(--admin-muted)]">أرقام تشغيلية للمراجعة الإدارية. تقييم أسلوب المحادثة يحتاج مراجعة بشرية ولا ينتج النظام ترتيبًا وظيفيًا تلقائيًا.</p></div>
        <Link href="/admin/hr/shifts" className="admin-btn-secondary inline-flex min-h-11 items-center gap-2"><UserRoundCog className="h-4 w-4" />إدارة الورديات</Link>
      </div>
      <div className="mt-5 grid gap-3 lg:grid-cols-[1.4fr_1fr_1fr_auto]">
        <label className="relative"><Search className="absolute right-3 top-3.5 h-4 w-4 text-[var(--admin-muted)]" /><input aria-label="بحث في موظفي الدعم" value={filters.search} onChange={(event) => setFilters({ ...filters, search: event.target.value })} placeholder="اسم الموظف أو رقمه" className="admin-input w-full pr-10" /></label>
        <input aria-label="من تاريخ" type="date" value={filters.from} onChange={(event) => setFilters({ ...filters, from: event.target.value })} className="admin-input w-full" />
        <input aria-label="إلى تاريخ" type="date" value={filters.to} onChange={(event) => setFilters({ ...filters, to: event.target.value })} className="admin-input w-full" />
        <button type="button" onClick={() => void load()} className="admin-btn-primary inline-flex min-h-11 items-center justify-center gap-2"><Filter className="h-4 w-4" />تطبيق</button>
      </div>
    </section>

    <section className="admin-panel flex flex-wrap items-center gap-x-8 gap-y-4" aria-label="ملخص الفترة">
      <div className="flex items-center gap-3"><MessageCircleMore className="h-5 w-5 text-[var(--admin-primary)]" /><div><p className="text-xs font-bold text-[var(--admin-muted)]">المحادثات</p><p className="font-black">{formatter.format(summary.conversations)}</p></div></div>
      <div className="flex items-center gap-3"><Timer className="h-5 w-5 text-[var(--admin-primary)]" /><div><p className="text-xs font-bold text-[var(--admin-muted)]">المغلقة</p><p className="font-black">{formatter.format(summary.closed)}</p></div></div>
      <div className="flex items-center gap-3"><Star className="h-5 w-5 text-amber-700" /><div><p className="text-xs font-bold text-[var(--admin-muted)]">التقييمات</p><p className="font-black">{formatter.format(summary.ratings)}</p></div></div>
      <div className="mr-auto flex items-center gap-3"><span className="admin-badge">{formatter.format(total)} موظف داخل صلاحيتك</span><button type="button" onClick={() => void exportCsv()} className="admin-btn-secondary inline-flex min-h-10 items-center gap-2"><Download className="h-4 w-4" />تصدير CSV</button></div>
    </section>

    <section className="admin-panel overflow-hidden p-0">
      {loading ? <div className="flex min-h-72 items-center justify-center"><Loader2 className="h-7 w-7 animate-spin text-[var(--admin-primary)]" /><span className="mr-3 font-bold">جارٍ حساب المؤشرات…</span></div> : rows.length === 0 ? <div className="py-20 text-center"><MessageCircleMore className="mx-auto h-9 w-9 text-[var(--admin-muted)]" /><p className="mt-4 font-black">لا توجد بيانات في هذه الفترة</p><p className="mt-1 text-sm text-[var(--admin-muted)]">غيّر الفترة أو ابحث باسم مختلف.</p></div> : <div className="overflow-x-auto"><table className="w-full min-w-[1250px] text-sm"><thead className="bg-[var(--admin-bg)] text-right text-xs font-black text-[var(--admin-muted)]"><tr><th className="sticky right-0 z-[2] bg-[var(--admin-bg)] p-4">حساب الموظف</th><th className="p-4">الوردية</th><th className="p-4">الحضور والانصراف</th><th className="p-4">إغلاق الشات</th><th className="p-4">سرعة الرد</th><th className="p-4">تقييم الطلاب</th></tr></thead><tbody>{rows.map((row) => <SupportEmployeeRow key={row.employeeId} row={row} />)}</tbody></table></div>}
    </section>

    <section className="grid gap-3 text-xs text-[var(--admin-muted)] md:grid-cols-3">
      <p><b className="text-[var(--admin-text)]">سرعة الرد:</b> أول رسالة من الموظف بعد استلام المحادثة، خلال فترة إسنادها إليه.</p>
      <p><b className="text-[var(--admin-text)]">نسبة الإغلاق:</b> المحادثات التي أنهاها الموظف مقارنة بكل المحادثات التي شارك فيها.</p>
      <p><b className="text-[var(--admin-text)]">تنبيه الشفت:</b> يظهر عند تجاوز متوسط التأخير ساعتين يوميًا، وغالبًا يعني أن الجدول يحتاج مراجعة.</p>
    </section>
  </div>;
}
