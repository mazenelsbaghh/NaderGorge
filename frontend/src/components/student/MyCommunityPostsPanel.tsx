'use client';

import { Clock3, BarChart3 } from 'lucide-react';

import type { MyCommunityPostDto } from '@/services/community-service';

type MyCommunityPostsPanelProps = {
  posts: MyCommunityPostDto[];
  loading?: boolean;
};

const formatDate = (value: string) =>
  new Intl.DateTimeFormat('ar-EG', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));

const statusLabel = (status: string) => {
  switch (status) {
    case 'Approved':
      return 'مقبول';
    case 'Rejected':
      return 'مرفوض';
    default:
      return 'قيد المراجعة';
  }
};

const statusClasses = (status: string) => {
  switch (status) {
    case 'Approved':
      return 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400';
    case 'Rejected':
      return 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400';
    default:
      return 'bg-[#e4e6eb] text-gray-800 dark:bg-[var(--admin-card-soft)] dark:text-[var(--admin-muted)]';
  }
};

export function MyCommunityPostsPanel({ posts, loading = false }: MyCommunityPostsPanelProps) {
  return (
    <section className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-sm dark:shadow-none p-4">
      <div className="mb-4 flex items-center gap-2 border-b border-[var(--admin-border)] pb-3">
        <Clock3 className="h-5 w-5 text-gray-500 dark:text-[var(--admin-muted)]" />
        <h3 className="text-[16px] font-bold text-[var(--admin-text)]">منشوراتك وحالتها</h3>
      </div>

      {loading ? (
        <div className="space-y-3">
          <div className="h-20 animate-pulse rounded-lg bg-[var(--admin-card-soft)]" />
          <div className="h-20 animate-pulse rounded-lg bg-[var(--admin-card-soft)]" />
        </div>
      ) : posts.length === 0 ? (
        <div className="rounded-lg border border-dashed border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-8 text-center">
          <p className="text-sm font-bold text-[var(--admin-text)]">لم ترسل أي بوستات بعد.</p>
        </div>
      ) : (
        <div className="space-y-3">
          {posts.map((post) => (
            <article
              key={post.id}
              className="rounded-lg border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3"
            >
              <div className="flex flex-wrap items-center justify-between gap-3 mb-2">
                <div className="flex items-center gap-2">
                  <p className="text-[12px] font-medium text-gray-500 dark:text-[var(--admin-muted)]">{formatDate(post.createdAt)}</p>
                  {post.isPoll && (
                     <span className="flex items-center gap-1 text-[11px] font-bold text-[#0866ff]/80">
                       <BarChart3 className="h-3 w-3" />
                       استطلاع
                     </span>
                  )}
                </div>
                <span className={`rounded-md px-2 py-0.5 text-[11px] font-bold ${statusClasses(post.status)}`}>
                  {statusLabel(post.status)}
                </span>
              </div>
              <p className="whitespace-pre-wrap text-[14px] leading-snug text-[var(--admin-text)] line-clamp-3">
                {post.body}
              </p>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}
