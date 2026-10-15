'use client';

export function AssessmentPendingResultsSwitch({ enabled, onChange, disabled = false }: {
  enabled: boolean; onChange: (enabled: boolean) => void; disabled?: boolean;
}) {
  return <label className="flex min-h-11 items-start gap-3 text-sm text-[var(--admin-text)]">
    <input type="checkbox" role="switch" checked={enabled} disabled={disabled}
      onChange={event => onChange(event.target.checked)} className="mt-1 h-5 w-5 shrink-0 accent-[var(--admin-primary)]" />
    <span className="space-y-1">
      <span className="block font-bold">إظهار النتيجة أثناء تصحيح المقالي</span>
      <span className="block leading-6 text-[var(--admin-muted)]">تظهر درجات الأسئلة المصححة، والمقالي «قيد التصحيح» مع الإجابة النموذجية. الطالب يقدر يكمل الحصة، ودرجة المقالي تُضاف بعد التصحيح. يعمل مع التصحيح التلقائي أو اليدوي.</span>
    </span>
  </label>;
}
