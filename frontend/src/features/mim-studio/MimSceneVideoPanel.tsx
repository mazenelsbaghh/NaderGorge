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
  const [reviewedAccount, setReviewedAccount] = useState(false);
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, []);
  const [quotedVersion, setQuotedVersion] = useState<string | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    mimStudioService.video(lessonId, scene, controller.signal).then(setVideo)
      .catch(cause => { if (!controller.signal.aborted) setError(studioError(cause)); });
    return () => controller.abort();
  }, [lessonId, scene]);

  const act = async (action: 'quote' | 'submit' | 'refresh' | 'review') => {
    if (busy) return;
    setBusy(true); setError('');
    try {
      const next = action === 'quote' ? await mimStudioService.quoteVideo(lessonId, scene, model) :
        action === 'review' && video ? await mimStudioService.reviewVideo(lessonId, scene, video.version, reviewedAccount) :
        action === 'submit' && video ? await mimStudioService.submitVideo(lessonId, scene, video.version) : await mimStudioService.video(lessonId, scene);
      setVideo(next); setReviewedAccount(false); setNow(Date.now());
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
  const canQuote = !video || quoted || ['failed', 'retry_ready'].includes(video.state);
  const pending = video && ['submitting', 'running'].includes(video.state);
  const unknown = video?.state === 'unknown';
  const reviewReady = !!video?.reviewAvailableAt && Date.parse(video.reviewAvailableAt) <= now;
  const visibleError = error || video?.error;
  return <section aria-label="توليد فيديو المشهد" className="mt-7 space-y-4 border-t border-[var(--admin-border)] pt-6">
    <h3 className="flex items-center gap-2 text-lg font-black text-[var(--admin-text)]"><Film className="h-5 w-5" />فيديو المشهد {scene + 1}</h3>
    <p className="max-w-prose text-sm leading-7 text-[var(--admin-muted)]">٣٠ ثانية · أفقي 16:9 · شيت ميم وشيت نادر مرفقان بالتوليد. راجع الاسكربت قبل عرض التكلفة.</p>
    {!connected && <p className="text-sm text-[var(--admin-muted)]">اربط حساب Higgsfield من قسم الربط أولاً.</p>}
    {disabled && <p className="text-sm text-[var(--admin-muted)]">احفظ الاسكربت وتأكد من مصدره قبل توليد الفيديو.</p>}
    {visibleError && <p role="alert" className="text-sm leading-7 text-red-700 dark:text-red-300">{visibleError}</p>}
    <div className="flex flex-wrap items-center gap-3">
      {canQuote && <button type="button" className="admin-btn-ghost min-h-11" disabled={busy || disabled || !connected || !scriptVersion} onClick={() => void act('quote')}>
        {busy ? 'جاري تجهيز الطلب…' : quoted ? 'تحديث تكلفة هذا المشهد' : 'عرض تكلفة هذا المشهد'}
      </button>}
      {quoted && video.model === model && <>
        <p className="font-bold text-[var(--admin-text)]">{video.quote}</p>
        <button type="button" className="admin-btn-primary min-h-11 disabled:opacity-50"
          disabled={busy || disabled || !connected || quotedVersion !== scriptVersion || video.model !== model || Date.parse(video.expiresAt) <= now}
          onClick={() => void act('submit')}>توليد هذا المشهد وخصم التكلفة المعروضة</button>
      </>}
      {video && !quoted && <button type="button" className="admin-btn-ghost min-h-11" disabled={busy} onClick={() => void act('refresh')}><RefreshCw className="h-4 w-4" />تحديث حالة الفيديو</button>}
    </div>
    {quoted && video.model === model && (quotedVersion !== scriptVersion || Date.parse(video.expiresAt) <= now) && <p role="status" className="text-sm leading-7 text-[var(--admin-muted)]">حدّث تكلفة المشهد لتفعيل زر التوليد؛ عرض التكلفة صالح لمدة خمس دقائق.</p>}
    {quoted && video.model !== model && <p role="status" className="text-sm leading-7 text-[var(--admin-muted)]">الموديل اتغيّر. اعرض التكلفة من جديد قبل التوليد.</p>}
    {video && !quoted && <p className="text-sm text-[var(--admin-muted)]">موديل الطلب المحفوظ: {video.model === 'wan3_0_prime' ? 'Wan 3.0 Prime' : 'Seedance 2.5'}</p>}
    {pending && <p role="status" className="text-sm leading-7 text-[var(--admin-muted)]">المشهد قيد التوليد على Higgsfield. حدّث الحالة بعد قليل؛ الانتقال لمشهد آخر لا يرسل طلب توليد جديد.</p>}
    {unknown && <p role="alert" className="text-sm leading-7 text-amber-800 dark:text-amber-200">نتيجة الإرسال غير مؤكدة. لن نعيد إرسال الطلب حتى لا يتكرر الخصم. راجع سجل التوليد في حساب Higgsfield لتحديد نتيجته.</p>}
    {unknown && !video.jobId && <div className="space-y-3 rounded-lg border border-[var(--admin-border)] p-4">
      <a href="https://higgsfield.ai/ai/video" target="_blank" rel="noopener noreferrer" className="admin-btn-ghost inline-flex min-h-11">فتح سجل Higgsfield</a>
      <p className="text-sm leading-7 text-[var(--admin-muted)]">لو الفيديو ظهر في السجل أو اتخصمت تكلفته، ما تبدأش محاولة جديدة. لو مفيش فيديو ولا خصم، أكد المراجعة علشان تقدر تعرض تكلفة جديدة. الخطوة دي لا ترسل فيديو ولا تخصم رصيد.</p>
      {!reviewReady && <p role="status" className="text-sm text-[var(--admin-muted)]">المراجعة متاحة بعد خمس دقائق من آخر إرسال، لإتاحة ظهور الطلب في سجل Higgsfield.</p>}
      <label className="flex items-start gap-3 text-sm leading-7">
        <input type="checkbox" className="mt-2" checked={reviewedAccount} disabled={busy || !reviewReady} onChange={event => setReviewedAccount(event.target.checked)} />
        راجعت سجل التوليد والرصيد: لم يبدأ الفيديو ولم تُخصم تكلفته.
      </label>
      <button type="button" className="admin-btn-ghost min-h-11" disabled={busy || !reviewReady || !reviewedAccount} onClick={() => void act('review')}>حفظ المراجعة وإتاحة تسعير جديد</button>
    </div>}
    {video?.state === 'retry_ready' && <p role="status" className="text-sm leading-7 text-[var(--admin-muted)]">تم حفظ مراجعتك. اعرض التكلفة من جديد، ثم وافق على التوليد عند الاستعداد.</p>}
    {video?.state === 'failed' && <p role="alert" className="text-sm leading-7 text-red-700 dark:text-red-300">Higgsfield أعلن فشل التوليد. الاسكربت محفوظ؛ راجع تفاصيل الطلب في حسابك.</p>}
    {video?.state === 'review_required' && <p className="text-sm leading-7 text-[var(--admin-muted)]">الطلب يحتاج مراجعة من داخل حساب Higgsfield قبل عرضه هنا.</p>}
    {video?.jobId && <p className="break-all text-xs text-[var(--admin-muted)]">رقم الطلب: {video.jobId}</p>}
    {video?.urls.map(url => <div key={url} className="space-y-2">
      <video controls preload="metadata" src={url} className="aspect-video w-full rounded-lg bg-black" aria-label={`فيديو المشهد ${scene + 1}`} />
      <a href={url} target="_blank" rel="noopener noreferrer" className="admin-btn-ghost inline-flex min-h-11">فتح فيديو المشهد</a>
    </div>)}
  </section>;
}
