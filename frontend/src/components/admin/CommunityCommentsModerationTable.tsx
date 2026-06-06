'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { CheckCircle2, MessageSquareMore, ShieldX } from 'lucide-react';
import toast from 'react-hot-toast';

import { AdminDataTable, type AdminColumn } from '@/components/admin/AdminDataTable';
import { adminService, type ModerationCommunityCommentDto } from '@/services/admin-service';

const formatDate = (value?: string | null) =>
  value
    ? new Intl.DateTimeFormat('ar-EG', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
    : '—';

export function CommunityCommentsModerationTable() {
  const [comments, setComments] = useState<ModerationCommunityCommentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [actingId, setActingId] = useState<string | null>(null);
  const [rejectingCommentId, setRejectingCommentId] = useState<string | null>(null);
  const [rejectReason, setRejectReason] = useState('');

  const loadComments = useCallback(async () => {
    setLoading(true);
    try {
      const rows = await adminService.getPendingCommunityComments();
      setComments(rows);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadComments();
  }, [loadComments]);

  const handleApprove = useCallback(async (commentId: string) => {
    setActingId(commentId);
    try {
      await adminService.approveCommunityComment(commentId);
      toast.success('تم قبول التعليق.');
      await loadComments();
    } finally {
      setActingId(null);
    }
  }, [loadComments]);

  const handleReject = useCallback(async () => {
    if (!rejectingCommentId || !rejectReason.trim()) {
      return;
    }

    setActingId(rejectingCommentId);
    try {
      await adminService.rejectCommunityComment(rejectingCommentId, rejectReason.trim());
      toast.success('تم رفض التعليق.');
      setRejectingCommentId(null);
      setRejectReason('');
      await loadComments();
    } finally {
      setActingId(null);
    }
  }, [loadComments, rejectReason, rejectingCommentId]);

  const columns = useMemo<AdminColumn<ModerationCommunityCommentDto>[]>(() => [
    {
      key: 'student',
      label: 'الطالب',
      render: (row) => (
        <div className="space-y-1">
          <p className="font-black text-[var(--admin-text)]">{row.studentName}</p>
          <p className="text-xs font-medium text-[var(--admin-muted)]">أضيف في {formatDate(row.createdAt)}</p>
        </div>
      ),
    },
    {
      key: 'comment',
      label: 'التعليق',
      render: (row) => (
        <div className="max-w-xl space-y-2">
          <p className="line-clamp-4 whitespace-pre-wrap text-sm font-medium leading-7 text-[var(--admin-text)]">
            {row.body}
          </p>
          <p className="text-xs font-medium text-[var(--admin-muted)]">البوست: {row.postId}</p>
        </div>
      ),
    },
    {
      key: 'actions',
      label: 'الإجراء',
      render: (row) => (
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            disabled={actingId === row.id}
            onClick={() => void handleApprove(row.id)}
            className="inline-flex items-center gap-2 rounded-full border border-[var(--admin-success-20)] bg-[var(--admin-success-10)] px-4 py-2 text-xs font-black text-[var(--admin-success)] transition hover:brightness-110 disabled:opacity-60"
          >
            <CheckCircle2 className="h-4 w-4" />
            قبول
          </button>
          <button
            type="button"
            disabled={actingId === row.id}
            onClick={() => {
              setRejectingCommentId(row.id);
              setRejectReason('');
            }}
            className="inline-flex items-center gap-2 rounded-full border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] px-4 py-2 text-xs font-black text-[var(--admin-danger)] transition hover:brightness-110 disabled:opacity-60"
          >
            <ShieldX className="h-4 w-4" />
            رفض
          </button>
        </div>
      ),
    },
  ], [actingId, handleApprove]);

  return (
    <section className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
      <div className="mb-5 flex items-start justify-between gap-4">
        <div>
          <div className="mb-3 inline-flex items-center gap-2 rounded-full border border-[var(--admin-primary-15)] bg-[var(--admin-primary-10)] px-4 py-2 text-xs font-black tracking-[0.18em] text-[var(--admin-primary)]">
            <MessageSquareMore className="h-4 w-4" />
            Community Comments
          </div>
          <h3 className="text-xl font-black text-[var(--admin-text)]">تعليقات المجتمع قيد المراجعة</h3>
          <p className="mt-2 max-w-3xl text-sm font-medium leading-relaxed text-[var(--admin-muted)]">
            هذه التعليقات لن تظهر للطلاب حتى يتم اعتمادها يدويًا.
          </p>
        </div>
        <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-5 py-4 text-center">
          <p className="text-xs font-bold tracking-[0.18em] text-[var(--admin-muted)]">معلّقة الآن</p>
          <p className="mt-2 text-3xl font-black text-[var(--admin-primary)]">{comments.length}</p>
        </div>
      </div>

      <AdminDataTable
        data={comments}
        columns={columns}
        loading={loading}
        rowKey={(row) => row.id}
        emptyMessage="لا توجد تعليقات قيد المراجعة حاليًا."
      />

      {rejectingCommentId && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="reject-community-comment-title"
          className="fixed inset-0 z-[100] flex items-center justify-center p-4"
          dir="rtl"
        >
          <button
            type="button"
            aria-label="إغلاق"
            onClick={() => {
              setRejectingCommentId(null);
              setRejectReason('');
            }}
            className="absolute inset-0 cursor-default bg-black/60 backdrop-blur-[2px]"
          />
          <div className="relative w-full max-w-lg rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-bg)] p-6 shadow-2xl">
            <h4 id="reject-community-comment-title" className="text-lg font-black text-[var(--admin-text)]">
              سبب رفض التعليق
            </h4>
            <p className="mt-2 text-sm font-medium leading-6 text-[var(--admin-muted)]">
              سيتم حفظ السبب في سجل المراجعة ولن يظهر التعليق للطلاب.
            </p>
            <textarea
              value={rejectReason}
              onChange={(event) => setRejectReason(event.target.value)}
              rows={4}
              autoFocus
              className="mt-5 w-full resize-none rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 text-sm font-medium text-[var(--admin-text)] outline-none transition focus:border-[var(--admin-primary)]"
              placeholder="اكتب سبب الرفض..."
            />
            <div className="mt-5 flex items-center justify-end gap-3">
              <button
                type="button"
                onClick={() => {
                  setRejectingCommentId(null);
                  setRejectReason('');
                }}
                className="rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)] px-5 py-2.5 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)]"
              >
                إلغاء
              </button>
              <button
                type="button"
                disabled={!rejectReason.trim() || actingId === rejectingCommentId}
                onClick={() => void handleReject()}
                className="rounded-full bg-[var(--admin-danger)] px-5 py-2.5 text-sm font-black text-white transition hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-60"
              >
                رفض التعليق
              </button>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
