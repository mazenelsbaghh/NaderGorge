'use client';

import { useCallback, useState } from 'react';
import SecureVideoPlayer from '@/components/video/SecureVideoPlayer';
import { formatPlayerTime } from '@/lib/player-time';

export default function VcdnPreview() {
  const [playback, setPlayback] = useState({ current: 0, duration: 0 });
  const handlePlaybackTime = useCallback((current: number, duration: number) => {
    setPlayback(previous => previous.current === current && previous.duration === duration
      ? previous : { current, duration });
  }, []);
  return <main className="mx-auto max-w-5xl px-4 py-8" dir="rtl">
    <h1 className="mb-2 text-2xl font-bold">فيديو VCDN بالمشغل بتاع المنصة</h1>
    <p className="mb-5 text-sm text-[var(--muted-foreground)]">تجربة محلية — الفيديو بيتبث من VCDN مباشرة. مفيش مشاهدات بتتحسب.</p>
    <SecureVideoPlayer lessonVideoId="local-vcdn-preview" localVcdnHlsPreview
      onPlaybackTime={handlePlaybackTime} />
    <p className="mt-3 text-sm tabular-nums" dir="ltr">{formatPlayerTime(playback.current)} / {formatPlayerTime(playback.duration)}</p>
  </main>;
}
