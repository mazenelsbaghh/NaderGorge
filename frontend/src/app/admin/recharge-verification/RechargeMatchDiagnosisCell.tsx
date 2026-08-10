import {
  AlertTriangle,
  CircleCheck,
  CircleHelp,
  MessagesSquare,
  ShieldAlert,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import type {
  AdminRechargeMatchCandidateDto,
  AdminRechargeRequestDto,
} from '@/services/wallet-service';
import {
  describeRechargeMatchDiagnosis,
  type RechargeMatchDiagnosisTone,
} from '@/lib/recharge-match-diagnosis';

const toneClasses: Record<RechargeMatchDiagnosisTone, string> = {
  teal: 'border-teal-600/30 bg-teal-600/10 text-teal-950 dark:text-teal-100',
  amber: 'border-amber-500/35 bg-amber-500/10 text-amber-950 dark:text-amber-100',
  rose: 'border-rose-500/30 bg-rose-500/10 text-rose-950 dark:text-rose-100',
  neutral: 'border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-[var(--admin-text)]',
};

const toneIcons: Record<RechargeMatchDiagnosisTone, LucideIcon> = {
  teal: CircleCheck,
  amber: AlertTriangle,
  rose: ShieldAlert,
  neutral: CircleHelp,
};

const factClass = (matches: boolean) => matches
  ? 'bg-teal-600/12 text-teal-800 dark:text-teal-200'
  : 'bg-amber-500/15 text-amber-800 dark:text-amber-200';

function CandidateDetails({ candidate }: { candidate: AdminRechargeMatchCandidateDto }) {
  return (
    <div className="mt-2 border-t border-current/15 pt-2">
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs font-bold">
        <span className="inline-flex items-center gap-1">
          <MessagesSquare className="h-3.5 w-3.5" aria-hidden="true" />
          <bdi dir="ltr" className="font-mono">{candidate.senderPhoneNumber}</bdi>
        </span>
        <span className="font-mono">
          {candidate.amount == null ? 'مبلغ غير معروف' : `${candidate.amount.toLocaleString('ar-EG-u-nu-latn')} ج.م`}
        </span>
      </div>
      <div className="mt-2 flex flex-wrap gap-1.5 text-[11px] font-black">
        <span className={`rounded-full px-2 py-1 ${factClass(candidate.phoneMatches)}`}>
          {candidate.phoneMatches ? 'الرقم مطابق' : 'الرقم مختلف'}
        </span>
        {candidate.hasSingleDigitMismatchPattern ? (
          <span className="rounded-full bg-amber-500/15 px-2 py-1 text-amber-800 dark:text-amber-200">
            خطأ محتمل في رقم واحد
          </span>
        ) : null}
        <span className={`rounded-full px-2 py-1 ${factClass(candidate.amountMatches)}`}>
          {candidate.amountMatches ? 'المبلغ مطابق' : 'المبلغ مختلف'}
        </span>
        <span className={`rounded-full px-2 py-1 ${factClass(candidate.withinWindow)}`}>
          {candidate.withinWindow ? 'داخل فترة المطابقة' : 'خارج فترة المطابقة'}
        </span>
        {!candidate.sameWallet ? (
          <span className="rounded-full bg-[var(--admin-card)] px-2 py-1 text-[var(--admin-muted)]">
            محفظة استقبال أخرى
          </span>
        ) : null}
      </div>
      {candidate.walletLabel ? (
        <span className="mt-2 block text-[11px] font-bold opacity-75">وصلت إلى: {candidate.walletLabel}</span>
      ) : null}
    </div>
  );
}

export function RechargeMatchDiagnosisCell({ request }: { request: AdminRechargeRequestDto }) {
  const presentation = describeRechargeMatchDiagnosis(request);
  if (!presentation) {
    return <span className="block min-w-56 text-center text-sm font-bold text-[var(--admin-muted)]">تمت المعالجة</span>;
  }

  const Icon = toneIcons[presentation.tone];
  const candidate = request.matchDiagnosis?.candidate;

  return (
    <div
      className={`min-w-64 max-w-72 rounded-xl border p-3 text-right ${toneClasses[presentation.tone]}`}
      aria-label={`${presentation.title}. ${presentation.detail}`}
    >
      <div className="flex items-start gap-2">
        <Icon className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
        <div className="min-w-0">
          <strong className="block text-sm font-black leading-5">{presentation.title}</strong>
          <p className="mt-1 text-xs font-semibold leading-5">{presentation.detail}</p>
        </div>
      </div>

      {candidate ? <CandidateDetails candidate={candidate} /> : null}
    </div>
  );
}
