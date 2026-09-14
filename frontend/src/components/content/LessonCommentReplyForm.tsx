'use client';

import { useId, useRef, useState } from 'react';

export function LessonCommentReplyForm({
  recipient,
  onSubmit,
  onCancel,
}: {
  recipient: string;
  onSubmit: (body: string) => Promise<void>;
  onCancel: () => void;
}) {
  const inputId = useId();
  const [body, setBody] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');
  const inFlight = useRef(false);

  async function submitReply(event: React.FormEvent) {
    event.preventDefault();
    if (inFlight.current || !body.trim()) return;
    inFlight.current = true;
    setSubmitting(true);
    setError('');
    try {
      await onSubmit(body.trim());
      setBody('');
    } catch {
      setError('تعذر تأكيد إرسال الرد. تحقق من الردود قبل المحاولة مرة أخرى.');
    } finally {
      inFlight.current = false;
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={submitReply} className="mt-3 min-w-0 space-y-3">
      <label
        htmlFor={inputId}
        className="block text-sm font-bold text-[var(--admin-text)] [overflow-wrap:anywhere]"
      >
        رد على {recipient}
      </label>
      <textarea
        id={inputId}
        value={body}
        onChange={(event) => setBody(event.target.value)}
        required
        maxLength={2000}
        disabled={submitting}
        autoFocus
        className="admin-input min-h-20 min-w-0 w-full resize-y rounded-2xl text-base"
        placeholder="اكتب ردك على هذا التعليق..."
      />
      {error && (
        <p role="alert" className="text-sm text-[var(--admin-danger)]">
          {error}
        </p>
      )}
      <div className="flex flex-wrap items-center gap-2">
        <button
          type="submit"
          disabled={submitting || !body.trim()}
          className="admin-btn-primary min-h-11 disabled:opacity-50"
        >
          {submitting ? 'جارٍ إرسال الرد...' : 'إرسال الرد'}
        </button>
        <button
          type="button"
          disabled={submitting}
          onClick={onCancel}
          className="admin-btn-secondary min-h-11"
        >
          إلغاء الرد
        </button>
        <span className="text-xs text-[var(--admin-muted)]">
          {body.trim().length}/2000
        </span>
      </div>
    </form>
  );
}
