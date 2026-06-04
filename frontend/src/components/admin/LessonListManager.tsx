'use client';

import { useCallback, useState, useEffect, forwardRef, useImperativeHandle } from 'react';
import { BookOpenText, Trash2, Edit2, GripVertical, Settings, RefreshCw } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { contentService, LessonSummaryDto } from '@/services/content-service';
import NeumorphButton from '@/components/ui/neumorph-button';

export interface LessonListManagerRef {
  reload: () => void;
}

interface LessonListManagerProps {
  sectionId: string;
}

export const LessonListManager = forwardRef<LessonListManagerRef, LessonListManagerProps>(
  ({ sectionId }, ref) => {
    const router = useRouter();
    const [lessons, setLessons] = useState<LessonSummaryDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [loadError, setLoadError] = useState(false);

    const loadLessons = useCallback(async () => {
      try {
        setLoading(true);
        setLoadError(false);
        const res = await contentService.getLessons(sectionId);
        const items = (res.data?.data || []) as LessonSummaryDto[];
        setLessons(items.sort((a, b) => a.order - b.order));
      } catch {
        setLoadError(true);
      } finally {
        setLoading(false);
      }
    }, [sectionId]);

    useImperativeHandle(ref, () => ({
      reload: loadLessons
    }), [loadLessons]);

    useEffect(() => {
      if (sectionId) {
        loadLessons();
      }
    }, [sectionId, loadLessons]);

    // ── Error / Retry ────────────────────────────────────────────────────
    if (loadError) {
      return (
        <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-[var(--admin-border)] p-12 text-center gap-4">
          <div className="rounded-full bg-red-100 p-4 text-red-500 dark:bg-red-900/20">
            <RefreshCw className="h-8 w-8" />
          </div>
          <h4 className="text-lg font-bold text-[var(--admin-text)]">تعذّر تحميل الحصص</h4>
          <p className="max-w-xs text-sm text-[var(--admin-muted)]">تحقق من اتصالك بالإنترنت ثم حاول مجدداً.</p>
          <NeumorphButton onClick={loadLessons} intent="ghost" size="md" pill>
            <RefreshCw className="h-4 w-4" /> إعادة المحاولة
          </NeumorphButton>
        </div>
      );
    }

    if (loading) {
      return (
        <div className="space-y-3">
          {[1, 2, 3].map((i) => (
            <div
              key={i}
              className="flex h-20 w-full animate-pulse items-center justify-between rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 shadow-sm"
              style={{ animationDelay: `${i * 100}ms` }}
            >
              <div className="flex items-center gap-4">
                <div className="h-12 w-12 rounded-xl bg-[var(--admin-muted)] opacity-20" />
                <div className="space-y-2">
                  <div className="h-4 w-32 rounded bg-[var(--admin-muted)] opacity-20" />
                  <div className="h-3 w-20 rounded bg-[var(--admin-muted)] opacity-20" />
                </div>
              </div>
            </div>
          ))}
        </div>
      );
    }

    if (lessons.length === 0) {
      return (
        <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-[var(--admin-border)] p-12 text-center">
          <div className="mb-4 rounded-full bg-[var(--admin-primary-15)] p-4 text-[var(--admin-primary)]">
            <BookOpenText className="h-8 w-8" />
          </div>
          <h4 className="mb-2 text-lg font-bold text-[var(--admin-text)]">لا يوجد حصة بعد</h4>
          <p className="max-w-xs text-sm text-[var(--admin-muted)] mb-6">
            الحصة تحتوي على الفيديوهات والواجبات — أضف الحصة الأولى لهذا القسم.
          </p>
          <NeumorphButton
            asChild
            intent="primary"
            size="lg"
            pill
          >
            <a href="#add-lesson-form">
              + أضف الحصة الأولى
            </a>
          </NeumorphButton>
        </div>
      );
    }

    return (
      <div className="space-y-4">
        {lessons.map((lesson) => (
          <div
            key={lesson.id}
            onClick={() => router.push(`/admin/content/lessons/${lesson.id}`)}
            className="flex items-center justify-between rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] p-4 shadow-sm transition-all hover:border-[var(--admin-primary-15)] cursor-pointer"
          >
            <div className="flex items-center gap-4">
              <div className="flex cursor-grab items-center px-1 text-[var(--admin-muted)] opacity-50 hover:opacity-100">
                <GripVertical className="h-5 w-5" />
              </div>
              <div className="rounded-lg bg-[var(--admin-card)] p-2.5 text-[var(--admin-text)] border border-[var(--admin-border)]">
                <BookOpenText className="h-4 w-4" />
              </div>
              <div>
                <h4 className="font-bold text-[var(--admin-text)]">{lesson.title}</h4>
                <p className="text-xs text-[var(--admin-muted)] mt-0.5 line-clamp-1 max-w-sm">{lesson.summary}</p>
                <div className="mt-1 flex items-center gap-2 text-xs font-mono text-[var(--admin-muted)]">
                  <span className="rounded bg-[var(--admin-bg)] px-1.5 py-0.5 border border-[var(--admin-border)]">
                    ترتيب: {lesson.order}
                  </span>
                  <span className="rounded bg-[var(--admin-primary-15)] px-1.5 py-0.5 text-[var(--admin-primary)] font-bold">
                    {lesson.price ? `${lesson.price} جنيه` : 'مجانى'}
                  </span>
                </div>
              </div>
            </div>
            
            <div className="flex items-center gap-2">
              <div className="relative group/settings">
                <NeumorphButton disabled intent="icon" size="icon" title="إعدادات الحصة قريباً">
                  <Settings className="h-4 w-4" />
                </NeumorphButton>
                <span className="pointer-events-none absolute -top-8 right-0 whitespace-nowrap rounded-lg bg-[var(--admin-text)] px-2 py-1 text-[10px] font-bold text-[var(--admin-bg)] opacity-0 transition-opacity group-hover/settings:opacity-100">قريباً</span>
              </div>
              <div className="relative group/edit">
                <NeumorphButton disabled intent="icon" size="icon" title="تعديل الحصة قريباً">
                  <Edit2 className="h-4 w-4" />
                </NeumorphButton>
                <span className="pointer-events-none absolute -top-8 right-0 whitespace-nowrap rounded-lg bg-[var(--admin-text)] px-2 py-1 text-[10px] font-bold text-[var(--admin-bg)] opacity-0 transition-opacity group-hover/edit:opacity-100">قريباً</span>
              </div>
              <div className="relative group/del">
                <NeumorphButton disabled intent="danger" size="icon" title="حذف الحصة قريباً">
                  <Trash2 className="h-4 w-4" />
                </NeumorphButton>
                <span className="pointer-events-none absolute -top-8 right-0 whitespace-nowrap rounded-lg bg-[var(--admin-text)] px-2 py-1 text-[10px] font-bold text-[var(--admin-bg)] opacity-0 transition-opacity group-hover/del:opacity-100">قريباً</span>
              </div>
            </div>
          </div>
        ))}
      </div>
    );
  }
);

LessonListManager.displayName = 'LessonListManager';
