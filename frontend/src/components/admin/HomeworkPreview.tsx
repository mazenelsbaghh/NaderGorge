'use client';

import { useState } from 'react';
import { createPortal } from 'react-dom';
import { AccessibleDialog } from '@/components/shared/AccessibleDialog';
import { QuestionImage } from '@/components/assessment/QuestionImage';
import { normalizeQuestionRichText } from '@/lib/question-text';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import type { HomeworkDashboardDto } from '@/services/admin-service';

export function HomeworkPreview({ homework, onClose }: { homework: HomeworkDashboardDto; onClose: () => void }) {
  const [showCorrections, setShowCorrections] = useState(false);
  return createPortal(
    <AccessibleDialog open onClose={onClose} title={`معاينة الواجب: ${homework.title}`} subtitle="معاينة فقط، لا تُنشئ محاولة أو تسليمًا للطلاب." className="w-full max-w-3xl overflow-y-auto rounded-2xl bg-[var(--admin-card)] p-4 text-[var(--admin-text)] sm:p-6">
      <div dir="rtl" className="min-w-0 space-y-5 p-4 sm:p-6">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <label className="flex min-h-11 items-center gap-2 text-sm font-bold">
            <input type="checkbox" checked={showCorrections} onChange={event => setShowCorrections(event.target.checked)} /> إظهار الإجابات النموذجية
          </label>
          <button type="button" onClick={onClose} className="admin-btn-ghost min-h-11">إغلاق المعاينة</button>
        </div>
        {homework.description && <p className="whitespace-pre-wrap break-words text-sm">{homework.description}</p>}
        {homework.questions.length === 0 && <p>لم تتم إضافة أسئلة لهذا الواجب بعد.</p>}
        {homework.questions.map((question, index) => (
          <section key={question.homeworkQuestionId} className="min-w-0 space-y-3 border-t border-[var(--admin-border)] pt-5 [overflow-wrap:anywhere]" aria-label={`السؤال ${index + 1}`}>
            <h3 className="text-base font-bold">السؤال {index + 1} · {question.points} نقاط</h3>
            <div dangerouslySetInnerHTML={{ __html: normalizeQuestionRichText(question.text) }} />
            <QuestionImage imageUrl={question.imageUrl} alt={`صورة السؤال ${index + 1}`} />
            {question.audioUrl && <audio controls preload="none" src={resolveMediaUrl(question.audioUrl)} className="w-full" />}
            {question.baseText && <p className="whitespace-pre-wrap">{question.baseText}</p>}
            {question.possibleAnswers?.map((answer, answerIndex) => (
              <div key={answerIndex} className="rounded-lg border border-[var(--admin-border)] p-3">
                <div dangerouslySetInnerHTML={{ __html: normalizeQuestionRichText(answer) }} />
                {showCorrections && answer === question.correctAnswerKey && <span className="font-bold text-[var(--admin-success)]">الإجابة الصحيحة</span>}
              </div>
            ))}
            {question.type === 'Essay' && <p className="text-sm text-[var(--admin-muted)]">مساحة إجابة مقالية للطالب</p>}
            {showCorrections && question.writtenCorrection && <div dangerouslySetInnerHTML={{ __html: normalizeQuestionRichText(question.writtenCorrection) }} />}
          </section>
        ))}
      </div>
    </AccessibleDialog>, document.body
  );
}
