'use client';

import { AssessmentPendingResultsSwitch } from './AssessmentPendingResultsSwitch';
import type { ExamEssayGradingMode } from '@/services/exam-service';
export type { ExamEssayGradingMode } from '@/services/exam-service';

const descriptions = {
  Automatic: 'يُصحَّح المقالي تلقائيًا، مع مراعاة إعداد التصحيح اليدوي الخاص بالمدرس.',
  Manual: 'تظل النتيجة قيد التصحيح حتى يراجع المدرس إجابات المقالي ويعتمد درجاتها.',
  ModelAnswerOnly: 'تظهر الإجابة النموذجية بعد التسليم دون تقييم إجابة الطالب. يُستبعد المقالي من الدرجة النهائية وتُعدَّل درجة النجاح بنفس النسبة.',
};

export function ExamEssayGradingSettings({ mode, onChange, showPendingResults, onPendingResultsChange, disabled = false }: {
  mode: ExamEssayGradingMode; onChange: (mode: ExamEssayGradingMode) => void; disabled?: boolean;
  showPendingResults: boolean; onPendingResultsChange: (enabled: boolean) => void;
}) {
  return (
    <div className="space-y-4">
    <label className="block space-y-2 text-sm text-[var(--admin-text)]">
      <span className="font-bold">تصحيح الأسئلة المقالية</span>
      <select className="admin-input min-h-11 w-full" value={mode} disabled={disabled}
        onChange={event => onChange(event.target.value as ExamEssayGradingMode)}>
        <option value="Automatic">تصحيح تلقائي</option>
        <option value="Manual">تصحيح يدوي</option>
        <option value="ModelAnswerOnly">الإجابة النموذجية فقط، بدون تصحيح</option>
      </select>
      <span className="block leading-6 text-[var(--admin-muted)]">{descriptions[mode]}</span>
    </label>
    <AssessmentPendingResultsSwitch enabled={showPendingResults} onChange={onPendingResultsChange} disabled={disabled} />
    </div>
  );
}
