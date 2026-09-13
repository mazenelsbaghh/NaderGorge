'use client';

import { createClientId } from '@/lib/client-id';
import { forwardRef, useCallback, useEffect, useImperativeHandle, useRef, useState, type ComponentProps } from 'react';
import { useSearchParams } from 'next/navigation';
import SecureVideoPlayer, { type SecureVideoPlayerRef } from '@/components/video/SecureVideoPlayer';
import { LearningActivityCard } from './LearningActivityCard';
import { LearningNotebook } from './LearningNotebook';
import { learningError, videoLearningService, type LearningActivity, type LearningEntry, type LearningSnapshot } from '@/services/video-learning-service';

type Props = ComponentProps<typeof SecureVideoPlayer>;
export const InteractiveVideoPlayer = forwardRef<SecureVideoPlayerRef, Props>(function InteractiveVideoPlayer(props, forwardedRef) {
  const { onEnded } = props;
  const player = useRef<SecureVideoPlayerRef>(null);
  const [snapshot, setSnapshot] = useState<LearningSnapshot | null>(null);
  const [seconds, setSeconds] = useState(0);
  const [duration, setDuration] = useState(0);
  const [ended, setEnded] = useState(false);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [retry, setRetry] = useState(0);
  const pendingEntry = useRef<{ key: string; id: string } | null>(null);
  const [explanations, setExplanations] = useState<Record<string, string>>({});
  const [dismissed, setDismissed] = useState<Set<string>>(new Set());
  const [notice, setNotice] = useState('');
  const sentEnd = useRef(false);
  const initialSeekDone = useRef(false);
  const search = useSearchParams();
  const requestedTime = Number(search.get('t'));
  const requestedVideo = search.get('videoId');
  useEffect(() => {
    if (initialSeekDone.current || duration <= 0 || requestedVideo !== props.lessonVideoId || !Number.isFinite(requestedTime) || requestedTime <= 0) return;
    initialSeekDone.current = true;
    player.current?.seekTo(Math.min(requestedTime, duration));
  }, [duration, requestedTime, requestedVideo, props.lessonVideoId]);
  useImperativeHandle(forwardedRef, () => ({ seekTo: t => player.current?.seekTo(t), play: () => player.current?.play(), pause: () => player.current?.pause() }), []);
  useEffect(() => {
    const abort = new AbortController();
    setError('');
    videoLearningService.read(props.lessonVideoId, false, abort.signal).then(setSnapshot).catch(e => { if (!abort.signal.aborted) setError(learningError(e)); });
    return () => abort.abort();
  }, [props.lessonVideoId, retry]);
  const seek = (time: number) => { setEnded(false); player.current?.seekTo(time); };
  const record = async (kind: string, text: string, title = '', activityId?: string, at = seconds) => {
    if (!snapshot || busy) return;
    const key = JSON.stringify([props.lessonVideoId, snapshot.version, kind, text, title, activityId, Math.floor(at)]);
    if (pendingEntry.current?.key !== key) pendingEntry.current = { key, id: createClientId() };
    setBusy(true); setError('');
    try {
      const result = await videoLearningService.record(props.lessonVideoId, snapshot.version, {
        id: pendingEntry.current.id, kind, seconds: Math.floor(at), text, title, activityId,
      });
      pendingEntry.current = null;
      setSnapshot(previous => previous ? { ...previous, entries: [result.entry, ...previous.entries.filter(e => e.id !== result.entry.id)] } : previous);
      if (activityId) setExplanations(previous => ({ ...previous, [activityId]: result.explanation }));
      setNotice(kind === 'ask' ? 'سؤالك اتحفظ في تعليقات الحصة وبانتظار المراجعة.' : 'اتحفظ التفاعل.');
      return result.entry;
    } catch (e) { setError(learningError(e)); return undefined; } finally { setBusy(false); }
  };
  const activities = snapshot?.document.activities ?? [];
  const entries = snapshot?.entries ?? [];
  const answers = entries.filter(e => e.kind === 'answer');
  const answerFor = (a: LearningActivity) => answers.find(e => e.activityId === a.id);
  const due = (a: LearningActivity) => a.placement === 'end' ? ended : seconds >= a.seconds;
  const blocking = activities.find(a => a.kind === 'question' && a.required && due(a) && !answerFor(a));
  const blockId = blocking?.id;
  const timeUpdate = useCallback((time: number, length: number) => { setSeconds(Math.floor(time)); setDuration(length); }, []);
  useEffect(() => {
    if (!blockId) return;
    player.current?.pause();
    // Native provider controls may resume playback independently of our controls.
    const timer = window.setInterval(() => player.current?.pause(), 250);
    if (document.fullscreenElement) void document.exitFullscreen().catch(() => undefined);
    return () => window.clearInterval(timer);
  }, [blockId]);
  useEffect(() => {
    if (!ended || !snapshot || sentEnd.current) return;
    const pending = snapshot.document.activities.some(a => a.kind === 'question' && a.placement === 'end' && a.required && !snapshot.entries.some(e => e.kind === 'answer' && e.activityId === a.id));
    if (!pending && !snapshot.document.activities.some(a => a.kind === 'question' && a.placement === 'end')) { sentEnd.current = true; onEnded?.(); }
  }, [ended, snapshot, onEnded]);
  const finalQuestions = activities.filter(a => a.kind === 'question' && a.placement === 'end');
  const visible = activities.filter(a => a.kind !== 'concept' && a.id !== blockId &&
    (a.kind === 'question' ? due(a) && a.placement !== 'end' && !dismissed.has(a.id) :
      a.placement === 'end' ? ended : seconds >= a.seconds && seconds <= a.endSeconds));
  const showCard = (a: LearningActivity) => <LearningActivityCard key={a.id} activity={a} answer={answerFor(a)} explanation={explanations[a.id] || (answerFor(a) ? a.answer : '')}
    busy={busy} onAnswer={option => void record('answer', String(option), '', a.id, a.seconds)} onSeek={seek} />;
  const tools = snapshot?.document.tools;
  const showNotebook = !!tools && !snapshot?.stale && (tools.notes || tools.bookmarks || tools.understanding || tools.timeline || tools.askTeacher || tools.cards || tools.glossary || tools.experiments || tools.mastery || tools.review || tools.aiTutor);
  return <div className={`grid min-w-0 items-start gap-4 ${showNotebook ? '2xl:grid-cols-[minmax(0,1fr)_20rem]' : ''}`} dir="rtl">
    <div className="min-w-0 space-y-4">
    <div className="relative aspect-video overflow-hidden rounded-xl bg-black">
      <SecureVideoPlayer {...props} className={`${props.className ?? ""} isolate`} ref={player} onPlaybackTime={timeUpdate} enableChapterAids={!!tools?.chapterAids && !snapshot?.stale} reactionDensity={snapshot?.stale ? [] : snapshot?.density} onEnded={() => setEnded(true)} />
      {ended && finalQuestions.length > 0 && <div className="absolute inset-0 overflow-y-auto bg-[var(--admin-card)] p-4 text-[var(--admin-text)]">
        <h2 className="mb-4 text-xl font-bold">تحدّي ختام الفيديو</h2>
        <div className="space-y-4">{finalQuestions.filter(a => a.id !== blockId).map(showCard)}</div>
        <div className="mt-3 flex flex-wrap gap-2"><button className="admin-btn-ghost min-h-11" onClick={() => seek(0)}>العودة للفيديو</button>
        {!blocking && <button className="admin-btn-primary min-h-11" onClick={() => { if (!sentEnd.current) { sentEnd.current = true; onEnded?.(); } }}>إنهاء المراجعة والمتابعة</button>}</div>
      </div>}
      {blocking && <div className="absolute inset-0 overflow-y-auto bg-[var(--admin-card)] p-3" role="region" aria-label="سؤال مطلوب لاستكمال الفيديو">
        <p className="mb-2 font-bold text-[var(--admin-text)]">جاوب السؤال عشان تكمل</p>{showCard(blocking)}
        {error && <p role="alert" className="text-[var(--admin-danger)]">{error}</p>}
      </div>}
    </div>
    {error && <div role="alert" className="rounded-lg bg-[var(--admin-danger-10)] p-3 text-[var(--admin-danger)]">{error} <button className="min-h-11 underline" onClick={() => setRetry(r => r + 1)}>إعادة التحميل</button></div>}
    {snapshot?.stale && <p role="status" className="text-sm text-[var(--admin-muted)]">الفيديو اتغيّر؛ التفاعلات هتظهر بعد ما المدرس يراجع توقيتها ويعتمدها.</p>}
    {notice && <p role="status" className="text-sm text-[var(--admin-muted)]">{notice}</p>}
    {snapshot && !snapshot.stale && <>
      {visible.length > 0 && <div className="space-y-3">{visible.slice(0, 3).map(a => <div key={a.id}>{showCard(a)}
        {a.kind === 'question' && (!a.required || answerFor(a)) && <button className="admin-btn-ghost min-h-11" onClick={() => { setDismissed(previous => new Set([...previous, a.id])); if (a.required) player.current?.play(); }}>متابعة</button>}
      </div>)}</div>}
    </>}
    </div>
    {snapshot && showNotebook && <>
      <LearningNotebook snapshot={snapshot} videoId={props.lessonVideoId} seconds={seconds} duration={duration} busy={busy}
        onRecord={record} onSeek={seek} onRemove={async (entry: LearningEntry) => {
          try { await videoLearningService.remove(props.lessonVideoId, entry.id); setSnapshot(s => s ? { ...s, entries: s.entries.filter(e => e.id !== entry.id) } : s); }
          catch (e) { setError(learningError(e)); }
        }} />
    </>}
  </div>;
});
