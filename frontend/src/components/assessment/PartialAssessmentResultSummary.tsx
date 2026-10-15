import Link from 'next/link';
import type { PartialAssessmentResultDto } from '@/services/assessment-result';

export function PartialAssessmentResultSummary({ partial, returnHref, returnLabel, error, canContinue }: {
  partial: PartialAssessmentResultDto; returnHref: string; returnLabel: string; error: string; canContinue: boolean;
}) {
  return <section aria-label="النتيجة الجزئية" aria-live="polite" className="space-y-4 rounded-xl border border-border bg-card p-6 text-start" dir="rtl">
    <h2 className="text-2xl font-bold text-foreground">درجتك في الأسئلة المصححة</h2>
    <p className="text-2xl font-bold tabular-nums text-foreground">{partial.totalScore > 0
      ? `${partial.scoreAchieved} من ${partial.totalScore}` : 'لا توجد درجات مصححة بعد'}</p>
    <p className="text-sm leading-7 text-muted-foreground">النتيجة غير نهائية. {partial.pendingQuestionCount} سؤال مقالي قيد التصحيح، ودرجاتها تُضاف بعد اعتماد التصحيح. الإجابات النموذجية ظاهرة في المراجعة.</p>
    {canContinue && <p className="text-sm leading-7 text-foreground">تقدر تكمل الحصة عادي أثناء انتظار التصحيح.</p>}
    {error && <p role="status" className="text-sm text-destructive">{error}</p>}
    <Link href={returnHref} className="inline-flex min-h-11 items-center rounded-xl bg-primary px-5 py-3 text-sm font-bold text-primary-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary">{returnLabel}</Link>
  </section>;
}
