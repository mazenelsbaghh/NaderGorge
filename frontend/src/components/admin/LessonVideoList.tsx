'use client';

import { PlaySquare, Trash2, Edit2, GripVertical } from 'lucide-react';
import toast from 'react-hot-toast';

interface LessonVideoListProps {
  videos: any[];
}

export function LessonVideoList({ videos }: LessonVideoListProps) {
  if (!videos || videos.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-[var(--admin-border)] p-12 text-center">
        <div className="mb-4 rounded-full bg-[var(--admin-primary-15)] p-4 text-[var(--admin-primary)]">
          <PlaySquare className="h-8 w-8" />
        </div>
        <h4 className="mb-2 text-lg font-bold text-[var(--admin-text)]">لا يوجد فيديو بعد</h4>
        <p className="max-w-md text-sm text-[var(--admin-muted)] mb-6">
          أضف الفيديو الأول من النموذج أدناه لتبدأ في بث محتوى هذه الحصة.
        </p>
        <a 
          href="#add-video-form"
          className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-primary)] px-5 py-2.5 text-sm font-bold text-white shadow-sm transition hover:opacity-90"
        >
          + أضف فيديو
        </a>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {videos.map((video) => (
        <div
          key={video.id}
          className="flex flex-col sm:flex-row sm:items-center items-start justify-between gap-4 sm:gap-0 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] p-4 shadow-sm group"
        >
          <div className="flex items-start sm:items-center gap-3 sm:gap-4 w-full sm:w-auto">
            <div className="flex cursor-grab items-center px-1 text-[var(--admin-muted)] opacity-50 hover:opacity-100">
              <GripVertical className="h-5 w-5" />
            </div>
            <div className="rounded-lg bg-[var(--admin-card)] p-2.5 text-[var(--admin-text)] border border-[var(--admin-border)]">
              <PlaySquare className="h-4 w-4" />
            </div>
            <div>
              <h4 className="font-bold text-[var(--admin-text)]">{video.title}</h4>
              <div className="mt-2 sm:mt-1 flex flex-wrap items-center gap-2 text-[11px] sm:text-xs font-mono text-[var(--admin-muted)]">
                <span className="rounded bg-[var(--admin-bg)] px-1.5 py-0.5 border border-[var(--admin-border)]">
                  {video.provider || 'Bunny'}
                </span>
                <span className="rounded bg-[var(--admin-bg)] px-1.5 py-0.5 border border-[var(--admin-border)]">
                  مشاهدة: {video.maxWatchCount === 0 ? 'غير محدود' : `${video.maxWatchCount}×`}
                </span>
                <span className="rounded bg-[var(--admin-bg)] px-1.5 py-0.5 border border-[var(--admin-border)]">
                  ترتيب: {video.order}
                </span>
              </div>
            </div>
          </div>
          
          <div className="flex items-center gap-2 self-end sm:self-auto border-t sm:border-0 border-[var(--admin-border)] pt-3 sm:pt-0 w-full sm:w-auto justify-end opacity-60 group-hover:opacity-100 transition-opacity">
            <div className="relative group/edit">
              <button aria-label="تعديل الفيديو" disabled className="rounded-lg p-2 text-[var(--admin-muted)] opacity-40 cursor-not-allowed">
                <Edit2 className="h-4 w-4" />
              </button>
              <span className="pointer-events-none absolute -top-8 right-0 whitespace-nowrap rounded-lg bg-[var(--admin-text)] px-2 py-1 text-[10px] font-bold text-[var(--admin-bg)] opacity-0 transition-opacity group-hover/edit:opacity-100">قريباً</span>
            </div>
            <div className="relative group/del">
              <button aria-label="حذف الفيديو" disabled className="rounded-lg p-2 text-[var(--admin-muted)] opacity-40 cursor-not-allowed">
                <Trash2 className="h-4 w-4" />
              </button>
              <span className="pointer-events-none absolute -top-8 right-0 whitespace-nowrap rounded-lg bg-[var(--admin-text)] px-2 py-1 text-[10px] font-bold text-[var(--admin-bg)] opacity-0 transition-opacity group-hover/del:opacity-100">قريباً</span>
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}
