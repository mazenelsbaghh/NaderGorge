'use client';

import type { StudentProfileExtendedDto } from '@/services/admin-service';
import { videoProgressPercent } from '@/lib/student-learning-progress';
import { formatCairoTimestamp } from '@/lib/cairo-time';

type Tracking = StudentProfileExtendedDto['watchTracking'];
type WatchActivity = Tracking['activities'][number];
const duration = (seconds: number) => `${Math.floor(seconds / 60)}د ${Math.floor(seconds % 60)}ث`;
function VideoActivity({ video }: { video: WatchActivity }) {
  const percent = videoProgressPercent(video);
  const started = (video.learningWatchedSeconds ?? 0) > 0 || video.actualWatchedSeconds > 0 || video.watchCount > 0 || video.watchedSeconds > 0 || (video.sessions?.length ?? 0) > 0;
  return <li className="space-y-3 py-4"><div className="flex flex-wrap items-start justify-between gap-3"><div className="min-w-0"><h5 className="break-words font-bold">{video.videoTitle}</h5><p className="mt-1 text-sm text-[var(--admin-muted)]">{video.isCompleted ? 'مكتمل' : started ? 'بدأ ولم يكمل' : 'لم يبدأ المشاهدة'} · {percent === null ? 'المدة غير مسجلة' : `${percent}%`}</p></div><span className="text-xs text-[var(--admin-muted)]">المشاهدات: {video.watchCount} من {video.maxWatchCount}{video.isLocked && ' · وصل للحد'}</span></div>
    <dl className="flex flex-wrap gap-x-6 gap-y-2 text-xs text-[var(--admin-muted)]"><div><dt className="inline">مشاهدة فعلية: </dt><dd className="inline">{duration(video.actualWatchedSeconds)}</dd></div><div><dt className="inline">آخر مشاهدة: </dt><dd className="inline">{video.lastWatchedAt ? formatCairoTimestamp(video.lastWatchedAt) : 'غير مسجلة'}</dd></div><div><dt className="inline">متوسط السرعة: </dt><dd className="inline"><bdi>{video.averagePlaybackRate}×</bdi></dd></div></dl>
    {(video.sessions?.length ?? 0) > 0 && <details><summary className="inline-flex min-h-11 cursor-pointer items-center text-sm font-bold underline">جلسات المشاهدة ({video.sessions!.length})</summary><ul className="space-y-2 text-xs text-[var(--admin-muted)]">{video.sessions!.map(session => <li key={session.id} className="flex flex-wrap justify-between gap-3"><span>{formatCairoTimestamp(session.startedAt)}</span><span>{duration(session.actualWatchedSeconds)}</span></li>)}</ul></details>}
  </li>;
}
export function TeacherStudentWatchHistory({ tracking }: { tracking: Tracking }) {
  const lessons = new Map<string, WatchActivity[]>();
  for (const activity of tracking.activities) {
    const videos = lessons.get(activity.lessonId) ?? [];
    videos.push(activity); lessons.set(activity.lessonId, videos);
  }
  return <section className="space-y-5 text-[var(--admin-text)]"><header><h3 className="text-lg font-bold">تفاصيل المشاهدة والجلسات</h3><p className="mt-2 text-sm text-[var(--admin-muted)]">مشاهدة فعلية مسجلة: {duration(tracking.totalActualWatchedSeconds)}. يظهر هنا سجل النشاط، واستخدم «تقرير المشاهدة» لمراجعة كل الحصص المتاحة للطالب.</p></header>
    {lessons.size === 0 ? <p className="py-8 text-center text-sm text-[var(--admin-muted)]">لا يوجد نشاط مشاهدة مسجل في محتواك حتى الآن.</p> : Array.from(lessons, ([lessonId, videos]) => <article key={lessonId} className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4"><header className="border-b border-[var(--admin-border)] pb-3"><h4 className="font-bold">{videos[0].lessonTitle}</h4><p className="mt-1 text-xs text-[var(--admin-muted)]">{[videos[0].packageName, videos[0].termTitle].filter(Boolean).join(' · ')}</p></header><ul className="divide-y divide-[var(--admin-border)]">{videos.map(video => <VideoActivity key={video.lessonVideoId} video={video} />)}</ul></article>)}
  </section>;
}
