'use client';

import { useCallback, useRef, useState } from 'react';
import SecureVideoPlayer, { type SecureVideoPlayerRef } from '@/components/video/SecureVideoPlayer';
import { formatPlayerTime } from '@/lib/player-time';
import type { VideoChapterDto } from '@/services/content-service';

const chapters: VideoChapterDto[] = [
  { id: 'preview-opening', order: 1, title: 'بداية الفيديو', startTime: 0, endTime: 180,
    summaryText: 'ابدأ الفيديو وجرّب اختيار الجودة والسرعة وأزرار التقديم والترجيع.' },
  { id: 'preview-middle', order: 2, title: 'منتصف الفيديو', startTime: 180, endTime: 420,
    summaryText: 'استخدم شريط التقدم أو قائمة الفصول للانتقال إلى أي جزء.' },
  { id: 'preview-ending', order: 3, title: 'نهاية الفيديو', startTime: 420, endTime: 635,
    summaryText: 'جرّب ملء الشاشة وتغيير الجودة أثناء استمرار التشغيل.' },
];

const reactions = [
  { seconds: 92, understood: 6, confused: 1, example: 2 },
  { seconds: 258, understood: 3, confused: 4, example: 1 },
  { seconds: 477, understood: 7, confused: 1, example: 3 },
];

export default function HlsPreview() {
  const player = useRef<SecureVideoPlayerRef>(null);
  const [narrow, setNarrow] = useState(false);
  const [playback, setPlayback] = useState({ current: 0, duration: 0 });
  const handlePlaybackTime = useCallback((current: number, duration: number) => {
    setPlayback(previous => previous.current === current && previous.duration === duration ? previous : { current, duration });
  }, []);
  const activeChapter = chapters.find(chapter => playback.current >= chapter.startTime && playback.current < chapter.endTime)?.id;

  return <main className="mx-auto w-full max-w-7xl px-4 py-8 sm:px-6" dir="rtl">
    <header className="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div>
        <p className="mb-2 text-sm font-bold text-[var(--admin-primary)]">معاينة محلية</p>
        <h1 className="text-2xl font-black text-[var(--foreground)] sm:text-3xl">مشغل فيديو يوتيوب HLS داخل المنصة</h1>
        <p className="mt-2 max-w-2xl text-sm leading-7 text-[var(--muted-foreground)]">
          مشغل الدروس الفعلي بفصول الفيديو والجودة والسرعة والتحكم وملء الشاشة. الفيديو المعروض مثال عام للتجربة.
        </p>
      </div>
      <button type="button" onClick={() => setNarrow(value => !value)}
        className="min-h-11 rounded-lg border border-[var(--border)] bg-[var(--card)] px-4 text-sm font-semibold text-[var(--foreground)]">
        {narrow ? 'عرض الكمبيوتر' : 'معاينة عرض الموبايل'}
      </button>
    </header>

    <div className={narrow ? 'mx-auto max-w-[390px]' : 'grid gap-6 lg:grid-cols-[minmax(0,1fr)_18rem]'}>
      <section className="min-w-0" aria-label="معاينة مشغل الدرس">
        <SecureVideoPlayer ref={player} lessonVideoId="local-youtube-hls-preview" localYouTubeHlsPreview
          chapters={chapters} reactionDensity={reactions}
          onPlaybackTime={handlePlaybackTime} />
        <div className="mt-4 flex flex-wrap items-center justify-between gap-2 text-sm text-[var(--muted-foreground)]">
          <span>تجربة محلية بدون احتساب مشاهدات أو تعديل تقدم الطالب</span>
          <span dir="ltr" className="tabular-nums">{formatPlayerTime(playback.current)} / {formatPlayerTime(playback.duration)}</span>
        </div>
      </section>

      <aside className={`${narrow ? 'mt-5' : ''} rounded-xl border border-[var(--border)] bg-[var(--card)] p-4`} aria-label="فصول الفيديو التجريبية">
        <h2 className="mb-3 text-lg font-bold text-[var(--foreground)]">فصول الفيديو</h2>
        <div className="space-y-2">
          {chapters.map(chapter => <button key={chapter.id} type="button" onClick={() => player.current?.seekTo(chapter.startTime)}
            aria-current={activeChapter === chapter.id ? 'true' : undefined}
            className={`flex min-h-11 w-full items-center justify-between gap-3 rounded-lg px-3 text-start text-sm transition-colors ${
              activeChapter === chapter.id ? 'bg-[var(--admin-primary-15)] text-[var(--foreground)]' : 'text-[var(--muted-foreground)] hover:bg-[var(--admin-primary-15)]'
            }`}>
            <span>{chapter.title}</span><span dir="ltr" className="tabular-nums">{formatPlayerTime(chapter.startTime)}</span>
          </button>)}
        </div>
        <p className="mt-5 border-t border-[var(--border)] pt-4 text-xs leading-6 text-[var(--muted-foreground)]">
          استخدم زر المعلومات داخل المشغل لرؤية ملخص الفصل، أو اختر فصلًا من هنا للانتقال إليه.
        </p>
      </aside>
    </div>
  </main>;
}
