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

  const handleReject = useCallback(async (commentId: string) => {
    const reason = window.prompt('سبب الرفض');
    if (!reason?.trim()) {
      return;
    }

    setActingId(commentId);
    try {
      await adminService.rejectCommunityComment(commentId, reason.trim());
      toast.success('تم رفض التعليق.');
      await loadComments();
    } finally {
      setActingId(null);
    }
  }, [loadComments]);

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
            onClick={() => void handleReject(row.id)}
            className="inline-flex items-center gap-2 rounded-full border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] px-4 py-2 text-xs font-black text-[var(--admin-danger)] transition hover:brightness-110 disabled:opacity-60"
          >
            <ShieldX className="h-4 w-4" />
            رفض
          </button>
        </div>
      ),
    },
  ], [actingId, handleApprove, handleReject]);

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
    </section>
  );
}
