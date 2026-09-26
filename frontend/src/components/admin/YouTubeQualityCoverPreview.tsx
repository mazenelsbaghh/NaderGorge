'use client';

import { useEffect, useRef, useState } from 'react';
import { youtubeQualityCoverPercent } from '@/lib/youtube-quality-preview';

export function YouTubeQualityCoverPreview({ videoId, device, coverPercent }: {
  videoId: string;
  device: 'mobile' | 'desktop';
  coverPercent: number;
}) {
  const container = useRef<HTMLDivElement>(null);
  const [availableWidth, setAvailableWidth] = useState(0);
  const canvasWidth = device === 'mobile' ? 390 : 960;
  const canvasHeight = device === 'mobile' ? 360 : 540;
  const scale = availableWidth / canvasWidth;

  useEffect(() => {
    const element = container.current;
    if (!element) return;
    const observer = new ResizeObserver(() => setAvailableWidth(element.clientWidth));
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  return <div className="space-y-2">
    <div ref={container} className={`relative mx-auto w-full overflow-hidden rounded-xl bg-black ${device === 'mobile' ? 'max-w-[390px]' : ''}`} style={{ aspectRatio: `${canvasWidth} / ${canvasHeight}` }}>
      <div className="absolute left-0 top-0 origin-top-left" style={{ width: canvasWidth, height: canvasHeight, transform: `scale(${scale})` }}>
        {videoId ? <iframe src={`/api/video/preview?provider=youtube&id=${encodeURIComponent(videoId)}`} title={`معاينة الشريط على ${device === 'mobile' ? 'الموبايل' : 'الكمبيوتر'}`} className="absolute inset-0 size-full border-0" allow="autoplay; encrypted-media" />
          : <div className="grid size-full place-items-center px-6 text-center text-lg text-white/70">أدخل رابط فيديو يوتيوب لمعاينة الشريط عليه</div>}
        <div className="pointer-events-none absolute inset-x-0 top-0 h-12 bg-black" />
        <div data-youtube-cover-preview className="absolute inset-x-0 bottom-0 bg-black" style={{ height: `calc(76px + ${youtubeQualityCoverPercent(coverPercent)}%)`, maxHeight: 'calc(100% - 48px)' }} />
      </div>
    </div>
    <p className="text-xs leading-6 text-[var(--admin-muted)]">معاينة مساحة الفيديو والشريط {device === 'mobile' ? 'على الموبايل' : 'على الكمبيوتر'}. أزرار مشغّل الدرس تظهر أسفل هذه المساحة. زيادة التغطية تخفي جزءًا أكبر من الصورة.</p>
  </div>;
}
