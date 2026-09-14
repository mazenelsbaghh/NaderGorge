import Link from 'next/link';
import { ChevronDown, Database } from 'lucide-react';
import {
  ADMIN_AI_ROUTE_BUILDERS,
  type AdminAiGroundedAnswer,
} from '@/services/admin-ai-agent-contract';
import { formatCairoTimestamp } from '@/lib/cairo-time';

export function AdminAiEvidenceDisclosure({
  answer,
}: {
  answer?: AdminAiGroundedAnswer | null;
}) {
  if (!answer) return null;
  const groups = [
    ['حقائق', answer.facts],
    ['حسابات', answer.calculations],
    ['استنتاجات', answer.inferences],
    ['حدود الإجابة', answer.limitations],
  ] as const;
  return (
    <div className="mt-3 space-y-2">
      {groups
        .filter(([, items]) => items.length)
        .map(([label, items]) => (
          <section
            key={label}
            className="border-r-2 border-[var(--admin-primary)] pr-3"
          >
            <h4 className="text-xs font-black text-[var(--admin-muted)]">
              {label}
            </h4>
            <ul className="mt-1 space-y-1 text-sm">
              {items.map((item, i) => (
                <li key={i}>{item}</li>
              ))}
            </ul>
          </section>
        ))}
      {answer.evidence.map((evidence, index) => (
        <details
          key={`${evidence.capabilityKey}-${index}`}
          className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3"
        >
          <summary className="flex cursor-pointer list-none items-center gap-2 text-xs font-black">
            <Database className="h-4 w-4" />
            المصدر والحدود <ChevronDown className="mr-auto h-4 w-4" />
          </summary>
          <dl className="mt-3 grid grid-cols-2 gap-2 text-xs">
            <dt className="text-[var(--admin-muted)]">السجلات</dt>
            <dd>{evidence.resultCount}</dd>
            <dt className="text-[var(--admin-muted)]">وقت البيانات</dt>
            <dd dir="ltr">
              {formatCairoTimestamp(evidence.dataAsOf)}
            </dd>
            <dt className="text-[var(--admin-muted)]">الحالة</dt>
            <dd>
              {evidence.isTruncated
                ? 'نتيجة مختصرة'
                : evidence.isComplete
                  ? 'كاملة'
                  : 'جزئية'}
            </dd>
          </dl>
          {evidence.drillDown.length > 0 && (
            <div className="mt-3 flex flex-wrap gap-2">
              {evidence.drillDown.map((link, i) => {
                const href = ADMIN_AI_ROUTE_BUILDERS[link.routeKey]?.(
                  link.routeParams
                );
                return href ? (
                  <Link
                    key={i}
                    href={href}
                    className="rounded-lg border border-[var(--admin-border)] px-3 py-2 text-xs font-bold text-[var(--admin-primary)]"
                  >
                    {link.labelAr}
                  </Link>
                ) : null;
              })}
            </div>
          )}
        </details>
      ))}
    </div>
  );
}
