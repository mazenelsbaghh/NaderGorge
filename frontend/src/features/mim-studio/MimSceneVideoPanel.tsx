'use client';

import { useEffect, useState } from 'react';
import { Film, RefreshCw } from 'lucide-react';
import type { MimVideo } from './contract';
import { mimStudioService, studioError } from './service';

export function MimSceneVideoPanel({ lessonId, scene, scriptVersion, connected, disabled, model }: {
  model: string; lessonId: string; scene: number; scriptVersion: string | null; connected: boolean; disabled: boolean;
}) {
  const [video, setVideo] = useState<MimVideo | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [quotedVersion, setQuotedVersion] = useState<string | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    mimStudioService.video(lessonId, scene, controller.signal).then(setVideo)
      .catch(cause => { if (!controller.signal.aborted) setError(studioError(cause)); });
    return () => controller.abort();
  }, [lessonId, scene]);

  const act = async (action: 'quote' | 'submit' | 'refresh') => {
    if (busy) return;
    setBusy(true); setError('');
    try {
      const next = action === 'quote' ? await mimStudioService.quoteVideo(lessonId, scene, model) :
        action === 'submit' && video ? await mimStudioService.submitVideo(lessonId, scene, video.version) : await mimStudioService.video(lessonId, scene);
      setVideo(next);
      if (action === 'quote') setQuotedVersion(scriptVersion);
    } catch (cause) {
      setError(studioError(cause));
      // After an uncertain paid response, read status before making any other decision.
      if (action === 'submit') {
        try { setVideo(await mimStudioService.video(lessonId, scene)); }
        catch { setError('تعذر التحقق من نتيجة الطلب. حدّث الحالة قبل أي محاولة أخرى.'); }
      }
    } finally { setBusy(false); }
  };
  const quoted = video?.state === 'quoted';
  const canQuote = !video || quoted || video.state === 'failed';
  const pending = video && ['submitting', 'running'].includes(video.state);
  const unknown = video?.state === 'unknown';
  return <section aria-label="توليد فيديو المشهد" className="mt-7 space-y-4 border-t border-[var(--admin-border)] pt-6">
    <h3 className="flex items-center gap-2 text-lg font-black text-[var(--admin-text)]"><Film className="h-5 w-5" />فيديو المشهد {scene + 1}</h3>
    <p className="max-w-prose text-sm leading-7 text-[var(--admin-muted)]">٣٠ ثانية · أفقي 16:9 · شيت ميم وشيت نادر مرفقان بالتوليد. راجع الاسكربت قبل عرض التكلفة.</p>
    {!connected && <p className="text-sm text-[var(--admin-muted)]">اربط حساب Higgsfield من قسم الربط أولاً.</p>}
    {disabled && <p className="text-sm text-[var(--admin-muted)]">احفظ الاسكربت وتأكد من مصدره قبل توليد الفيديو.</p>}
    {error && <p role="alert" className="text-sm leading-7 text-red-700 dark:text-red-300">{error}</p>}
    <div className="flex flex-wrap items-center gap-3">
      {canQuote && <button type="button" className="admin-btn-ghost min-h-11" disabled={busy || disabled || !connected || !scriptVersion} onClick={() => void act('quote')}>
        {busy ? 'جاري تجهيز الطلب…' : quoted ? 'تحديث تكلفة هذا المشهد' : 'عرض تكلفة هذا المشهد'}
      </button>}
      {quoted && video.model === model && <>
        <p className="font-bold text-[var(--admin-text)]">{video.quote}</p>
        <button type="button" className="admin-btn-primary min-h-11 disabled:opacity-50"
          disabled={busy || disabled || !connected || quotedVersion !== scriptVersion || video.model !== model || Date.parse(video.expiresAt) <= new Date().getTime()}
          onClick={() => void act('submit')}>توليد هذا المشهد وخصم التكلفة المعروضة</button>
      </>}
      {video && !quoted && <button type="button" className="admin-btn-ghost min-h-11" disabled={busy} onClick={() => void act('refresh')}><RefreshCw className="h-4 w-4" />تحديث حالة الفيديو</button>}
    </div>
    {quoted && video.model !== model && <p role="status" className="text-sm leading-7 text-[var(--admin-muted)]">الموديل اتغيّر. اعرض التكلفة من جديد قبل التوليد.</p>}
    {video && !quoted && <p className="text-sm text-[var(--admin-muted)]">موديل الطلب المحفوظ: {video.model === 'wan3_0_prime' ? 'Wan 3.0 Prime' : 'Seedance 2.5'}</p>}
    {pending && <p role="status" className="text-sm leading-7 text-[var(--admin-muted)]">المشهد قيد التوليد على Higgsfield. حدّث الحالة بعد قليل؛ الانتقال لمشهد آخر لا يرسل طلب توليد جديد.</p>}
    {unknown && <p role="alert" className="text-sm leading-7 text-amber-800 dark:text-amber-200">نتيجة الإرسال غير مؤكدة. لن نعيد إرسال الطلب حتى لا يتكرر الخصم. راجع سجل التوليد في حساب Higgsfield لتحديد نتيجته.</p>}
    {video?.state === 'failed' && <p role="alert" className="text-sm leading-7 text-red-700 dark:text-red-300">Higgsfield أعلن فشل التوليد. الاسكربت محفوظ؛ راجع تفاصيل الطلب في حسابك.</p>}
    {video?.state === 'review_required' && <p className="text-sm leading-7 text-[var(--admin-muted)]">الطلب يحتاج مراجعة من داخل حساب Higgsfield قبل عرضه هنا.</p>}
    {video?.jobId && <p className="break-all text-xs text-[var(--admin-muted)]">رقم الطلب: {video.jobId}</p>}
    {video?.urls.map(url => <div key={url} className="space-y-2">
      <video controls preload="metadata" src={url} className="aspect-video w-full rounded-lg bg-black" aria-label={`فيديو المشهد ${scene + 1}`} />
      <a href={url} target="_blank" rel="noopener noreferrer" className="admin-btn-ghost inline-flex min-h-11">فتح فيديو المشهد</a>
    </div>)}
  </section>;
}
