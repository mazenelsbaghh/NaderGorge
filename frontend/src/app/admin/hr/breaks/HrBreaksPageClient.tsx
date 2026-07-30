'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Coffee, RefreshCw, Timer, Users } from 'lucide-react';
import { AdminPage } from '@/components/admin';
import { AdminBreakSessionDto, hrService } from '@/services/hr-service';
import { formatCairoDateTime } from '@/lib/cairo-time';

function elapsedMinutes(startedAt: string) { return Math.max(0, Math.floor((Date.now() - new Date(startedAt).getTime()) / 60000)); }

export default function HrBreaksPageClient() {
  const [rows, setRows] = useState<AdminBreakSessionDto[]>([]); const [loading, setLoading] = useState(true);
  const load = useCallback(async () => { setLoading(true); try { setRows(await hrService.listAdminBreakSessions()); } finally { setLoading(false); } }, []);
  useEffect(() => { void load(); const timer = window.setInterval(() => void load(), 30000); return () => window.clearInterval(timer); }, [load]);
  const active = useMemo(() => rows.filter((row) => row.openBreak), [rows]);
  return <AdminPage activePath="/admin/hr/breaks" sectionLabel="الموارد البشرية" pageTitle="متابعة البريك والإذن" subtitle="عرض مباشر للموظفين في البريك أو الإذن القصير، مع تحديث تلقائي كل 30 ثانية." action={<button onClick={() => void load()} className="admin-btn-primary inline-flex min-h-11 items-center gap-2"><RefreshCw className={loading ? 'h-4 w-4 animate-spin' : 'h-4 w-4'} />تحديث الآن</button>}>
    <div className="space-y-5" dir="rtl">
      <section className="grid gap-3 sm:grid-cols-2"><div className="admin-panel"><p className="text-sm font-bold text-[var(--admin-muted)]">حالات مفتوحة الآن</p><p className="mt-2 text-3xl font-black text-[var(--admin-primary)]">{active.length}</p></div><div className="admin-panel"><p className="text-sm font-bold text-[var(--admin-muted)]">جلسات اليوم المعروضة</p><p className="mt-2 text-3xl font-black">{rows.length}</p></div></section>
      {loading && rows.length === 0 ? <div className="admin-panel py-14 text-center"><RefreshCw className="mx-auto h-6 w-6 animate-spin text-[var(--admin-primary)]" /></div> : active.length === 0 ? <div className="admin-panel py-14 text-center"><Users className="mx-auto h-9 w-9 text-[var(--admin-muted)]" /><h2 className="mt-3 text-lg font-black">لا يوجد موظف في بريك أو إذن الآن</h2><p className="mt-1 text-sm font-bold text-[var(--admin-muted)]">تظهر هنا أي حالة مفتوحة فور تسجيلها.</p></div> : <section className="space-y-3"><h2 className="text-lg font-black">الحالات الحالية</h2><div className="grid gap-3 lg:grid-cols-2">{active.map((row) => { const item = row.openBreak!; const elapsed = elapsedMinutes(item.startedAt); const overdue = elapsed > item.allowedMinutes; return <article key={row.id} className="admin-panel"><div className="flex items-start justify-between gap-3"><div><p className="font-black">{row.employee}</p><p className="mt-1 text-sm font-bold text-[var(--admin-muted)]">{item.kind === 'ShortPermission' ? 'إذن قصير' : 'بريك'}</p></div><span className={overdue ? 'rounded-full bg-red-100 px-3 py-1 text-xs font-black text-red-700' : 'rounded-full bg-[var(--admin-primary-15)] px-3 py-1 text-xs font-black text-[var(--admin-primary)]'}>{overdue ? 'تجاوز الحد' : 'ضمن الحد'}</span></div><div className="mt-5 flex items-center gap-2 text-sm font-black"><Timer className="h-4 w-4 text-[var(--admin-primary)]" />{elapsed} من {item.allowedMinutes} دقيقة</div><p className="mt-2 text-xs font-bold text-[var(--admin-muted)]">بدأ في {formatCairoDateTime(item.startedAt, { hour: '2-digit', minute: '2-digit' })}</p></article>; })}</div></section>}
      <section className="space-y-3"><h2 className="text-lg font-black">إعداد الموظف</h2><p className="admin-panel text-sm font-bold text-[var(--admin-muted)]"><Coffee className="ml-2 inline h-4 w-4 text-[var(--admin-primary)]" />لتعديل رصيد البريك أو الإذن القصير، افتح ملف الموظف من «الهيكل والموظفون» وعدّل الدقائق من قسم «البريك والإذن القصير».</p></section>
    </div>
  </AdminPage>;
}
