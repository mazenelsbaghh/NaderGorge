'use client';

import { useRef, useState, type FormEvent } from 'react';
import { AdminModal } from '@/components/ui/admin-modal';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import NeumorphButton from '@/components/ui/neumorph-button';
import apiClient from '@/services/api-client';
import { normalizeQuestionRichText } from '@/lib/question-text';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import { getApiErrorSummary } from '@/lib/api-errors';
import toast from 'react-hot-toast';

interface AnswerReview {
  questionId: string; order: number; text: string; imageUrl?: string;
  answer?: string; audioUrl?: string; correction?: string; maximum: number; score: number | null;
}
interface AttemptReview {
  attemptId: string; studentName: string; title: string; score: number; total: number;
  status: string; canGrade: boolean; feedback?: string; questions: AnswerReview[];
}

export function AssessmentAttemptReview({ kind, assessmentId, attemptId, onChanged }: {
  kind: 'homework' | 'exam'; assessmentId: string; attemptId: string; onChanged?: () => void | Promise<void>;
}) {
  const [open, setOpen] = useState(false);
  const [review, setReview] = useState<AttemptReview>();
  const [scores, setScores] = useState<Record<string, string>>({});
  const [feedback, setFeedback] = useState('');
  const [busy, setBusy] = useState(false);
  const mutationRef = useRef(false);
  const [error, setError] = useState('');
  const [confirmDelete, setConfirmDelete] = useState(false);
  const load = async () => {
    setOpen(true); setBusy(true); setError(''); setReview(undefined);
    try {
      const response = kind === 'homework'
        ? await apiClient.get<{ success: boolean; data: AttemptReview; message?: string }>(`/admin/homework/${assessmentId}/submissions/${attemptId}/review`)
        : await apiClient.get<{ success: boolean; data: AttemptReview; message?: string }>(`/admin/exams/${assessmentId}/attempts/${attemptId}/assessment-review`);
      if (!response.data.success || !response.data.data) throw new Error(response.data.message || 'المحاولة غير متاحة');
      const attempt = response.data.data;
      setReview(attempt);
      setScores(Object.fromEntries(attempt.questions.map(q => [q.questionId, q.score == null ? '' : String(q.score)])));
      setFeedback(attempt.feedback || '');
    } catch (failure) { setError(getApiErrorSummary(failure, 'تعذر تحميل إجابات الطالب.')); }
    finally { setBusy(false); }
  };
  const save = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!review || mutationRef.current) return;
    mutationRef.current = true; setBusy(true); setError('');
    try {
      const payload = { scores: review.questions.map(q => ({ questionId: q.questionId, score: Number(scores[q.questionId]) })), feedback };
      if (kind === 'homework') await apiClient.put(`/admin/homework/${assessmentId}/submissions/${attemptId}/grade`, payload);
      else await apiClient.put(`/admin/exams/${assessmentId}/attempts/${attemptId}/grade`, payload);
      toast.success('تم حفظ التصحيح وتحديث النتيجة.');
      await load();
      await onChanged?.();
    } catch (failure) { setError(getApiErrorSummary(failure, 'تعذر حفظ التصحيح. درجاتك المدخلة ما زالت محفوظة هنا.')); }
    finally { mutationRef.current = false; setBusy(false); }
  };
  const remove = async () => {
    if (mutationRef.current) return;
    mutationRef.current = true; setBusy(true); setConfirmDelete(false);
    try {
      if (kind === 'homework') await apiClient.delete(`/admin/homework/${assessmentId}/submissions/${attemptId}`);
      else await apiClient.delete(`/admin/exams/${assessmentId}/attempts/${attemptId}`);
      setOpen(false); setReview(undefined);
      toast.success('تم حذف المحاولة وإتاحة الحل من جديد.');
      await onChanged?.();
    } catch (failure) { setOpen(true); setError(getApiErrorSummary(failure, 'تعذر حذف المحاولة.')); }
    finally { mutationRef.current = false; setBusy(false); }
  };
  return <>
    <NeumorphButton type="button" disabled={busy} onClick={() => void load()}>الإجابات والتصحيح</NeumorphButton>
    <AdminModal open={open} onClose={() => { if (!busy) setOpen(false); }} title={review ? `إجابات ${review.studentName}` : 'إجابات الطالب'} size="lg">
      {error && <p role="alert" className="mb-4 text-red-700">{error}</p>}
      {busy && <p role="status">جارٍ تنفيذ الطلب…</p>}
      {!review && !busy && <NeumorphButton onClick={() => void load()}>إعادة المحاولة</NeumorphButton>}
      {review && <form onSubmit={save} className="space-y-4" dir="rtl">
        <p className="font-bold">{review.title} — الدرجة: {review.score} / {review.total}</p>
        <p className="text-sm text-[var(--admin-muted)]">{review.canGrade ? 'يمكنك تصحيح كل الأسئلة يدويًا، بما فيها المقالي، بدون انتظار الذكاء الاصطناعي.' : 'التصحيح يتطلب تسليم المحاولة وتوفّر صلاحية التصحيح لحسابك.'}</p>
        {review.questions.map((q, index) => <section key={q.questionId} className="space-y-3 rounded-xl border border-[var(--admin-border)] p-4">
          <h3 className="font-bold">السؤال {index + 1}</h3>
          <div dir="auto" className="break-words" dangerouslySetInnerHTML={{ __html: normalizeQuestionRichText(q.text) }} />
          {q.imageUrl && <div>
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src={resolveMediaUrl(q.imageUrl)} alt={`صورة السؤال ${index + 1}`} className="max-h-60 max-w-full object-contain" />
          </div>}
          <div className="rounded-lg bg-[var(--admin-card-soft)] p-3"><p className="mb-1 text-sm font-bold">إجابة الطالب</p><p dir="auto" className="whitespace-pre-wrap break-words">{q.answer || (q.audioUrl ? 'إجابة صوتية' : 'لم يجب')}</p>
            {q.audioUrl && <audio controls preload="none" className="mt-2 w-full" src={resolveMediaUrl(q.audioUrl)} aria-label={`إجابة السؤال ${index + 1} الصوتية`} />}
          </div>
          {q.correction && <div><p className="text-sm font-bold">الإجابة المرجعية</p><div dir="auto" dangerouslySetInnerHTML={{ __html: normalizeQuestionRichText(q.correction) }} /></div>}
          <label className="flex flex-wrap items-center gap-3 text-sm font-bold">درجة السؤال (من {q.maximum})
            <input aria-label={`درجة السؤال ${index + 1}`} type="number" required min={0} max={q.maximum} step={kind === 'homework' ? 1 : '0.01'} disabled={!review.canGrade || busy} value={scores[q.questionId] ?? ''} onChange={e => setScores(previous => ({ ...previous, [q.questionId]: e.target.value }))} className="min-h-11 w-28 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-3" />
            {q.score == null && <span className="text-[var(--admin-muted)]">لم تُصحّح بعد</span>}
          </label>
        </section>)}
        <label className="block text-sm font-bold">ملاحظات التصحيح<textarea maxLength={4000} value={feedback} disabled={busy || !review.canGrade} onChange={e => setFeedback(e.target.value)} rows={3} className="mt-2 w-full rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-3" /></label>
        <div className="flex flex-wrap justify-between gap-3">
          <NeumorphButton type="submit" disabled={busy || !review.canGrade}>حفظ التصحيح اليدوي</NeumorphButton>
          <NeumorphButton type="button" intent="danger" disabled={busy} onClick={() => { setOpen(false); setConfirmDelete(true); }}>حذف المحاولة وإتاحة الحل من جديد</NeumorphButton>
        </div>
      </form>}
    </AdminModal>
    <ConfirmDialog open={confirmDelete} title="حذف محاولة الطالب؟" description={`سيتم حذف إجابات ${review?.studentName || 'الطالب'} ودرجات هذه المحاولة فقط، وإتاحة الحل من جديد. لا يمكن التراجع عن الحذف من هنا.`} confirmLabel="حذف المحاولة" onConfirm={() => void remove()} onCancel={() => { setConfirmDelete(false); setOpen(true); }} />
  </>;
}
