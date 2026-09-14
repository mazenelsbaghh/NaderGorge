'use client';

import { useCallback } from 'react';
import Link from 'next/link';
import { Check, ChevronLeft, Play } from 'lucide-react';
import { contentService, type LessonDetailDto } from '@/services/content-service';
import type { MyLessonDto } from '@/services/student-service';
import { usePlatformQuery } from '@/components/providers/QueryProvider';
import { useAuthStore } from '@/stores/auth-store';
import { videoProgressPercent } from '@/lib/student-learning-progress';
import { LearningProgress } from './LearningProgress';

export function LastLessonProgress({ lesson }: { lesson: MyLessonDto }) {
  const userId = useAuthStore(state => state.user?.id);
  const queryFn = useCallback(async ({ signal }: { signal: AbortSignal }) => {
    const response = await contentService.getLessonDetail(lesson.id, signal);
    if (!response.data.success || !response.data.data) throw new Error('Lesson progress unavailable');
    return response.data.data;
  }, [lesson.id]);
  const query = usePlatformQuery<LessonDetailDto>({ queryKey: ['student', 'lesson-progress', userId, lesson.id], queryFn, staleTime: 30_000, enabled: Boolean(userId) });
  return (
    <section aria-labelledby="last-lesson" className="space-y-4 border-t border-[var(--admin-border)] pt-5">
      <h2 id="last-lesson" className="text-xl font-black">آخر حصة فتحتها</h2>
      <Link href={`/student/packages/${lesson.packageId}/lessons/${lesson.id}`} className="flex min-h-12 items-center gap-3 focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)]">
        <div className="min-w-0 flex-1"><h3 className="text-lg font-bold">{lesson.title} • {lesson.packageName}</h3><p className="text-sm text-[var(--admin-muted)]">{lesson.teacherName}</p></div>
        <ChevronLeft className="h-5 w-5 shrink-0" aria-hidden="true" />
      </Link>
      <LearningProgress percent={lesson.watchProgressPercent ?? null} label="تقدّم الحصة" />
      {lesson.totalVideoSeconds ? <p className="text-sm text-[var(--admin-muted)]">{Math.floor((lesson.recordedWatchSeconds ?? 0) / 60)} من {Math.ceil(lesson.totalVideoSeconds / 60)} دقيقة</p> : null}
      {query.error ? <div role="alert" className="text-sm text-[var(--admin-muted)]">تعذر تحميل تفاصيل الفيديوهات. <button type="button" className="min-h-11 font-bold underline" onClick={() => void query.refetch()}>أعد المحاولة</button></div>
        : !query.data ? <div className="h-28 animate-pulse rounded-xl bg-[var(--admin-card-soft)]" role="status" aria-label="جارٍ تحميل تقدّم الفيديوهات" />
        : <ul className="divide-y divide-[var(--admin-border)]">
          {query.data.videos.filter(video => video.hasAccess).map(video => {
            const percent = videoProgressPercent(video);
            return <li key={video.id} className="flex items-center gap-3 py-3">
              <span className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full ${percent === 100 ? 'bg-[var(--admin-accent)] text-white' : 'bg-[var(--admin-card-strong)] text-[var(--admin-muted)]'}`}>{percent === 100 ? <Check className="h-4 w-4" aria-label="مكتمل" /> : <Play className="h-3 w-3" aria-hidden="true" />}</span>
              <div className="min-w-0 flex-1"><LearningProgress percent={percent} label={video.title} /></div>
              <span className="shrink-0 text-xs text-[var(--admin-muted)]">{video.durationSeconds ? `${Math.ceil(video.durationSeconds / 60)} د` : 'المدة غير متاحة'}</span>
            </li>;
          })}
        </ul>}
    </section>
  );
}
