'use client';

import { useState } from 'react';
import toast from 'react-hot-toast';
import type { ModerationLessonCommentDto } from '@/services/admin-service';
import { LessonCommentReplyForm } from './LessonCommentReplyForm';

export function CommentParentQuote({ body }: { body?: string | null }) {
  if (!body) return null;
  return (
    <blockquote className="mt-3 border-s-2 border-[var(--admin-border)] ps-3 text-sm text-[var(--admin-muted)]">
      <span className="font-bold">رد على: </span>
      <span className="whitespace-pre-wrap break-words">{body}</span>
    </blockquote>
  );
}

export function ModerationCommentReply({
  comment,
  onReply,
  onReplied,
}: {
  comment: ModerationLessonCommentDto;
  onReply: (commentId: string, body: string) => Promise<unknown>;
  onReplied: () => Promise<void>;
}) {
  const [composing, setComposing] = useState(false);
  if (comment.status === 'Rejected') return null;

  async function submit(body: string) {
    await onReply(comment.id, body);
    setComposing(false);
    toast.success('تم حفظ الرد تحت التعليق الأصلي.');
    try {
      await onReplied();
    } catch {
      toast.error('تم إرسال الرد، لكن تعذر تحديث القائمة.');
    }
  }

  return (
    <div className="mt-3">
      {composing ? (
        <LessonCommentReplyForm
          recipient={comment.studentName}
          onSubmit={submit}
          onCancel={() => setComposing(false)}
        />
      ) : (
        <button
          type="button"
          onClick={() => setComposing(true)}
          className="admin-btn-secondary min-h-11"
        >
          رد على التعليق
        </button>
      )}
    </div>
  );
}
