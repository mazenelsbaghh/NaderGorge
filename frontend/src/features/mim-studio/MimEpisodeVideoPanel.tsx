'use client';

import { useEffect, useState } from 'react';
import { Film, RefreshCw } from 'lucide-react';
import { mimStudioService, studioError } from './service';
import type { MimEpisodeVideo } from './contract';

export function MimEpisodeVideoPanel({ lessonId, scriptVersion, sceneCount, complete, disabled }: {
  lessonId: string; scriptVersion: string | null; sceneCount: number; complete: boolean; disabled: boolean;
}) {
  const [assembly, setAssembly] = useState<MimEpisodeVideo | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [preview, setPreview] = useState('');
  const pending = assembly?.state === 'queued' || assembly?.state === 'running';

  useEffect(() => {
    const controller = new AbortController();
    const read = () => mimStudioService.episodeVideo(lessonId, controller.signal).then(setAssembly)
      .catch(cause => { if (!controller.signal.aborted) setError(studioError(cause)); });
    void read();
    const timer = pending ? window.setInterval(() => void read(), 5000) : undefined;
    return () => { controller.abort(); window.clearInterval(timer); };
  }, [lessonId, scriptVersion, pending]);

  useEffect(() => {
    const controller = new AbortController();
    let objectUrl = '';
    setPreview('');
    if (assembly?.state === 'completed' && !disabled) {
      void mimStudioService.episodeFile(lessonId, controller.signal).then(file => {
        if (controller.signal.aborted) return;
        objectUrl = URL.createObjectURL(file); setPreview(objectUrl);
      }).catch(cause => { if (!controller.signal.aborted) setError(studioError(cause, 'تعذر تحميل فيديو الحلقة. حدّث الحالة.')); });
    }
    return () => { controller.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [lessonId, scriptVersion, assembly?.state, disabled]);

  const assemble = async () => {
    if (!scriptVersion || busy) return;
    setBusy(true); setError('');
    try { setAssembly(await mimStudioService.assembleEpisode(lessonId, scriptVersion)); }
    catch (cause) { setError(studioError(cause)); }
    finally { setBusy(false); }
  };
  const refresh = async () => {
    setError('');
    try { setAssembly(await mimStudioService.episodeVideo(lessonId)); }
    catch (cause) { setError(studioError(cause)); }
  };

  return <section aria-label="فيديو الحلقة النهائي" className="mt-7 space-y-4 border-t border-[var(--admin-border)] pt-6">
    <h3 className="flex items-center gap-2 text-lg font-black text-[var(--admin-text)]"><Film className="h-5 w-5" />فيديو الحلقة النهائي</h3>
    <p className="max-w-prose text-sm leading-7 text-[var(--admin-muted)]">يجمع {sceneCount} مشاهد بالترتيب، مع Fade خروج ودخول عند الانتقال. المدة {sceneCount * 30} ثانية، والصوت محفوظ، ومن غير عناوين أو كتابة مضافة.</p>
    {!complete && <p className="text-sm leading-7 text-[var(--admin-muted)]">كمّل كتابة مشاهد الحلقة وتوليد فيديو لكل مشهد أولًا.</p>}
    {error && <p role="alert" className="text-sm leading-7 text-red-700 dark:text-red-300">{error}</p>}
    {assembly?.error && <p role="status" className="text-sm leading-7 text-[var(--admin-muted)]">{assembly.error}</p>}
    <div className="flex flex-wrap gap-3">
      <button type="button" className="admin-btn-primary min-h-11 disabled:opacity-50" disabled={busy || pending || disabled || !complete || !scriptVersion || assembly?.state === 'waiting'}
        onClick={() => void assemble()}>{busy ? 'جاري بدء المونتاج…' : pending ? 'جاري تجميع الحلقة…' : assembly?.state === 'completed' ? 'تحديث تجميع الحلقة' : 'تجميع الحلقة بانتقالات Fade'}</button>
      <button type="button" className="admin-btn-ghost min-h-11" disabled={busy} onClick={() => void refresh()}><RefreshCw className="h-4 w-4" />تحديث حالة الحلقة</button>
    </div>
    {pending && <p role="status" className="text-sm text-[var(--admin-muted)]">المونتاج جاري — {Math.round(assembly.progress)}٪. تقدر تكمل شغلك وترجع للفيديو هنا.</p>}
    {assembly?.state === 'completed' && !preview && !disabled && <p role="status" className="text-sm text-[var(--admin-muted)]">الحلقة جاهزة، جاري تحميل المعاينة…</p>}
    {preview && <div className="space-y-3"><video controls preload="metadata" src={preview} className="aspect-video w-full rounded-lg bg-black" aria-label="معاينة الحلقة المجمعة" />
      <a href={preview} download="meem-papa-nader-episode.mp4" className="admin-btn-ghost inline-flex min-h-11">تنزيل فيديو الحلقة</a></div>}
  </section>;
}
