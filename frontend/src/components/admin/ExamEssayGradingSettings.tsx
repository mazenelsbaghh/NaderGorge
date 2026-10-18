'use client';

import { AssessmentPendingResultsSwitch } from './AssessmentPendingResultsSwitch';
import { useId } from 'react';
import type { ExamEssayGradingMode } from '@/services/exam-service';
export type { ExamEssayGradingMode } from '@/services/exam-service';

const descriptions = {
  Automatic: 'يُصحَّح المقالي تلقائيًا، مع مراعاة إعداد التصحيح اليدوي الخاص بالمدرس.',
  Manual: 'تظل النتيجة قيد التصحيح حتى يراجع المدرس إجابات المقالي ويعتمد درجاتها.',
  ModelAnswerOnly: 'تظهر الإجابة النموذجية بعد التسليم دون تقييم إجابة الطالب. يُستبعد المقالي من الدرجة النهائية وتُعدَّل درجة النجاح بنفس النسبة.',
};

const creditChoices = [
  { mode: 'AutomaticFullCredit', label: 'درجة كاملة أو صفر', description: 'الكاملة للإجابة الصحيحة بالكامل، وصفر للناقصة أو الخاطئة.' },
  { mode: 'AutomaticFullOrHalfCredit', label: 'درجة كاملة أو نصفها أو صفر', description: 'الكاملة للصحيحة بالكامل، والنصف للصحيحة جزئيًا، وصفر للخاطئة.' },
  { mode: 'AutomaticFullOrHalfCreditFrom70Percent', label: 'درجة كاملة من ٧٠٪، أو نصفها، أو صفر', description: '٧٠٪ فأكثر تاخد الكاملة؛ أي جزء صحيح أقل منها ياخد النصف؛ والغلط أو الفاضي صفر. مثال: ٧ من ١٠ تصبح ١٠ من ١٠.' },
  { mode: 'Automatic', label: 'درجات جزئية', description: 'درجة حسب الأجزاء الصحيحة؛ مثل ربع أو ثلاثة أرباع الدرجة.' },
] as const;

export function ExamEssayGradingSettings({ mode, onChange, showPendingResults, onPendingResultsChange, disabled = false }: {
  mode: ExamEssayGradingMode; onChange: (mode: ExamEssayGradingMode) => void; disabled?: boolean;
  showPendingResults: boolean; onPendingResultsChange: (enabled: boolean) => void;
}) {
  const creditGroupId = useId();
  const executionMode = mode === 'Manual' || mode === 'ModelAnswerOnly' ? mode : 'Automatic';
  const automatic = executionMode === 'Automatic';
  return (
    <div className="space-y-4">
    <label className="block space-y-2 text-sm text-[var(--admin-text)]">
      <span className="font-bold">تصحيح الأسئلة المقالية</span>
      <select className="admin-input min-h-11 w-full" value={executionMode} disabled={disabled}
        onChange={event => onChange(event.target.value as ExamEssayGradingMode)}>
        <option value="Automatic">تصحيح تلقائي</option>
        <option value="Manual">تصحيح يدوي</option>
        <option value="ModelAnswerOnly">الإجابة النموذجية فقط، بدون تصحيح</option>
      </select>
      <span className="block leading-6 text-[var(--admin-muted)]">{descriptions[executionMode]}</span>
    </label>
    {automatic && <fieldset disabled={disabled} className="space-y-3">
      <legend className="text-sm font-bold text-[var(--admin-text)]">توزيع درجة المقالي</legend>
      <div className="grid gap-3 sm:grid-cols-2">
        {creditChoices.map(choice => <label key={choice.mode}
          className={`flex min-h-11 cursor-pointer items-start gap-3 rounded-lg border p-3 text-sm ${mode === choice.mode
            ? 'border-[var(--admin-primary)] bg-[var(--admin-card-soft)]' : 'border-[var(--admin-border)]'}`}>
          <input type="radio" name={creditGroupId} value={choice.mode} checked={mode === choice.mode}
            onChange={() => onChange(choice.mode)} className="mt-1 h-4 w-4 shrink-0 accent-[var(--admin-primary)]" />
          <span><span className="block font-bold text-[var(--admin-text)]">{choice.label}</span>
            <span className="mt-1 block leading-6 text-[var(--admin-muted)]">{choice.description}</span></span>
        </label>)}
      </div>
    </fieldset>}
    <AssessmentPendingResultsSwitch enabled={showPendingResults} onChange={onPendingResultsChange} disabled={disabled} />
    </div>
  );
}
