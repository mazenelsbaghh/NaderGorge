'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { CheckCircle2, Filter, MessageSquareText, ShieldX, ThumbsUp } from 'lucide-react';
import toast from 'react-hot-toast';

import { AdminDataTable, type AdminColumn } from '@/components/admin/AdminDataTable';
import { AdminModal } from '@/components/admin/AdminModal';
import { adminService, type ModerationCommunityPostDto } from '@/services/admin-service';

type FilterStatus = 'All' | 'Pending' | 'Approved' | 'Rejected';

const FILTER_OPTIONS: FilterStatus[] = ['All', 'Pending', 'Approved', 'Rejected'];

const filterLabel: Record<FilterStatus, string> = {
  All: 'الكل',
  Pending: 'قيد المراجعة',
  Approved: 'مقبول',
  Rejected: 'مرفوض',
};

const formatDate = (value?: string | null) =>
  value
    ? new Intl.DateTimeFormat('ar-EG', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
    : '—';

const statusClasses = (status: string) => {
  switch (status) {
    case 'Approved':
      return 'border-[var(--admin-success-20)] bg-[var(--admin-success-10)] text-[var(--admin-success)]';
    case 'Rejected':
      return 'border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] text-[var(--admin-danger)]';
    default:
      return 'border-[var(--admin-primary-15)] bg-[var(--admin-primary-10)] text-[var(--admin-primary)]';
  }
};

const statusLabel = (status: string) => filterLabel[status as FilterStatus] ?? status;

export function CommunityPostsModerationTable() {
  const [posts, setPosts] = useState<ModerationCommunityPostDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actingId, setActingId] = useState<string | null>(null);
  const [bulkAction, setBulkAction] = useState<'approve' | 'reject' | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [rejectingPostIds, setRejectingPostIds] = useState<string[]>([]);
  const [activeFilter, setActiveFilter] = useState<FilterStatus>('Pending');

  const loadPosts = useCallback(async (filter: FilterStatus) => {
    setLoading(true);
    setError(null);
    try {
      const rows = await adminService.getCommunityPostsForModeration(filter);
      setPosts(rows);
      setSelectedIds(new Set());
    } catch {
      setError('تعذر تحميل منشورات المجتمع. تحقق من الاتصال ثم أعد المحاولة.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadPosts(activeFilter);
  }, [activeFilter, loadPosts]);

  const handleModeration = useCallback(async (postIds: string[], action: 'approve' | 'reject') => {
    if (postIds.length === 0) return;

    const isBulk = postIds.length > 1;
    if (isBulk) {
      setBulkAction(action);
    } else {
      setActingId(postIds[0]);
    }

    try {
      const results = await Promise.allSettled(
        postIds.map((postId) =>
          action === 'approve'
            ? adminService.approveCommunityPost(postId)
            : adminService.rejectCommunityPost(postId),
        ),
      );
      const succeeded = results.filter((result) => result.status === 'fulfilled').length;
      const failed = results.length - succeeded;
      const actionLabel = action === 'approve' ? 'قبول' : 'رفض';

      if (failed === 0) {
        toast.success(
          postIds.length === 1
            ? `تم ${actionLabel} المنشور.`
            : `تم ${actionLabel} ${succeeded} منشورات.`,
        );
      } else if (succeeded === 0) {
        toast.error(`تعذر ${actionLabel} المنشورات المحددة.`);
      } else {
        toast.error(`تم ${actionLabel} ${succeeded} منشورات، وتعذر تنفيذ الإجراء على ${failed}.`);
      }

      if (succeeded > 0) {
        await loadPosts(activeFilter);
      }
    } finally {
      setActingId(null);
      setBulkAction(null);
    }
  }, [activeFilter, loadPosts]);

  const toggleSelection = useCallback((postId: string) => {
    setSelectedIds((current) => {
      const next = new Set(current);
      if (next.has(postId)) next.delete(postId);
      else next.add(postId);
      return next;
    });
  }, []);

  const pendingCount = useMemo(
    () => posts.filter((post) => post.status === 'Pending').length,
    [posts],
  );
  const pendingPosts = useMemo(
    () => posts.filter((post) => post.status === 'Pending'),
    [posts],
  );
  const isMutating = actingId !== null || bulkAction !== null;

  const columns = useMemo<AdminColumn<ModerationCommunityPostDto>[]>(() => [
    {
      key: 'select',
      label: 'تحديد',
      align: 'center',
      render: (row) =>
        row.status === 'Pending' ? (
          <input
            type="checkbox"
            checked={selectedIds.has(row.id)}
            disabled={isMutating}
            onChange={() => toggleSelection(row.id)}
            aria-label={`تحديد منشور الطالب ${row.studentName}`}
            className="h-4 w-4 accent-[var(--admin-primary)]"
          />
        ) : (
          <span className="text-[var(--admin-muted)]">—</span>
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
      key: 'post',
      label: 'المنشور',
      render: (row) => (
        <div className="max-w-xl space-y-2">
          <p className="line-clamp-4 whitespace-pre-wrap text-sm font-medium leading-7 text-[var(--admin-text)]">
            {row.body}
          </p>
          {row.reviewedByName && (
            <p className="text-xs font-medium text-[var(--admin-muted)]">
              آخر مراجعة: {row.reviewedByName} في {formatDate(row.reviewedAt)}
            </p>
          )}
        </div>
      ),
    },
    {
      key: 'engagement',
      label: 'التفاعل',
      render: (row) => (
        <div className="flex flex-wrap gap-2">
          <span className="rounded-full border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-3 py-1 text-xs font-black text-[var(--admin-muted)]">
            <ThumbsUp className="ml-1 inline h-3.5 w-3.5" />
            {row.likeCount}
          </span>
          <span className="rounded-full border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-3 py-1 text-xs font-black text-[var(--admin-muted)]">
            <MessageSquareText className="ml-1 inline h-3.5 w-3.5" />
            {row.commentCount}
          </span>
        </div>
      ),
    },
    {
      key: 'status',
      label: 'الحالة',
      render: (row) => (
        <span className={`inline-flex rounded-full border px-3 py-1 text-xs font-black ${statusClasses(row.status)}`}>
          {statusLabel(row.status)}
        </span>
      ),
    },
    {
      key: 'actions',
      label: 'الإجراء',
      render: (row) =>
        row.status === 'Pending' ? (
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
              onClick={() => setRejectingPostIds([row.id])}
              className="inline-flex items-center gap-2 rounded-full border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] px-4 py-2 text-xs font-black text-[var(--admin-danger)] transition hover:brightness-110 disabled:opacity-60"
            >
              <ShieldX className="h-4 w-4" />
              رفض
            </button>
          </div>
        ) : (
          <span className="text-xs font-bold text-[var(--admin-muted)]">لا توجد إجراءات إضافية</span>
        ),
    },
  ], [handleModeration, isMutating, selectedIds, toggleSelection]);

  return (
    <div className="space-y-6">
      <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <div className="mb-3 inline-flex items-center gap-2 rounded-full border border-[var(--admin-primary-15)] bg-[var(--admin-primary-10)] px-4 py-2 text-xs font-black tracking-[0.18em] text-[var(--admin-primary)]">
              <MessageSquareText className="h-4 w-4" />
              Community Moderation
            </div>
            <h3 className="text-xl font-black text-[var(--admin-text)]">مراجعة منشورات مجتمع الطلاب</h3>
            <p className="mt-2 max-w-3xl text-sm font-medium leading-relaxed text-[var(--admin-muted)]">
              تظهر هنا كل المنشورات المرسلة من الطلاب. المنشورات قيد المراجعة فقط تحتاج قرارًا، بينما يحتفظ المقبول والمرفوض بسجل آخر مراجعة.
            </p>
          </div>
          <div className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-5 py-4 text-center">
            <p className="text-xs font-bold tracking-[0.18em] text-[var(--admin-muted)]">قيد المراجعة ضمن النتائج</p>
            <p className="mt-2 text-3xl font-black text-[var(--admin-primary)]">{error ? '—' : pendingCount}</p>
          </div>
        </div>
      </section>

      <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
        <div className="mb-5 flex flex-wrap items-center gap-3">
          <div className="inline-flex items-center gap-2 text-sm font-black text-[var(--admin-text)]">
            <Filter className="h-4 w-4 text-[var(--admin-primary)]" />
            تصفية الحالة
          </div>
          {FILTER_OPTIONS.map((filter) => {
            const active = filter === activeFilter;
            return (
              <button
                key={filter}
                type="button"
                disabled={loading || isMutating}
                onClick={() => setActiveFilter(filter)}
                className={`rounded-full px-4 py-2 text-xs font-black transition ${
                  active
                    ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-sm'
                    : 'border border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                }`}
              >
                {filterLabel[filter]}
              </button>
            );
          })}
        </div>

        {!error && pendingPosts.length > 0 && (
          <div className="mb-4 flex flex-wrap items-center gap-3 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
            <span className="text-sm font-black text-[var(--admin-text)]">
              تم تحديد {selectedIds.size} من {pendingPosts.length} قيد المراجعة
            </span>
            <button
              type="button"
              disabled={isMutating}
              onClick={() =>
                setSelectedIds(
                  selectedIds.size === pendingPosts.length
                    ? new Set()
                    : new Set(pendingPosts.map((post) => post.id)),
                )
              }
              className="rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-2 text-xs font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)] disabled:opacity-60"
            >
              {selectedIds.size === pendingPosts.length ? 'إلغاء تحديد الكل' : 'تحديد الكل'}
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
                onClick={() => setRejectingPostIds([...selectedIds])}
                className="rounded-full bg-[var(--admin-danger)] px-4 py-2 text-xs font-black text-white transition hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {isMutating ? 'جارٍ الرفض...' : 'رفض المحدد'}
              </button>
            </div>
          </div>
        )}

        <AdminDataTable
          data={posts}
          columns={columns}
          loading={loading}
          rowKey={(row) => row.id}
          errorMessage={error}
          onRetry={() => void loadPosts(activeFilter)}
          emptyMessage={
            activeFilter === 'Pending'
              ? 'لا توجد منشورات قيد المراجعة حاليًا.'
              : 'لا توجد منشورات مطابقة لهذا الفلتر.'
          }
          rowActionLabel={(row) => `عرض تفاصيل منشور الطالب ${row.studentName}`}
          expandedRowRender={(row) => (
            <div className="space-y-4">
              <div>
                <p className="text-xs font-bold tracking-[0.18em] text-[var(--admin-muted)]">النص الكامل</p>
                <p className="mt-2 whitespace-pre-wrap text-sm font-medium leading-8 text-[var(--admin-text)]">
                  {row.body}
                </p>
              </div>
              <div className="grid gap-3 md:grid-cols-4">
                <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3">
                  <p className="text-xs font-bold tracking-[0.18em] text-[var(--admin-muted)]">الحالة</p>
                  <p className="mt-2 text-sm font-black text-[var(--admin-text)]">{statusLabel(row.status)}</p>
                </div>
                <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3">
                  <p className="text-xs font-bold tracking-[0.18em] text-[var(--admin-muted)]">وقت الإرسال</p>
                  <p className="mt-2 text-sm font-black text-[var(--admin-text)]">{formatDate(row.createdAt)}</p>
                </div>
                <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3">
                  <p className="text-xs font-bold tracking-[0.18em] text-[var(--admin-muted)]">الإعجابات</p>
                  <p className="mt-2 text-sm font-black text-[var(--admin-text)]">{row.likeCount}</p>
                </div>
                <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3">
                  <p className="text-xs font-bold tracking-[0.18em] text-[var(--admin-muted)]">آخر مراجعة</p>
                  <p className="mt-2 text-sm font-black text-[var(--admin-text)]">
                    {row.reviewedByName ? `${row.reviewedByName} • ${formatDate(row.reviewedAt)}` : 'لم تتم المراجعة بعد'}
                  </p>
                </div>
              </div>
            </div>
          )}
        />
      </section>

      <AdminModal
        open={rejectingPostIds.length > 0}
        onClose={() => {
          if (!isMutating) setRejectingPostIds([]);
        }}
        title={rejectingPostIds.length > 1 ? 'تأكيد رفض المنشورات المحددة' : 'تأكيد رفض المنشور'}
        subtitle="سيتم إخفاء المنشورات المرفوضة عن الطلاب وتسجيل قرار المراجعة."
      >
        <div className="space-y-5" dir="rtl">
          <p className="text-sm font-medium leading-7 text-[var(--admin-text)]">
            هل تريد متابعة رفض {rejectingPostIds.length > 1 ? `${rejectingPostIds.length} منشورات` : 'هذا المنشور'}؟
          </p>
          <div className="flex items-center justify-end gap-3 border-t border-[var(--admin-border)] pt-4">
            <button
              type="button"
              disabled={isMutating}
              onClick={() => setRejectingPostIds([])}
              className="rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)] px-5 py-2.5 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)] disabled:opacity-60"
            >
              إلغاء
            </button>
            <button
              type="button"
              disabled={isMutating}
              onClick={async () => {
                const ids = rejectingPostIds;
                await handleModeration(ids, 'reject');
                setRejectingPostIds([]);
              }}
              className="rounded-full bg-[var(--admin-danger)] px-5 py-2.5 text-sm font-black text-white transition hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isMutating ? 'جارٍ الرفض...' : 'تأكيد الرفض'}
            </button>
          </div>
        </div>
      </AdminModal>
    </div>
  );
}
