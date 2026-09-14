import type { RepairSynchronization } from '@/services/auto-repair-service';

const states: Record<RepairSynchronization['snapshot']['state'], { title: string; next: string }> = {
  ready: { title: 'المصدر متزامن مع السيرفرات', next: 'فحص المصدر ناجح. يبدأ الإصلاح حسب إعدادات التشغيل والموافقة.' },
  release_failed: { title: 'النشر فشل ويحتاج مراجعة', next: 'راجع سبب الفشل أو التراجع، ثم انشر إصلاحًا أو تراجعًا موثقًا للمصدر عبر مسار النشر المعتاد. الإصلاح التلقائي لن يبدأ قبل تطابق السيرفرات.' },
  pending_release: { title: 'بانتظار تأكيد الإصدار المنشور', next: 'المصدر المشترك مختلف عن إصدار السيرفرات. استكمل النشر الجاري؛ وإذا فشل أو تم التراجع عنه، راجع الإصدار قبل استئناف الإصلاح.' },
  dependencies_changed: { title: 'بيئة الإصلاح تحتاج تحديثًا', next: 'تغيّرت ملفات الاعتماد أو أدوات البناء. جهّز بيئة إصلاح مطابقة وتحقق منها أولًا.' },
  unavailable: { title: 'تعذّر التحقق من المزامنة', next: 'تحقق من اتصال GitHub والسيرفرات وصلاحيات المنفذ. لن يبدأ إصلاح جديد قبل نجاح الفحص.' },
  storage_low: { title: 'المساحة لا تكفي لبدء الإصلاح', next: 'وفّر ٢٠ جيجابايت على الأقل في مساحة عمل المنفذ ثم أعد الفحص.' },
};
const timestamp = (value: string) => new Date(value).toLocaleString('ar-EG', { timeZone: 'Africa/Cairo' });

export default function SynchronizationStatus({ observation, lastReady, now, reportUnavailable }: {
  observation: RepairSynchronization | null;
  lastReady: RepairSynchronization | null;
  now: number;
  reportUnavailable: boolean;
}) {
  const fresh = observation && !reportUnavailable && now - Date.parse(observation.checkedAt) < 180_000;
  const state = fresh ? states[observation.snapshot.state] : null;
  return <section aria-label="مزامنة المصدر" className="admin-panel space-y-3 p-5">
    <h2 className="font-bold" aria-live="polite">{state?.title ?? 'حالة المزامنة غير مؤكدة'}</h2>
    <p className="max-w-3xl text-sm text-[var(--admin-text-muted)]">{state?.next ?? 'لم يصل فحص حديث. البيانات السابقة لا تكفي لبدء إصلاح جديد؛ تحقق من اتصال المنفذ ثم حدّث التقرير.'}</p>
    <div className="flex flex-wrap gap-x-6 gap-y-2 text-sm text-[var(--admin-text-muted)]">
      <span>{observation ? `آخر فحص مستلم: ${timestamp(observation.checkedAt)}` : 'لم يصل فحص للمصدر بعد.'}</span>
      {lastReady && <span>آخر تطابق مؤكد: {timestamp(lastReady.checkedAt)}</span>}
    </div>
    {observation && <details className="text-sm">
      <summary className="min-h-11 cursor-pointer content-center font-medium">تفاصيل الإصدارات وقت الفحص</summary>
      <dl className="grid gap-3 pt-2 sm:grid-cols-2">
        <div><dt className="text-[var(--admin-text-muted)]">المصدر المشترك</dt><dd className="break-all" dir="ltr">{observation.snapshot.sharedCommit || '—'}</dd></div>
        {observation.snapshot.nodes.map(node => <div key={node.nodeId}><dt>{node.nodeId}</dt><dd className="break-all" dir="ltr">{node.releaseId}</dd></div>)}
      </dl>
    </details>}
  </section>;
}
