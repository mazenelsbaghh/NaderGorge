'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { MessageCircle, RefreshCw, Send } from 'lucide-react';
import toast from 'react-hot-toast';
import { adminService, type ExamParentMessageState, type ExamParentMessageSummary } from '@/services/admin-service';
import { getApiErrorSummary } from '@/lib/api-errors';
import { createClientId } from '@/lib/client-id';

export function examParentMessageFailure(code: string | null): string {
  const normalized = code?.replace('WHATSAPP_CLOUD_', '');
  switch (normalized) {
    case '131042': return 'تعذر التحقق من أهلية حساب واتساب أو الدفع. راجع حساب Meta إذا استمر الفشل.';
    case '131026': return 'تعذر تسليم الرسالة لرقم ولي الأمر.';
    case '132005': return 'الاسم أو نص المتغير أطول من الحد المسموح.';
    case '132018': return 'متغيرات الرسالة لا تطابق القالب المعتمد.';
    case 'PARENT_PHONE_NOT_FOUND': return 'لا يوجد رقم واتساب صالح لولي الأمر.';
    case 'RESULT_TEMPLATE_CHANGED': return 'راجع قالب رسالة النتيجة في إعدادات الامتحان.';
    case 'RESULT_TEMPLATE_PARAMETERS_INVALID': return 'راجع قيم متغيرات القالب ورقم متابعة الطالب.';
    case 'RECIPIENT_OR_TEMPLATE_CHANGED': return 'تغيّر رقم المستلم أو القالب، أو تم إيقاف استقبال الرسائل.';
    case 'INTERRUPTED_DELIVERY': return 'حالة الإرسال غير مؤكدة وتحتاج مراجعة قبل تكرار الرسالة.';
    default: return code ? `تعذر إرسال الرسالة (${code})` : 'تعذر إرسال الرسالة.';
  }
}

const statusLabels: Record<string, string> = {
  NotSent: 'لم تُرسل', NotReady: 'النتيجة لم تجهز', Pending: 'في انتظار الإرسال', Sending: 'جاري الإرسال',
  Sent: 'قبلها واتساب، ننتظر التسليم', Delivered: 'وصلت', Read: 'تمت قراءتها', Failed: 'فشل الإرسال',
  Skipped: 'تم إيقاف الإرسال', Uncertain: 'الإرسال غير مؤكد',
};

export function ExamParentMessageStatus({ state }: { state?: ExamParentMessageState }) {
  if (!state) return <span className="text-sm text-[var(--admin-muted)]">جارٍ تحميل الحالة…</span>;
  return <div className="max-w-64 space-y-1 text-sm">
    <p className={`font-bold ${state.status === 'Failed' ? 'text-[var(--admin-danger)]' : 'text-[var(--admin-text)]'}`}>
      {statusLabels[state.status] ?? state.status}
    </p>
    {state.failureCode && <p className="text-xs leading-5 text-[var(--admin-muted)]">{examParentMessageFailure(state.failureCode)}</p>}
  </div>;
}

export function ExamParentMessagesPanel({ examId, refreshKey = 0, onStatesChange, sendLabel = 'إعادة إرسال الرسائل المتوقفة' }: {
  examId: string;
  refreshKey?: number;
  onStatesChange: (states: Record<string, ExamParentMessageState>) => void;
  sendLabel?: string;
}) {
  const [summary, setSummary] = useState<ExamParentMessageSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState('');
  const operationIds = useRef<{ missing: string | null; failed: string | null }>({ missing: null, failed: null });
  const request = useRef<AbortController | null>(null);
  const load = useCallback(async () => {
    request.current?.abort();
    const controller = new AbortController();
    request.current = controller;
    setLoading(true);
    try {
      const data = await adminService.getExamParentMessages(examId, controller.signal);
      if (controller.signal.aborted) return;
      setSummary(data);
      onStatesChange(Object.fromEntries(data.attempts.map(state => [state.attemptId, state])));
      setError('');
    } catch (cause) {
      if (!controller.signal.aborted) setError(getApiErrorSummary(cause, 'تعذر تحميل حالات رسائل النتائج.'));
    } finally {
      if (!controller.signal.aborted) setLoading(false);
    }
  }, [examId, onStatesChange]);
  useEffect(() => {
    void load();
    return () => request.current?.abort();
  }, [load, refreshKey]);
  useEffect(() => {
    if (!summary || !summary.pendingCount && !summary.awaitingDeliveryCount) return;
    const timer = window.setInterval(() => { if (!document.hidden) void load(); }, summary.pendingCount ? 10000 : 30000);
    return () => window.clearInterval(timer);
  }, [summary, load]);

  const retryableFailedCount = summary?.attempts.filter(state => state.canRetry && state.status === 'Failed').length ?? 0;

  async function retry(failedOnly = false) {
    if (sending || !summary?.retryableCount || failedOnly && !retryableFailedCount) return;
    setSending(true);
    const mode = failedOnly ? 'failed' : 'missing';
    try {
      operationIds.current[mode] ??= createClientId();
      const result = await adminService.retryExamParentMessages(examId, operationIds.current[mode], undefined, failedOnly);
      operationIds.current[mode] = null;
      toast.success(result.queuedCount ? `تمت إضافة ${result.queuedCount} رسالة لقائمة الإرسال` : 'لا توجد رسائل تحتاج إعادة إرسال الآن');
      await load();
    } catch (cause) {
      toast.error(getApiErrorSummary(cause, 'تعذر إضافة الرسائل لقائمة الإرسال. حاول مرة أخرى.'));
    } finally { setSending(false); }
  }

  return <section aria-labelledby="exam-parent-messages-title" className="space-y-4 border-y border-[var(--admin-border)] py-5">
    <div className="flex flex-wrap items-start justify-between gap-4">
      <div className="space-y-1">
        <h2 id="exam-parent-messages-title" className="flex items-center gap-2 text-lg font-bold text-[var(--admin-text)]">
          <MessageCircle className="h-5 w-5 text-[var(--admin-primary)]" aria-hidden="true" /> رسائل النتائج على واتساب
        </h2>
        <p className="max-w-prose text-sm leading-6 text-[var(--admin-muted)]">اسم الطالب في الرسالة أول اسمين فقط. إعادة الإرسال تشمل الرسائل الفاشلة والنتائج التي توقفت قبل الإرسال.</p>
      </div>
      <div className="flex w-full flex-wrap gap-2 sm:w-auto">
        <button type="button" className="admin-btn-ghost min-h-11" disabled={loading || sending} onClick={() => void load()}>
          <RefreshCw className="h-4 w-4" aria-hidden="true" /> تحديث الحالات
        </button>
        <button type="button" className="admin-btn-ghost min-h-11" disabled={loading || sending || Boolean(error) || !retryableFailedCount}
          onClick={() => void retry(true)}>
          إعادة إرسال الفاشلة فقط ({retryableFailedCount})
        </button>
        <button type="button" className="admin-btn-primary min-h-11" disabled={loading || sending || Boolean(error) || !summary?.retryableCount}
          onClick={() => void retry()}>
          <Send className="h-4 w-4" aria-hidden="true" />
          {sending ? 'جاري تجهيز الرسائل…' : `${sendLabel}${summary ? ` (${summary.retryableCount})` : ''}`}
        </button>
      </div>
    </div>
    {loading && !summary && <p role="status" className="text-sm text-[var(--admin-muted)]">جاري تحميل حالات الإرسال…</p>}
    {error && <p role="alert" className="text-sm text-[var(--admin-danger)]">{error}</p>}
    {summary && <>
      <p className="text-base font-bold text-[var(--admin-text)]" aria-live="polite">
        الرسائل الناقصة: {summary.failedCount + summary.notSentCount}
        <span className="mr-2 text-sm font-normal text-[var(--admin-muted)]">جاهز للإرسال منها: {summary.retryableCount}</span>
      </p>
      <dl className="flex flex-wrap gap-x-8 gap-y-3 text-sm" aria-live="polite">
        {([['وصلت', summary.deliveredCount], ['فشلت', summary.failedCount], ['لم تُرسل', summary.notSentCount], ['قيد الإرسال', summary.pendingCount], ['تنتظر تأكيد الوصول', summary.awaitingDeliveryCount]] as const).map(([label, count]) =>
          <div key={label} className="flex items-baseline gap-2"><dt className="text-[var(--admin-muted)]">{label}</dt><dd className="font-bold tabular-nums text-[var(--admin-text)]">{count}</dd></div>)}
      </dl>
      {(!summary.enabled || summary.configurationError) && <p role="alert" className="text-sm text-[var(--admin-danger)]">
        {summary.configurationError ?? 'فعّل رسالة النتيجة لولي الأمر واختر القالب من إعدادات الامتحان أولًا.'}
      </p>}
      {summary.pendingCount > 0 && <p role="status" className="text-sm text-[var(--admin-muted)]">الإرسال مستمر في الخلفية، ويمكنك إغلاق الصفحة. الحالات تتحدث أثناء الإرسال.</p>}
      {summary.pendingCount === 0 && summary.awaitingDeliveryCount > 0 && <p role="status" className="text-sm text-[var(--admin-muted)]">واتساب قبل الرسائل، وجاري متابعة تأكيد وصولها لولي الأمر.</p>}
      {summary.uncertainCount > 0 && <p className="text-sm text-[var(--admin-muted)]">{summary.uncertainCount} رسالة تحتاج مراجعة لأن وصولها غير مؤكد.</p>}
      {summary.attempts.some(state => state.failureCode?.endsWith('131042')) && <p className="text-sm leading-6 text-[var(--admin-danger)]">بعض الرسائل فشلت بسبب أهلية حساب واتساب أو الدفع. راجع حساب Meta إذا استمر الخطأ بعد إعادة المحاولة.</p>}
    </>}
  </section>;
}
