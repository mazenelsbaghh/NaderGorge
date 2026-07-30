'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { CheckCircle2, MessageSquareMore, ShieldX } from 'lucide-react';
import toast from 'react-hot-toast';

import { AdminDataTable, type AdminColumn } from '@/components/admin/AdminDataTable';
import { AdminModal } from '@/components/admin/AdminModal';
import { adminService, type ModerationCommunityCommentDto } from '@/services/admin-service';

const formatDate = (value?: string | null) =>
  value
    ? new Intl.DateTimeFormat('ar-EG', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
    : '—';

export function CommunityCommentsModerationTable() {
  const [comments, setComments] = useState<ModerationCommunityCommentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actingId, setActingId] = useState<string | null>(null);
  const [bulkAction, setBulkAction] = useState<'approve' | 'reject' | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [rejectingCommentIds, setRejectingCommentIds] = useState<string[]>([]);
  const [rejectReason, setRejectReason] = useState('');

  const loadComments = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const rows = await adminService.getPendingCommunityComments();
      setComments(rows);
      setSelectedIds(new Set());
    } catch {
      setError('تعذر تحميل تعليقات المجتمع. تحقق من الاتصال ثم أعد المحاولة.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadComments();
  }, [loadComments]);

  const handleModeration = useCallback(async (
    commentIds: string[],
    action: 'approve' | 'reject',
    reason?: string,
  ) => {
    if (commentIds.length === 0) return;

    const isBulk = commentIds.length > 1;
    if (isBulk) {
      setBulkAction(action);
    } else {
      setActingId(commentIds[0]);
    }

    try {
      const results = await Promise.allSettled(
        commentIds.map((commentId) =>
          action === 'approve'
            ? adminService.approveCommunityComment(commentId)
            : adminService.rejectCommunityComment(commentId, reason ?? ''),
        ),
      );
      const succeeded = results.filter((result) => result.status === 'fulfilled').length;
      const failed = results.length - succeeded;
      const actionLabel = action === 'approve' ? 'قبول' : 'رفض';

      if (failed === 0) {
        toast.success(
          commentIds.length === 1
            ? `تم ${actionLabel} التعليق.`
            : `تم ${actionLabel} ${succeeded} تعليقات.`,
        );
      } else if (succeeded === 0) {
        toast.error(`تعذر ${actionLabel} التعليقات المحددة.`);
      } else {
        toast.error(`تم ${actionLabel} ${succeeded} تعليقات، وتعذر تنفيذ الإجراء على ${failed}.`);
      }

      if (succeeded > 0) {
        await loadComments();
      }
    } finally {
      setActingId(null);
      setBulkAction(null);
    }
  }, [loadComments]);

  const handleReject = useCallback(async () => {
    if (rejectingCommentIds.length === 0 || !rejectReason.trim()) {
      return;
    }

    await handleModeration(rejectingCommentIds, 'reject', rejectReason.trim());
    setRejectingCommentIds([]);
    setRejectReason('');
  }, [handleModeration, rejectReason, rejectingCommentIds]);

  const toggleSelection = useCallback((commentId: string) => {
    setSelectedIds((current) => {
      const next = new Set(current);
      if (next.has(commentId)) next.delete(commentId);
      else next.add(commentId);
      return next;
    });
  }, []);

  const isMutating = actingId !== null || bulkAction !== null;

  const columns = useMemo<AdminColumn<ModerationCommunityCommentDto>[]>(() => [
    {
      key: 'select',
      label: 'تحديد',
      align: 'center',
      render: (row) => (
        <input
          type="checkbox"
          checked={selectedIds.has(row.id)}
          disabled={isMutating}
          onChange={() => toggleSelection(row.id)}
          aria-label={`تحديد تعليق الطالب ${row.studentName}`}
          className="h-4 w-4 accent-[var(--admin-primary)]"
        />
      ),
    },
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
          <p className="text-xs font-medium text-[var(--admin-muted)]">المنشور: {row.postId}</p>
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
            disabled={isMutating}
            onClick={() => void handleModeration([row.id], 'approve')}
            className="inline-flex items-center gap-2 rounded-full border border-[var(--admin-success-20)] bg-[var(--admin-success-10)] px-4 py-2 text-xs font-black text-[var(--admin-success)] transition hover:brightness-110 disabled:opacity-60"
          >
            <CheckCircle2 className="h-4 w-4" />
            قبول
          </button>
          <button
            type="button"
            disabled={isMutating}
            onClick={() => {
              setRejectingCommentIds([row.id]);
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
  ], [handleModeration, isMutating, selectedIds, toggleSelection]);

  return (
    <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
      <div className="mb-5 flex items-start justify-between gap-4">
        <div>
          <div className="mb-3 inline-flex items-center gap-2 rounded-full border border-[var(--admin-primary-15)] bg-[var(--admin-primary-10)] px-4 py-2 text-xs font-black tracking-[0.18em] text-[var(--admin-primary)]">
            <MessageSquareMore className="h-4 w-4" />
            تعليقات المجتمع
          </div>
          <h3 className="text-xl font-black text-[var(--admin-text)]">تعليقات المجتمع قيد المراجعة</h3>
          <p className="mt-2 max-w-3xl text-sm font-medium leading-relaxed text-[var(--admin-muted)]">
            هذه التعليقات لن تظهر للطلاب حتى يتم اعتمادها يدويًا.
          </p>
        </div>
        <div className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-5 py-4 text-center">
          <p className="text-xs font-bold tracking-[0.18em] text-[var(--admin-muted)]">معلّقة الآن</p>
          <p className="mt-2 text-3xl font-black text-[var(--admin-primary)]">{error ? '—' : comments.length}</p>
        </div>
      </div>

      {!error && comments.length > 0 && (
        <div className="mb-4 flex flex-wrap items-center gap-3 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
          <span className="text-sm font-black text-[var(--admin-text)]">
            تم تحديد {selectedIds.size} من {comments.length}
          </span>
          <button
            type="button"
            disabled={isMutating}
            onClick={() =>
              setSelectedIds(
                selectedIds.size === comments.length
                  ? new Set()
                  : new Set(comments.map((comment) => comment.id)),
              )
            }
            className="rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-2 text-xs font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)] disabled:opacity-60"
          >
            {selectedIds.size === comments.length ? 'إلغاء تحديد الكل' : 'تحديد الكل'}
          </button>
          <div className="mr-auto flex flex-wrap gap-2">
            <button
              type="button"
              disabled={selectedIds.size === 0 || isMutating}
              onClick={() => void handleModeration([...selectedIds], 'approve')}
              className="rounded-full bg-[var(--admin-success)] px-4 py-2 text-xs font-black text-white transition hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isMutating ? 'جارٍ القبول...' : 'قبول المحدد'}
            </button>
            <button
              type="button"
              disabled={selectedIds.size === 0 || isMutating}
              onClick={() => {
                setRejectingCommentIds([...selectedIds]);
                setRejectReason('');
              }}
              className="rounded-full bg-[var(--admin-danger)] px-4 py-2 text-xs font-black text-white transition hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isMutating ? 'جارٍ الرفض...' : 'رفض المحدد'}
            </button>
          </div>
        </div>
      )}

      <AdminDataTable
        data={comments}
        columns={columns}
        loading={loading}
        rowKey={(row) => row.id}
        emptyMessage="لا توجد تعليقات قيد المراجعة حاليًا."
        errorMessage={error}
        onRetry={() => void loadComments()}
      />

      <AdminModal
        open={rejectingCommentIds.length > 0}
        onClose={() => {
          if (isMutating) return;
          setRejectingCommentIds([]);
          setRejectReason('');
        }}
        title={rejectingCommentIds.length > 1 ? 'سبب رفض التعليقات المحددة' : 'سبب رفض التعليق'}
        subtitle="سيتم حفظ السبب في سجل المراجعة ولن تظهر التعليقات المرفوضة للطلاب."
      >
        <div className="space-y-5" dir="rtl">
          <div>
            <label htmlFor="community-comment-reject-reason" className="text-sm font-black text-[var(--admin-text)]">
              سبب الرفض
            </label>
            <textarea
              id="community-comment-reject-reason"
              value={rejectReason}
              onChange={(event) => setRejectReason(event.target.value)}
              rows={4}
              autoFocus
              disabled={isMutating}
              className="mt-2 w-full resize-none rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 text-sm font-medium text-[var(--admin-text)] outline-none transition focus:border-[var(--admin-primary)] disabled:opacity-60"
              placeholder="اكتب سبب الرفض..."
            />
          </div>
          <div className="flex items-center justify-end gap-3 border-t border-[var(--admin-border)] pt-4">
            <button
              type="button"
              disabled={isMutating}
              onClick={() => {
                setRejectingCommentIds([]);
                setRejectReason('');
              }}
              className="rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)] px-5 py-2.5 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)] disabled:opacity-60"
            >
              إلغاء
            </button>
            <button
              type="button"
              disabled={!rejectReason.trim() || isMutating}
              onClick={() => void handleReject()}
              className="rounded-full bg-[var(--admin-danger)] px-5 py-2.5 text-sm font-black text-white transition hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isMutating ? 'جارٍ الرفض...' : 'تأكيد الرفض'}
            </button>
          </div>
        </div>
      </AdminModal>
    </section>
  );
}
