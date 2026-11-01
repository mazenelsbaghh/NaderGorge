'use client';

export function AssessmentVisibilitySettings({ scoreVisible, onScoreChange, answersVisible, onAnswersChange, disabled = false }: {
  scoreVisible: boolean; onScoreChange: (visible: boolean) => void;
  answersVisible: boolean; onAnswersChange: (visible: boolean) => void; disabled?: boolean;
}) {
  return <div className="space-y-4">
    <label className="flex min-h-11 items-start gap-3 text-sm text-[var(--admin-text)]">
      <input type="checkbox" role="switch" checked={scoreVisible} disabled={disabled}
        onChange={event => onScoreChange(event.target.checked)} className="mt-1 h-5 w-5 shrink-0 accent-[var(--admin-primary)]" />
      <span><span className="block font-bold">إظهار النتيجة</span>
        <span className="mt-1 block leading-6 text-[var(--admin-muted)]">إظهار إجمالي الدرجة «كام من كام». عند الإغلاق تظل درجات الأسئلة والإجابات والتصحيح وتعليقات المصحّح ظاهرة حسب إعداد إظهار الإجابات.</span></span>
    </label>
    <label className="flex min-h-11 items-start gap-3 text-sm text-[var(--admin-text)]">
      <input type="checkbox" role="switch" checked={answersVisible} disabled={disabled}
        onChange={event => onAnswersChange(event.target.checked)} className="mt-1 h-5 w-5 shrink-0 accent-[var(--admin-primary)]" />
      <span><span className="block font-bold">إظهار الإجابات والتصحيح</span>
        <span className="mt-1 block leading-6 text-[var(--admin-muted)]">السماح للطالب بمراجعة إجاباته ودرجات الأسئلة والإجابات النموذجية وتعليقات المصحّح بعد التسليم.</span></span>
    </label>
  </div>;
}
