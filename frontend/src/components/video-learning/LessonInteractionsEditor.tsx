'use client';

import { useEffect, useRef, useState } from 'react';
import SecureVideoPlayer, { type SecureVideoPlayerRef } from '@/components/video/SecureVideoPlayer';
import { ActivityEditor } from './ActivityEditor';
import { LearningActivityCard } from './LearningActivityCard';
import { learningError, newActivity, timeLabel, toolLabels, videoLearningService,
  type LearningReport, type LearningSnapshot, type LearningTools } from '@/services/video-learning-service';

type Video = { id: string; title: string; chapters?: { title: string; startTime: number; endTime: number }[] | null };
export function LessonInteractionsEditor({ lessonId, videos }: { lessonId: string; videos: Video[] }) {
  const [videoId, setVideoId] = useState(videos[0]?.id ?? '');
  const [snapshot, setSnapshot] = useState<LearningSnapshot | null>(null);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [retry, setRetry] = useState(0);
  const [report, setReport] = useState<LearningReport | null>(null);
  const [preview, setPreview] = useState(false);
  const [showVideo, setShowVideo] = useState(false);
  const [authorTime, setAuthorTime] = useState(0);
  const player = useRef<SecureVideoPlayerRef>(null);
  useEffect(() => {
    if (!videoId) return;
    const abort = new AbortController();
    setSnapshot(null); setError(''); setReport(null); setDirty(false);
    videoLearningService.read(videoId, true, abort.signal).then(setSnapshot).catch(e => { if (!abort.signal.aborted) setError(learningError(e)); });
    return () => abort.abort();
  }, [videoId, retry]);
  const video = videos.find(v => v.id === videoId);
  const update = (document: LearningSnapshot['document']) => { if (snapshot) setSnapshot({ ...snapshot, document }); setDirty(true); setNotice(''); };
  const run = async (action: () => Promise<void>) => {
    setBusy(true); setError(''); setNotice('');
    try { await action(); } catch (e) { setError(learningError(e)); } finally { setBusy(false); }
  };
  if (!videos.length) return <p className="p-6">أضف فيديو للحصة أولًا، ثم اضبط أدوات التفاعل هنا.</p>;
  return <section className="space-y-6" dir="rtl" aria-label="التفاعل والمراجعة">
    <p className="text-sm text-[var(--admin-muted)]">كل الأدوات مقفولة افتراضيًا للحصص الحالية والجديدة. الأدمن فقط يقدر يفعّلها ويعتمد إعداداتها لكل فيديو.</p>
    <div className="flex flex-wrap items-end justify-between gap-4">
      <label className="min-w-0 flex-1">فيديو الحصة<select disabled={busy || dirty} className="admin-input mt-2" value={videoId} onChange={e => setVideoId(e.target.value)}>{videos.map(v => <option key={v.id} value={v.id}>{v.title}</option>)}</select></label>
      <button className="admin-btn-ghost min-h-11" disabled={busy} onClick={() => setRetry(r => r + 1)}>إعادة التحميل وإلغاء المسودة</button>
    </div>
    {error && <p role="alert" className="rounded-lg bg-[var(--admin-danger-10)] p-4 text-[var(--admin-danger)]">{error}</p>}
    {notice && <p role="status" className="text-[var(--admin-success)]">{notice}</p>}
    {!snapshot && !error && <p role="status">جارٍ تحميل إعدادات الفيديو…</p>}
    {snapshot && <>
      <button type="button" className="admin-btn-ghost min-h-11" aria-expanded={showVideo} onClick={() => setShowVideo(value => !value)}>{showVideo ? 'إخفاء معاينة الفيديو' : 'ضبط التوقيت من الفيديو'}</button>
      {showVideo && <div className="max-w-4xl space-y-3"><SecureVideoPlayer key={videoId} ref={player} lessonVideoId={videoId} onPlaybackTime={time => setAuthorTime(Math.floor(time))} />
        <p>اللحظة الحالية: {timeLabel(authorTime)}</p>
        <div className="flex flex-wrap gap-2">{snapshot.document.activities.map(a => <button key={a.id} className="admin-btn-ghost min-h-11" onClick={() => player.current?.seekTo(a.seconds)}>{timeLabel(a.seconds)} · {a.title || 'نشاط'}</button>)}</div>
      </div>}
      {snapshot.stale && <p role="alert" className="rounded-lg bg-[var(--admin-warning-10)] p-4">مصدر الفيديو اتغير. الأنشطة متوقفة للطلاب لحد ما تراجع التوقيتات وتحفظها.</p>}
      <fieldset disabled={busy} className="space-y-5">
        <legend className="mb-3 text-xl font-bold">الأدوات المتاحة للطالب</legend>
        <div className="grid gap-x-8 gap-y-1 sm:grid-cols-2">{Object.entries(toolLabels).map(([key, label]) => <label key={key} className="flex min-h-11 items-center gap-3 border-b border-[var(--admin-border)] py-2">
          <input className="h-5 w-5 shrink-0" type="checkbox" checked={snapshot.document.tools[key as Exclude<keyof LearningTools, 'aiDailyLimit'>]} onChange={e => update({ ...snapshot.document, tools: { ...snapshot.document.tools, [key]: e.target.checked } })} />{label}
        </label>)}</div>
        {snapshot.document.tools.aiTutor && <label className="block max-w-sm">حد طلبات AI للطالب يوميًا في الفيديو<input type="number" min={1} max={50} className="admin-input mt-1" value={snapshot.document.tools.aiDailyLimit} onChange={e => update({ ...snapshot.document, tools: { ...snapshot.document.tools, aiDailyLimit: Number(e.target.value) } })} /></label>}
      </fieldset>
      <div className="flex flex-wrap gap-3">
        <button disabled={busy || snapshot.document.activities.length >= 150} className="admin-btn-ghost min-h-11" onClick={() => update({ ...snapshot.document, activities: [...snapshot.document.activities, newActivity(authorTime)] })}>إضافة نشاط {showVideo ? `عند ${timeLabel(authorTime)}` : ''}</button>
        {video?.chapters?.length ? <label>إضافة تحدي عند نهاية فصل<select disabled={busy} className="admin-input" value="" onChange={e => {
          const chapter = video.chapters?.[Number(e.target.value)]; if (!chapter) return;
          update({ ...snapshot.document, activities: [...snapshot.document.activities, { ...newActivity(chapter.endTime), placement: 'chapter', concept: chapter.title }] });
        }}><option value="">اختار الفصل</option>{video.chapters.map((c, i) => <option key={i} value={i}>{c.title} · {timeLabel(c.endTime)}</option>)}</select></label> : null}
        <button disabled={busy} className="admin-btn-ghost min-h-11" onClick={() => setPreview(p => !p)}>{preview ? 'العودة للتحرير' : 'معاينة الأنشطة'}</button>
        {snapshot.document.tools.aiAuthoring && <button disabled={busy || dirty || snapshot.stale} className="admin-btn-ghost min-h-11" onClick={() => run(async () => {
          const generated = await videoLearningService.ai(videoId, snapshot.version, 'author', 0);
          update({ ...snapshot.document, activities: [...snapshot.document.activities, ...generated.activities] });
          setNotice('اتجهزت مسودات مقترحة. راجع المحتوى والتوقيتات وفعّل الأدوات المناسبة قبل اعتمادها.');
        })}>توليد مسودات من تحليل الفيديو</button>}
      </div>
      {dirty && <p className="text-sm text-[var(--admin-muted)]">عندك تعديلات لم تُنشر. الحفظ يعتمد الأنشطة ويبدأ سجل إجابات جديد للأسئلة.</p>}
      {!snapshot.document.activities.length && <p className="py-6 text-[var(--admin-muted)]">أضف سؤالًا أو كارتًا أو مصطلحًا. أدوات الملاحظات والتفاعل السريع تعمل بدون تجهيز أنشطة.</p>}
      <fieldset disabled={busy} className="space-y-5">{snapshot.document.activities.map(a => preview
        ? <LearningActivityCard key={a.id} activity={a} preview />
        : <ActivityEditor key={a.id} activity={a} lessonId={lessonId} onChange={value => update({ ...snapshot.document, activities: snapshot.document.activities.map(item => item.id === a.id ? value : item) })} onRemove={() => update({ ...snapshot.document, activities: snapshot.document.activities.filter(item => item.id !== a.id) })} />)}</fieldset>
      <div className="flex flex-wrap gap-3 border-t border-[var(--admin-border)] pt-5">
        <button disabled={busy} className="admin-btn-primary min-h-11" onClick={() => run(async () => {
          const saved = await videoLearningService.save(videoId, snapshot); setSnapshot(saved); setDirty(false); setReport(null); setNotice('تم اعتماد إعدادات التفاعل والأنشطة.');
        })}>{busy ? 'جارٍ تنفيذ الطلب…' : 'حفظ واعتماد التفاعلات'}</button>
        <button disabled={busy} className="admin-btn-ghost min-h-11" onClick={() => run(async () => setReport(await videoLearningService.report(videoId)))}>عرض نتائج الطلاب</button>
      </div>
      {report && <div className="space-y-4">
        <h3 className="text-xl font-bold">نتائج التفاعل</h3>
        {!report.answers.length && !report.density.length && <p>لا توجد إجابات أو تفاعلات بعد.</p>}
        {report.answers.map(a => <p key={a.activityId}>{report.activities.find(item => item.id === a.activityId)?.title}، {a.correct} إجابة صحيحة من {a.students} طالبًا</p>)}
        {report.density.map(d => <p key={d.seconds}>{timeLabel(d.seconds)}: فهمت {d.understood} · مش فاهم {d.confused} · محتاج مثال {d.example}</p>)}
        <h4 className="font-bold">أسئلة مرتبطة باللحظة</h4>
        {report.questions.map(q => <div key={q.id} className="border-b border-[var(--admin-border)] py-3"><p className="text-sm">{q.studentName} · {timeLabel(q.seconds)}</p><p className="whitespace-pre-wrap">{q.text}</p></div>)}
        <p className="text-sm text-[var(--admin-muted)]">مراجعة الأسئلة والرد عليها من تبويب تعليقات الحصة. ملاحظات الطالب ونجومه خاصة به.</p>
      </div>}
    </>}
  </section>;
}
