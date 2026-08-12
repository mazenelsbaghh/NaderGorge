import { AlertTriangle, CheckCircle2, CircleX } from 'lucide-react';
import type { AdminAiExecution } from '@/services/admin-ai-agent-contract';
export function AdminAiExecutionResult({
  execution,
}: {
  execution: AdminAiExecution;
}) {
  const good = execution.status === 'Succeeded';
  const recovery = execution.status === 'RecoveryRequired';
  const Icon = good ? CheckCircle2 : recovery ? AlertTriangle : CircleX;
  return (
    <section
      aria-label="نتيجة التنفيذ"
      className="mt-3 rounded-xl border border-[var(--admin-border)] p-3"
    >
      <h4 className="flex items-center gap-2 font-black">
        <Icon
          className={`h-5 w-5 ${good ? 'text-[var(--admin-success)]' : 'text-[var(--admin-warning)]'}`}
        />
        {execution.safeSummaryAr}
      </h4>
      {recovery && (
        <p className="mt-2 text-sm text-[var(--admin-warning)]">
          النتيجة تحتاج مصالحة آمنة من المصدر الأصلي. لا تعِد التنفيذ يدويًا.
        </p>
      )}
      {execution.affectedCount !== null && (
        <dl className="mt-3 grid grid-cols-4 gap-2 text-center text-xs">
          <div>
            <dt>متأثر</dt>
            <dd className="font-black">{execution.affectedCount}</dd>
          </div>
          <div>
            <dt>ناجح</dt>
            <dd className="font-black">{execution.succeededCount ?? 0}</dd>
          </div>
          <div>
            <dt>متخطى</dt>
            <dd className="font-black">{execution.skippedCount ?? 0}</dd>
          </div>
          <div>
            <dt>فشل</dt>
            <dd className="font-black">{execution.failedCount ?? 0}</dd>
          </div>
        </dl>
      )}
      {execution.items.length > 0 && (
        <ul
          className="mt-3 max-h-56 space-y-2 overflow-auto"
          tabIndex={0}
          aria-label="نتائج العناصر"
        >
          {execution.items.map((item, i) => (
            <li
              key={i}
              className="border-t border-[var(--admin-border)] pt-2 text-xs"
            >
              <b dir="auto">{item.safeReference}</b> — {item.safeMessageAr}
            </li>
          ))}
        </ul>
      )}
      <p className="mt-2 text-[11px] text-[var(--admin-muted)]" dir="ltr">
        Trace: {execution.traceId}
      </p>
    </section>
  );
}
