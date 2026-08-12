'use client';
import { AlertTriangle, Check, X } from 'lucide-react';
import type { AdminAiProposal } from '@/services/admin-ai-agent-contract';
import { AdminAiExecutionResult } from './AdminAiExecutionResult';
import { AdminAiStrongConfirmation } from './AdminAiStrongConfirmation';
import Link from 'next/link';
import { ADMIN_AI_ROUTE_BUILDERS } from '@/services/admin-ai-agent-contract';
export function AdminAiActionProposalCard({
  proposal,
  busy,
  onConfirm,
  onCancel,
  onSecureInput,
}: {
  proposal: AdminAiProposal;
  busy: boolean;
  onConfirm: (phrase?: string) => void;
  onCancel: () => void;
  onSecureInput: () => void;
}) {
  const pending = ['PendingSecureInput', 'PendingConfirmation'].includes(
    proposal.status
  );
  const targetHref = proposal.targetDrillDown
    ? ADMIN_AI_ROUTE_BUILDERS[proposal.targetDrillDown.routeKey](
        proposal.targetDrillDown.routeParams
      )
    : null;
  return (
    <article
      className="rounded-2xl border-2 border-[var(--admin-warning)] bg-[var(--admin-card)] p-4"
      aria-label={`مقترح: ${proposal.capabilityLabelAr}`}
    >
      <header className="flex items-start gap-3">
        <AlertTriangle className="mt-1 h-5 w-5 shrink-0 text-[var(--admin-warning)]" />
        <div>
          <p className="text-xs font-black text-[var(--admin-warning)]">
            مقترح يحتاج مراجعتك
          </p>
          <h3 className="font-black">{proposal.capabilityLabelAr}</h3>
          <p className="text-sm text-[var(--admin-muted)]">
            {proposal.targetLabelAr}
          </p>
          {targetHref && (
            <Link
              href={targetHref}
              className="text-xs font-bold text-[var(--admin-primary)]"
            >
              فتح السجل الأصلي
            </Link>
          )}
        </div>
        <span className="mr-auto rounded-full bg-[var(--admin-warning-10)] px-2 py-1 text-xs font-bold text-[var(--admin-warning)]">
          {proposal.primaryRisk}
        </span>
      </header>
      <p className="mt-3 text-sm leading-6">{proposal.effectSummaryAr}</p>
      {proposal.consequenceAr && (
        <p className="mt-2 text-sm font-bold text-[var(--admin-danger)]">
          {proposal.consequenceAr}
        </p>
      )}
      {proposal.changes.length > 0 && (
        <dl className="mt-3 space-y-2">
          {proposal.changes.map((c, i) => (
            <div
              key={i}
              className="grid grid-cols-2 gap-2 rounded-lg bg-[var(--admin-card-soft)] p-3 text-sm"
            >
              <dt className="col-span-2 font-black">{c.labelAr}</dt>
              <dd>
                <span className="block text-xs text-[var(--admin-muted)]">
                  الحالي
                </span>
                <span dir="auto">{String(c.currentValue ?? '—')}</span>
              </dd>
              <dd>
                <span className="block text-xs text-[var(--admin-muted)]">
                  الجديد
                </span>
                <span dir="auto">{String(c.requestedValue ?? '—')}</span>
              </dd>
            </div>
          ))}
        </dl>
      )}
      {proposal.bulk && (
        <section className="mt-3 rounded-xl border border-[var(--admin-border)] p-3 text-sm">
          <b>
            إجراء جماعي (
            {proposal.bulk.semantics === 'Atomic'
              ? 'الكل أو لا شيء'
              : 'نتائج جزئية'}
            )
          </b>
          <p>{proposal.bulk.selectionRuleAr}</p>
          <p>
            {proposal.bulk.candidateCount} مرشح، {proposal.bulk.excludedCount}{' '}
            مستبعد
          </p>
          <p className="text-[var(--admin-muted)]">
            {proposal.bulk.partialFailureBehaviorAr}
          </p>
          {proposal.bulk.representativeItems.length > 0 && (
            <ul
              className="mt-2 max-h-40 space-y-1 overflow-y-auto"
              tabIndex={0}
            >
              {proposal.bulk.representativeItems.map((reference) => (
                <li key={reference} className="break-words" dir="auto">
                  • {reference}
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
      {proposal.validationSummary.length > 0 && (
        <ul className="mt-3 space-y-1 text-xs text-[var(--admin-muted)]">
          {proposal.validationSummary.map((x, i) => (
            <li key={i}>• {x}</li>
          ))}
        </ul>
      )}
      {proposal.execution && (
        <AdminAiExecutionResult execution={proposal.execution} />
      )}
      {pending && proposal.requiresSecureInput && (
        <button
          data-admin-ai-secure-trigger
          onClick={onSecureInput}
          className="mt-4 min-h-11 w-full rounded-xl border border-[var(--admin-primary)] px-4 font-black text-[var(--admin-primary)]"
        >
          إدخال القيمة الآمنة
        </button>
      )}
      {pending &&
        proposal.confirmationType === 'TypedStrong' &&
        proposal.strongConfirmationPhrase && (
          <AdminAiStrongConfirmation
            phrase={proposal.strongConfirmationPhrase}
            expiresAt={proposal.expiresAt}
            busy={busy}
            onConfirm={onConfirm}
          />
        )}
      {pending &&
        proposal.confirmationType === 'Explicit' &&
        !proposal.requiresSecureInput && (
          <div className="mt-4 flex gap-2">
            <button
              disabled={busy}
              onClick={() => onConfirm()}
              className="min-h-11 flex-1 rounded-xl bg-[var(--admin-primary)] px-4 font-black text-[var(--admin-primary-contrast)]"
            >
              <Check className="inline h-4 w-4" /> تأكيد التنفيذ
            </button>
            <button
              disabled={busy}
              onClick={onCancel}
              className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 font-bold"
            >
              <X className="inline h-4 w-4" /> إلغاء
            </button>
          </div>
        )}
      {pending &&
        (proposal.confirmationType === 'TypedStrong' ||
          proposal.requiresSecureInput) && (
          <button
            disabled={busy}
            onClick={onCancel}
            className="mt-3 min-h-11 w-full rounded-xl border border-[var(--admin-border)] px-4 font-bold"
          >
            <X className="inline h-4 w-4" /> إلغاء المقترح دون تنفيذ
          </button>
        )}
      <time
        className="mt-3 block text-xs text-[var(--admin-muted)]"
        dateTime={proposal.expiresAt}
      >
        ينتهي: {new Date(proposal.expiresAt).toLocaleString('ar-EG')}
      </time>
      {!pending && !proposal.execution && (
        <p
          role="status"
          className="mt-2 text-sm font-bold text-[var(--admin-muted)]"
        >
          حالة المقترح: {proposal.status}
        </p>
      )}
    </article>
  );
}
