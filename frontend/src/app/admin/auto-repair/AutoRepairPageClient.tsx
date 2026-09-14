'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { RefreshCw, ShieldCheck, Wrench } from 'lucide-react';
import SynchronizationStatus from './SynchronizationStatus';
import { AdminPage } from '@/components/admin';
import { decideRepair, getRepair, getRepairs, setRepairControl, type RepairDetail, type RepairOverview, type RepairStatus } from '@/services/auto-repair-service';

const labels: Record<RepairStatus, string> = {
  queued: 'في الانتظار', diagnosing: 'تشخيص', repairing: 'إصلاح', testing: 'اختبار', ready: 'جاهزة للنشر',
  deploying: 'نشر', monitoring: 'مراقبة بعد النشر', completed: 'مكتملة', awaiting_approval: 'تحتاج قرارك', failed: 'تعذّر الإصلاح', rolled_back: 'تم التراجع', duplicate: 'تكرار مجمّع', dismissed: 'مستبعدة',
};
const date = (timestamp: string) => new Date(timestamp).toLocaleString('ar-EG', { timeZone: 'Africa/Cairo' });

export default function AutoRepairPageClient() {
  const [overview, setOverview] = useState<RepairOverview | null>(null);
  const [selected, setSelected] = useState('');
  const [detail, setDetail] = useState<RepairDetail | null>(null);
  const [status, setStatus] = useState('active');
  const [dismissReason, setDismissReason] = useState('');
  const [page, setPage] = useState(1);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [confirmation, setConfirmation] = useState('');
  const [now, setNow] = useState(() => Date.now());
  const [updated, setUpdated] = useState<number | null>(null);
  const generation = useRef(0);
  const invalidate = useCallback(() => { generation.current++; }, []);
  const load = useCallback(async () => {
    const current = ++generation.current;
    try {
      const [next, nextDetail] = await Promise.all([getRepairs(status, page), selected ? getRepair(selected) : Promise.resolve(null)]);
      if (current !== generation.current) return;
      setOverview(next); setDetail(nextDetail); setError(''); setUpdated(Date.now());
    } catch { if (current === generation.current) setError('تعذر تحديث التقرير. البيانات المعروضة قديمة؛ حاول التحديث.'); }
  }, [page, selected, status]);
  useEffect(() => {
    void load();
    const timer = window.setInterval(() => { setNow(Date.now()); if (!document.hidden) void load(); }, 15_000);
    return () => { window.clearInterval(timer); invalidate(); };
  }, [load, invalidate]);
  const act = async (action: () => Promise<void>) => {
    setBusy(true);
    try { await action(); setConfirmation(''); setDismissReason(''); await load(); }
    catch { setError('تعذر تنفيذ الطلب. حدّث التقرير وتحقق من حالة الخدمة ثم حاول مجددًا.'); }
    finally { setBusy(false); }
  };
  const control = overview?.control;
  const online = !!control?.heartbeat && updated !== null && now - Date.parse(control.heartbeat) < 180_000;
  return <AdminPage activePath="/admin/auto-repair" sectionLabel="المراقبة الفنية" pageTitle="الإصلاح التلقائي"
    subtitle="من اكتشاف المشكلة إلى التحقق من الإصدار على السيرفرات الثلاثة"
    action={<button className="admin-btn-ghost flex items-center gap-2" onClick={() => void load()} disabled={busy}><RefreshCw size={16} />تحديث التقرير</button>}>
    <div className="space-y-5">
      {error && <p role="alert" className="rounded-xl border border-red-300 bg-red-50 p-4 text-red-900">{error}</p>}
      {!overview ? <p role="status" className="admin-panel p-6">جارٍ تحميل تقرير الإصلاح…</p> : <>
        <section aria-label="حالة الخدمة" className="admin-panel flex flex-wrap items-center justify-between gap-4 p-5">
          <div className="flex items-center gap-3"><Wrench className="text-[var(--admin-primary)]" /><div>
            <h2 className="font-bold">{online ? (control?.paused ? 'الرصد متصل، الإصلاح متوقف مؤقتًا' : 'المنفذ متصل') : 'المنفذ غير متصل'}</h2>
            <p className="mt-1 text-sm text-[var(--admin-text-muted)]">{control?.heartbeat ? `آخر اتصال: ${date(control.heartbeat)} · ${control.runner}` : 'لم يصل اتصال من خدمة السيرفر بعد. لن يبدأ إصلاح قبل تجهيز المنفذ.'}</p>
          </div></div>
          <div className="flex flex-wrap items-center gap-3">
            <button className="admin-btn-ghost" disabled={busy || !!error} onClick={() => void act(() => setRepairControl({ paused: !control!.paused, autoDeploy: control!.autoDeploy }))}>{control?.paused ? 'استئناف الإصلاح' : 'إيقاف الإصلاح مؤقتًا'}</button>
            <label className="flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" checked={control?.autoDeploy ?? false} disabled={busy || !!error}
              onChange={e => void act(() => setRepairControl({ paused: control!.paused, autoDeploy: e.target.checked }))} />النشر التلقائي بعد التحقق</label>
          </div>
          <p className="w-full text-sm text-[var(--admin-text-muted)]">الإيقاف يمنع بدء عمليات جديدة. أي نشر جارٍ يُستكمل حتى نقطة آمنة لتجنب ترك السيرفرات بإصدارات مختلفة.</p>
        </section>
        <SynchronizationStatus observation={overview.synchronization ?? null} lastReady={overview.lastSynchronized ?? null} now={now} reportUnavailable={!!error} />
        <div className="flex flex-wrap gap-x-6 gap-y-2 text-sm" aria-label="إجمالي الحالات">{overview.counts.map(count => <span key={count.status}>{labels[count.status] ?? count.status}: <strong>{count.count}</strong></span>)}</div>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <label className="flex items-center gap-2 text-sm">الحالة<select className="admin-input min-h-11" value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="active">قائمة العمل</option><option value="archive">السجل: مكتملة ومستبعدة ومكررة</option><option value="all">كل الحالات</option>{Object.entries(labels).map(([key, label]) => <option value={key} key={key}>{label}</option>)}</select></label>
          <p className="text-sm text-[var(--admin-text-muted)]">{updated && `آخر تحديث: ${date(new Date(updated).toISOString())}`}</p>
        </div>
        <p className="text-sm text-[var(--admin-text-muted)]">المكتملة والمستبعدة والتكرارات المجمّعة تخرج تلقائيًا من قائمة العمل وتبقى في السجل. «التكرار» عدد مرات ظهور الحالة؛ و«المحاولات» عدد مرات بدء تشخيصها.</p>
        <section className="admin-panel overflow-x-auto" aria-label="قائمة المشاكل">
          <table className="w-full min-w-[680px] text-right text-sm"><thead className="bg-[var(--admin-bg)]"><tr>{['المشكلة', 'الحالة', 'التكرار', 'المحاولات', 'آخر ظهور'].map(title => <th className="p-4" key={title}>{title}</th>)}</tr></thead>
            <tbody>{overview.incidents.map(incident => <tr key={incident.id} className="border-t border-[var(--admin-border)]">
              <td className="max-w-80 p-4"><button aria-expanded={selected === incident.id} className="text-right font-semibold text-[var(--admin-primary)] underline-offset-4 hover:underline" onClick={() => { setSelected(incident.id); setDetail(null); setConfirmation(''); setDismissReason(''); }}>{incident.category}</button><p className="mt-1 text-xs text-[var(--admin-text-muted)]">{incident.source} · {incident.level}</p></td>
              <td className="p-4">{labels[incident.status]}</td><td className="p-4">{incident.occurrences}</td><td className="p-4">{incident.attempts}</td><td className="p-4">{date(incident.lastSeen)}</td>
            </tr>)}</tbody></table>
          {!overview.incidents.length && <p className="p-8 text-center text-[var(--admin-text-muted)]">لا توجد مشاكل مطابقة. ستظهر الأخطاء والتحذيرات هنا عند رصدها بواسطة خدمة السيرفر.</p>}
        </section>
        <nav aria-label="صفحات التقرير" className="flex items-center gap-4"><button className="admin-btn-ghost" disabled={page === 1} onClick={() => setPage(page - 1)}>السابق</button><span>صفحة {page}</span><button className="admin-btn-ghost" disabled={page * 30 >= overview.total} onClick={() => setPage(page + 1)}>التالي</button></nav>
      </>}
      {selected && !detail && <p role="status">جارٍ تحميل تفاصيل المشكلة…</p>}
      {detail && <section className="admin-panel space-y-5 p-5" aria-label="تفاصيل الإصلاح">
        <div className="flex items-center justify-between"><h2 className="text-lg font-bold">تفاصيل الإصلاح · {labels[detail.status]}</h2><button className="admin-btn-ghost" onClick={() => { setSelected(''); setDetail(null); }}>إغلاق التفاصيل</button></div>
        <p className="whitespace-pre-wrap break-words">{detail.summary || 'لم ينتهِ التشخيص بعد.'}</p>
        {detail.releaseId && <p className="break-all text-sm">الإصدار: <bdi>{detail.releaseId}</bdi></p>}
        <details><summary className="cursor-pointer py-2 font-semibold">الدليل من اللوج</summary><pre dir="auto" className="max-h-80 overflow-auto whitespace-pre-wrap break-words rounded-lg bg-[var(--admin-bg)] p-4 text-xs">{detail.evidence}</pre></details>
        {(detail.status === 'awaiting_approval' || (detail.status === 'ready' && detail.approvedHash !== detail.proposalHash && !control?.autoDeploy)) && <div className="space-y-3 rounded-xl border border-[var(--admin-border)] p-4">
          <h3 className="flex items-center gap-2 font-semibold"><ShieldCheck size={18} />إجراء يحتاج اعتمادك</h3>
          <p className="text-sm">راجع ملخص التغيير ونتائج التحقق في السجل. الاعتماد يخص بصمة الإصلاح المعروضة فقط.</p>
          {detail.proposalHash ? <><label className="block text-sm">اكتب: اعتماد {detail.proposalHash.slice(0, 12)}<input className="admin-input mt-2 block w-full" value={confirmation} onChange={e => setConfirmation(e.target.value)} autoComplete="off" /></label>
            <button className="admin-btn-primary" disabled={busy || !!error || confirmation !== `اعتماد ${detail.proposalHash.slice(0, 12)}`} onClick={() => void act(() => decideRepair(detail.id, { action: 'approve', proposalHash: detail.proposalHash, confirmation }))}>اعتماد الإصلاح والنشر</button></> : <p>لا يوجد إصلاح قابل للنشر بعد. هذه الحالة تحتاج تدخلًا لتحديد الإجراء المناسب.</p>}
        </div>}
        {['failed', 'rolled_back', 'dismissed'].includes(detail.status) && <button className="admin-btn-ghost" disabled={busy || !!error} onClick={() => void act(() => decideRepair(detail.id, { action: 'retry' }))}>إعادة التشخيص والمحاولة</button>}
        {['queued', 'failed', 'rolled_back', 'awaiting_approval', 'ready'].includes(detail.status) && <div className="space-y-3 border-t border-[var(--admin-border)] pt-4">
          <label className="block text-sm">سبب الاستبعاد من قائمة العمل<textarea className="admin-input mt-2 block w-full" value={dismissReason} onChange={e => setDismissReason(e.target.value)} maxLength={1000} /></label>
          <p className="text-sm text-[var(--admin-text-muted)]">تنتقل الحالة إلى السجل ويتوقف تشخيص تكراراتها حتى تعيد فتحها. الحالة الجاري تنفيذها لا يمكن استبعادها.</p>
          <button className="admin-btn-ghost" disabled={busy || !!error || dismissReason.trim().length < 10} onClick={() => void act(() => decideRepair(detail.id, { action: 'dismiss', reason: dismissReason.trim() }))}>استبعاد ونقل للسجل</button>
        </div>}
        <h3 className="font-semibold">ما الذي تم؟</h3>
        <ol className="divide-y divide-[var(--admin-border)]">{detail.events.map(event => <li key={event.id} className="space-y-2 py-4"><div className="flex flex-wrap justify-between gap-2 text-sm"><strong>{labels[event.status] ?? event.status}</strong><time dateTime={event.timestamp}>{date(event.timestamp)}</time></div><p className="whitespace-pre-wrap break-words text-sm">{event.detail}</p></li>)}</ol>
      </section>}
    </div>
  </AdminPage>;
}
