'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Coffee, Pencil, RefreshCw, Timer, Users } from 'lucide-react';
import toast from 'react-hot-toast';
import { AdminPage } from '@/components/admin';
import { AdminBreakSessionDto, hrService } from '@/services/hr-service';
import { formatCairoDateTime, parseUtcDateTime } from '@/lib/cairo-time';

function elapsedMinutes(startedAt: string, serverNowUtc: string) { return Math.max(0, Math.floor((parseUtcDateTime(serverNowUtc).getTime() - parseUtcDateTime(startedAt).getTime()) / 60000)); }

function cairoDateTimeInput(value: string) {
  const dateParts = new Intl.DateTimeFormat('en-CA', { timeZone: 'Africa/Cairo', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' })
    .formatToParts(parseUtcDateTime(value)).reduce<Record<string, string>>((parts, part) => ({ ...parts, [part.type]: part.value }), {});
  return `${dateParts.year}-${dateParts.month}-${dateParts.day}T${dateParts.hour}:${dateParts.minute}`;
}

export default function HrBreaksPageClient() {
  const [rows, setRows] = useState<AdminBreakSessionDto[]>([]); const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<{ id: string; startedAt: string; endedAt: string } | null>(null);
  const [saving, setSaving] = useState(false);
  const load = useCallback(async () => { setLoading(true); try { setRows(await hrService.listAdminBreakSessions()); } finally { setLoading(false); } }, []);
  useEffect(() => { void load(); const timer = window.setInterval(() => void load(), 30000); return () => window.clearInterval(timer); }, [load]);
  const active = useMemo(() => rows.filter((row) => row.openBreak), [rows]);
  const saveBreak = async () => {
    if (!editing) return;
    setSaving(true);
    try {
      const response = await hrService.updateAttendanceBreak(editing.id, {
        startedAt: new Date(editing.startedAt).toISOString(),
        endedAt: editing.endedAt ? new Date(editing.endedAt).toISOString() : null,
      });
      if (!response.success) throw new Error(response.message);
      toast.success('تم تعديل وقت البريك وإعادة احتساب الدوام.');
      setEditing(null);
      await load();
    } catch (error) { toast.error(error instanceof Error ? error.message : 'تعذر تعديل وقت البريك.'); }
    finally { setSaving(false); }
  };
  return <AdminPage activePath="/admin/hr/breaks" sectionLabel="الموارد البشرية" pageTitle="متابعة البريك والإذن" subtitle="عرض مباشر للموظفين في البريك أو الإذن القصير، مع تحديث تلقائي كل 30 ثانية." action={<button onClick={() => void load()} className="admin-btn-primary inline-flex min-h-11 items-center gap-2"><RefreshCw className={loading ? 'h-4 w-4 animate-spin' : 'h-4 w-4'} />تحديث الآن</button>}>
    <div className="space-y-5" dir="rtl">
      <section className="grid gap-3 sm:grid-cols-2"><div className="admin-panel"><p className="text-sm font-bold text-[var(--admin-muted)]">حالات مفتوحة الآن</p><p className="mt-2 text-3xl font-black text-[var(--admin-primary)]">{active.length}</p></div><div className="admin-panel"><p className="text-sm font-bold text-[var(--admin-muted)]">جلسات اليوم المعروضة</p><p className="mt-2 text-3xl font-black">{rows.length}</p></div></section>
      {loading && rows.length === 0 ? <div className="admin-panel py-14 text-center"><RefreshCw className="mx-auto h-6 w-6 animate-spin text-[var(--admin-primary)]" /></div> : active.length === 0 ? <div className="admin-panel py-14 text-center"><Users className="mx-auto h-9 w-9 text-[var(--admin-muted)]" /><h2 className="mt-3 text-lg font-black">لا يوجد موظف في بريك أو إذن الآن</h2><p className="mt-1 text-sm font-bold text-[var(--admin-muted)]">تظهر هنا أي حالة مفتوحة فور تسجيلها.</p></div> : <section className="space-y-3"><h2 className="text-lg font-black">الحالات الحالية</h2><div className="grid gap-3 lg:grid-cols-2">{active.map((row) => { const item = row.openBreak!; const elapsed = elapsedMinutes(item.startedAt, row.serverNowUtc); const overdue = elapsed > item.allowedMinutes; return <article key={row.id} className="admin-panel"><div className="flex items-start justify-between gap-3"><div><p className="font-black">{row.employee}</p><p className="mt-1 text-sm font-bold text-[var(--admin-muted)]">{item.kind === 'ShortPermission' ? 'إذن قصير' : 'بريك'}</p></div><span className={overdue ? 'rounded-full bg-red-100 px-3 py-1 text-xs font-black text-red-700' : 'rounded-full bg-[var(--admin-primary-15)] px-3 py-1 text-xs font-black text-[var(--admin-primary)]'}>{overdue ? 'تجاوز الحد' : 'ضمن الحد'}</span></div><div className="mt-5 flex items-center gap-2 text-sm font-black"><Timer className="h-4 w-4 text-[var(--admin-primary)]" />{elapsed} من {item.allowedMinutes} دقيقة</div><p className="mt-2 text-xs font-bold text-[var(--admin-muted)]">بدأ في {formatCairoDateTime(item.startedAt, { hour: '2-digit', minute: '2-digit' })}</p><button onClick={() => setEditing({ id: item.id, startedAt: cairoDateTimeInput(item.startedAt), endedAt: '' })} className="admin-btn-secondary mt-4 inline-flex min-h-10 items-center gap-2"><Pencil className="h-4 w-4" />تعديل وقت البريك</button></article>; })}</div></section>}
      <section className="space-y-3"><h2 className="text-lg font-black">إعداد الموظف</h2><p className="admin-panel text-sm font-bold text-[var(--admin-muted)]"><Coffee className="ml-2 inline h-4 w-4 text-[var(--admin-primary)]" />لتعديل رصيد البريك أو الإذن القصير، افتح ملف الموظف من «الهيكل والموظفون» وعدّل الدقائق من قسم «البريك والإذن القصير».</p></section>
      {editing && <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/40 p-4"><section className="admin-panel w-full max-w-md"><h2 className="text-lg font-black">تعديل وقت البريك</h2><p className="mt-1 text-sm font-bold text-[var(--admin-muted)]">كل الأوقات بتوقيت القاهرة.</p><div className="mt-5 space-y-4"><label className="block text-sm font-black">بدأ البريك<input type="datetime-local" value={editing.startedAt} onChange={(event) => setEditing({ ...editing, startedAt: event.target.value })} className="admin-input mt-1 w-full" /></label><label className="block text-sm font-black">انتهى البريك (اختياري)<input type="datetime-local" value={editing.endedAt} onChange={(event) => setEditing({ ...editing, endedAt: event.target.value })} className="admin-input mt-1 w-full" /></label></div><div className="mt-6 flex justify-end gap-3"><button onClick={() => setEditing(null)} disabled={saving} className="admin-btn-secondary">إلغاء</button><button onClick={() => void saveBreak()} disabled={saving || !editing.startedAt} className="admin-btn-primary">{saving ? 'جارٍ الحفظ...' : 'حفظ التعديل'}</button></div></section></div>}
    </div>
  </AdminPage>;
}
