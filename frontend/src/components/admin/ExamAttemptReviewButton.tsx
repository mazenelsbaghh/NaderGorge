'use client';

import { useState } from 'react';
import { AdminModal } from '@/components/ui/admin-modal';
import NeumorphButton from '@/components/ui/neumorph-button';
import apiClient from '@/services/api-client';
import type { ExamResultDto } from '@/services/exam-service';

export function ExamAttemptReviewButton({ examId, attemptId }: { examId: string; attemptId: string }) {
  const [open, setOpen] = useState(false);
  const [review, setReview] = useState<ExamResultDto>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const load = async () => {
    setOpen(true); setBusy(true); setError('');
    try {
      const response = await apiClient.get<{ data: ExamResultDto }>(`/admin/exams/${examId}/attempts/${attemptId}/review`);
      setReview(response.data.data);
    } catch { setError('تعذر تحميل الإجابات. تأكد أن المحاولة تم تسليمها ثم أعد المحاولة.'); }
    finally { setBusy(false); }
  };
  return <>
    <NeumorphButton type="button" onClick={() => void load()}>عرض الإجابات</NeumorphButton>
    <AdminModal open={open} onClose={() => setOpen(false)} title="إجابات الطالب" size="lg">
      {busy ? <p role="status">جارٍ تحميل الإجابات…</p> : error ? <div role="alert"><p>{error}</p><button onClick={() => void load()}>إعادة المحاولة</button></div> : review ?
        <div className="space-y-4" dir="rtl">
          <p>الدرجة: {review.scoreAchieved} / {review.totalScore}</p>
          {review.questions.map(question => <section key={question.examQuestionId} className="rounded-xl border border-[var(--admin-border)] p-4 space-y-2">
            <h3 className="font-bold">{question.order}. {question.questionText}</h3>
            <p>إجابة الطالب: {question.isAnswered ? question.selectedOptionText || 'إجابة مرفقة' : 'لم يجب'}</p>
            {question.studentAudioUrl && <audio controls src={question.studentAudioUrl} aria-label="إجابة الطالب الصوتية" />}
            {question.correctOptionText && <p>الإجابة الصحيحة: {question.correctOptionText}</p>}
            {question.writtenCorrection && <p>{question.writtenCorrection}</p>}
            <p>الدرجة المحتسبة: {question.pointsAwarded}</p>
          </section>)}
        </div> : null}
    </AdminModal>
  </>;
}
