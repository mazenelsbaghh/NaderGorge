'use client';

import { useEffect, useRef, useState } from 'react';
import { X, ZoomIn, ZoomOut } from 'lucide-react';
import { resolveMediaUrl } from '@/utils/resolve-media-url';

export default function LessonAidDialog({ title, summary, imageUrl, onClose }: {
  title: string;
  summary?: string;
  imageUrl?: string;
  onClose: () => void;
}) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const [zoomed, setZoomed] = useState(false);
  useEffect(() => {
    const dialog = dialogRef.current;
    dialog?.showModal();
    return () => dialog?.close();
  }, []);

  return (
    <dialog ref={dialogRef} onCancel={onClose} aria-label={title}
      className="lesson-aid-dialog m-auto max-h-[90dvh] w-[calc(100%-1rem)] max-w-4xl overflow-hidden rounded-2xl border border-slate-200 bg-white p-0 text-[#0A1D3D] shadow-xl backdrop:bg-black/65" dir="rtl">
      <header className="flex items-center gap-3 border-b border-slate-200 px-4 py-2">
        <h3 className="min-w-0 flex-1 text-base font-bold" dir="auto">{title}</h3>
        {imageUrl && <button type="button" className="flex min-h-11 min-w-11 items-center justify-center rounded-lg hover:bg-slate-100" aria-label={zoomed ? 'تصغير الخريطة' : 'تكبير الخريطة'} onClick={() => setZoomed(!zoomed)}>{zoomed ? <ZoomOut /> : <ZoomIn />}</button>}
        <button type="button" autoFocus onClick={onClose} aria-label="إغلاق" className="flex min-h-11 min-w-11 items-center justify-center rounded-lg hover:bg-slate-100"><X /></button>
      </header>
      <div className="max-h-[calc(90dvh-5rem)] overflow-auto overscroll-contain p-4">
        {summary && <p className="whitespace-pre-wrap text-base leading-8" dir="auto">{summary}</p>}
        {imageUrl && <>
          <p className="mb-3 text-sm text-slate-600">كبّر الخريطة واسحب لقراءة تفاصيلها.</p>
          {/* Keep intrinsic dimensions: a fill image in an auto-height panel collapses on phones. */}
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src={resolveMediaUrl(imageUrl)} alt={title} className={zoomed ? 'h-auto w-[180%] max-w-none' : 'mx-auto h-auto max-h-[70dvh] w-full object-contain'} />
        </>}
      </div>
    </dialog>
  );
}
