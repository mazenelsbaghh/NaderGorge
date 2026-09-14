'use client';

import { useState } from 'react';
import { X, ZoomIn, ZoomOut } from 'lucide-react';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import { timeLabel } from '@/services/video-learning-service';
import type { VideoChapterDto } from '@/services/content-service';

export function PlayerChapterPanel({ chapter, chapters, initialTab, onClose, onSeek }: {
  chapter: Omit<VideoChapterDto, 'order'>; chapters: Omit<VideoChapterDto, 'order'>[]; initialTab: 'summary' | 'map';
  onClose: () => void; onSeek: (seconds: number) => void;
}) {
  const [tab, setTab] = useState(initialTab);
  const [zoomed, setZoomed] = useState(false);
  return <aside aria-label="فصول الفيديو والخريطة الذهنية" dir="rtl" onClick={e => e.stopPropagation()}
    onPointerDown={e => e.stopPropagation()} onKeyDown={e => { e.stopPropagation(); if (e.key === 'Escape') onClose(); }}
    className="absolute inset-y-2 right-2 z-[var(--z-modal)] flex w-[min(90%,22rem)] min-w-0 flex-col overflow-hidden rounded-lg bg-[var(--admin-card)] text-[var(--admin-text)]">
    <header className="flex shrink-0 items-center gap-2 border-b border-[var(--admin-border)] px-3">
      <h3 className="min-w-0 flex-1 truncate font-bold">{chapter.title}</h3>
      <button type="button" aria-label="إغلاق معلومات الفصل" onClick={onClose} className="flex size-11 shrink-0 items-center justify-center"><X size={18} /></button>
    </header>
    <div className="flex shrink-0 gap-2 px-2">
      <button className="min-h-11 px-2 text-sm" aria-pressed={tab === 'summary'} onClick={() => setTab('summary')}>الفصول والملخص</button>
      {chapter.mindmapImageUrl && <button className="min-h-11 px-2 text-sm" aria-pressed={tab === 'map'} onClick={() => setTab('map')}>الخريطة الذهنية</button>}
      {tab === 'map' && <button className="ms-auto flex size-11 items-center justify-center" aria-label={zoomed ? 'تصغير الخريطة' : 'تكبير الخريطة'} onClick={() => setZoomed(z => !z)}>{zoomed ? <ZoomOut size={18} /> : <ZoomIn size={18} />}</button>}
    </div>
    <div className="min-h-0 flex-1 overflow-auto overscroll-contain p-3">
      {tab === 'map' && chapter.mindmapImageUrl ? <>
        {/* Intrinsic image size supports zooming without leaving the player or native fullscreen. */}
        {/* eslint-disable-next-line @next/next/no-img-element */}
        <img src={resolveMediaUrl(chapter.mindmapImageUrl)} alt={`الخريطة الذهنية: ${chapter.title}`} className={zoomed ? 'h-auto w-[180%] max-w-none' : 'h-auto w-full'} />
      </> : <>
        {chapter.summaryText && <p className="mb-3 whitespace-pre-wrap text-sm leading-7">{chapter.summaryText}</p>}
        <nav aria-label="الانتقال لفصل" className="space-y-1">{chapters.map(c => <button type="button" key={c.id} aria-current={c.id === chapter.id ? 'true' : undefined}
          className={`flex min-h-11 w-full items-center justify-between gap-2 rounded-lg px-2 text-start text-sm ${c.id === chapter.id ? 'bg-[var(--admin-primary-15)]' : ''}`} onClick={() => onSeek(c.startTime)}>
          <span>{c.title}</span><span>{timeLabel(c.startTime)}</span>
        </button>)}</nav>
      </>}
    </div>
  </aside>;
}
