'use client';

import { useState } from 'react';
import { LearningActivityCard } from './LearningActivityCard';
import { learningError, timeLabel, videoLearningService, type LearningEntry, type LearningSnapshot } from '@/services/video-learning-service';

export function LearningNotebook({ snapshot, videoId, seconds, duration, busy, onRecord, onSeek, onRemove }: {
  snapshot: LearningSnapshot; videoId: string; seconds: number; duration: number; busy: boolean;
  onRecord: (kind: string, text: string, title?: string, activityId?: string, at?: number) => Promise<LearningEntry | undefined>;
  onSeek: (seconds: number) => void; onRemove: (entry: LearningEntry) => Promise<void>;
}) {
  const { tools, activities } = snapshot.document;
  const [tab, setTab] = useState('notes');
  const [text, setText] = useState('');
  const [title, setTitle] = useState('');
  const [aiBusy, setAiBusy] = useState(false);
  const [reply, setReply] = useState('');
  const [teacherReplies, setTeacherReplies] = useState<Record<string, { id: string; body: string; author: string }[]>>({});
  const [error, setError] = useState('');
  const [reviewIndex, setReviewIndex] = useState(0);
  const latestUnderstanding = [...snapshot.entries].filter(e => e.kind === 'understanding').filter((e, i, all) => all.findIndex(other => Math.floor(other.seconds / 15) === Math.floor(e.seconds / 15)) === i);
  const difficult = [...latestUnderstanding.filter(e => e.text !== 'understood'), ...snapshot.entries.filter(e => e.kind === 'answer' && e.correct === false)]
    .sort((a, b) => a.seconds - b.seconds).filter((e, i, all) => !i || e.seconds !== all[i - 1].seconds);
  const sections = [
    ...(tools.notes || tools.bookmarks ? [{ id: 'notes', label: 'دفتر الطالب' }] : []),
    ...(tools.askTeacher ? [{ id: 'ask', label: 'اسأل المدرس' }] : []),
    ...(tools.cards || tools.glossary || tools.experiments ? [{ id: 'aids', label: 'مساعدات الحصة' }] : []),
    ...(tools.mastery ? [{ id: 'mastery', label: 'أتقنتها' }] : []),
    ...(tools.aiTutor ? [{ id: 'ai', label: 'ساعدني أفهم' }] : []),
  ];
  const current = sections.some(s => s.id === tab) ? tab : sections[0]?.id;
  const askAi = async (mode: string) => {
    setAiBusy(true); setError(''); setReply('');
    try { setReply((await videoLearningService.ai(videoId, snapshot.version, mode, seconds, text)).text); }
    catch (e) { setError(learningError(e)); } finally { setAiBusy(false); }
  };
  const saveText = async (kind: string) => { const saved = await onRecord(kind, text, title); if (saved) { setText(''); setTitle(''); } };
  return <section className="min-w-0 space-y-4 text-[var(--admin-text)]" aria-label="أدوات التعلم">
    {(tools.understanding || tools.timeline) && <div className="flex flex-wrap items-center gap-2">
      <span className="me-1 text-sm">عند {timeLabel(seconds)}</span>
      {[['understood', '💡 فهمت'], ['example', 'محتاج مثال'], ['confused', '🤔 مش فاهم']].map(([value, label]) => <button key={value} disabled={busy} className="admin-btn-ghost min-h-11" onClick={() => void onRecord('understanding', value)}>{label}</button>)}
    </div>}
    {tools.timeline && snapshot.density.length > 0 && <div>
      <p className="mb-2 text-sm">تفاعلات الطلاب على الفيديو، اضغط للانتقال</p>
      <div className="relative h-11 overflow-hidden rounded-lg bg-[var(--admin-card-soft)]" dir="ltr" aria-label="كثافة تفاعلات الفيديو">
        {snapshot.density.map(d => <button key={d.seconds} style={{ left: `${Math.min(97, 100 * d.seconds / Math.max(duration, seconds, 1))}%`, opacity: Math.min(1, 0.3 + (d.understood + d.confused + d.example) / 10) }}
          className={`absolute inset-y-0 min-w-3 ${d.confused > d.understood ? 'bg-[var(--admin-warning)]' : 'bg-[var(--admin-primary)]'}`}
          aria-label={`${timeLabel(d.seconds)}، فهمت ${d.understood}، مش فاهم ${d.confused}، محتاج مثال ${d.example}`} title={`${timeLabel(d.seconds)} · 💡 ${d.understood} · 🤔 ${d.confused} · مثال ${d.example}`} onClick={() => onSeek(d.seconds)} />)}
      </div>
    </div>}
    {tools.review && <button disabled={!difficult.length} className="admin-btn-ghost min-h-11" onClick={() => {
      onSeek(Math.max(0, difficult[reviewIndex % difficult.length].seconds - 10)); setReviewIndex(i => i + 1);
    }}>راجع أجزائي الصعبة ({difficult.length}) {difficult.length > 0 ? `· التالي ${(reviewIndex % difficult.length) + 1}` : ''}</button>}
    {sections.length > 0 && <>
      <nav aria-label="لوحة التعلم" className="flex flex-wrap gap-1 border-b border-[var(--admin-border)]">
        {sections.map(s => <button key={s.id} aria-current={current === s.id ? 'page' : undefined} onClick={() => { setTab(s.id); setError(''); }} className={`${current === s.id ? 'admin-btn-primary' : 'admin-btn-ghost'} min-h-11`}>{s.label}</button>)}
      </nav>
      <div className="max-h-[32rem] space-y-4 overflow-y-auto p-1">
        {current === 'notes' && <>
          <p className="text-sm text-[var(--admin-muted)]">ملاحظاتك ونجومك خاصة بك. الحفظ عند {timeLabel(seconds)}.</p>
          <label className="block">عنوان اللحظة<input maxLength={300} className="admin-input mt-1" value={title} onChange={e => setTitle(e.target.value)} placeholder="مثلًا: قانون مهم" /></label>
          {tools.notes && <label className="block">ملاحظتي<textarea maxLength={1800} className="admin-input mt-1" value={text} onChange={e => setText(e.target.value)} rows={3} /></label>}
          <div className="flex flex-wrap gap-2">
            {tools.notes && <button disabled={busy || !text.trim()} className="admin-btn-primary min-h-11" onClick={() => void saveText('note')}>حفظ الملاحظة</button>}
            {tools.bookmarks && <button disabled={busy || !title.trim()} className="admin-btn-ghost min-h-11" onClick={() => void saveText('bookmark')}>☆ حفظ لحظة مهمة</button>}
            {tools.aiTutor && tools.notes && <button disabled={aiBusy} className="admin-btn-ghost min-h-11" onClick={() => void askAi('note')}>اقترح ملاحظة من الشرح</button>}
          </div>
          {reply && <div className="space-y-2"><p className="whitespace-pre-wrap leading-7">{reply}</p><button className="admin-btn-ghost min-h-11" onClick={() => { setText(reply.slice(0, 1800)); setReply(''); }}>نقل المقترح للملاحظة</button></div>}
          {snapshot.entries.filter(e => e.kind === 'note' || e.kind === 'bookmark').map(e => <div key={e.id} className="border-b border-[var(--admin-border)] py-3">
            <button className="min-h-11 text-start font-bold" onClick={() => onSeek(e.seconds)}>{e.kind === 'bookmark' ? '☆ ' : ''}{e.title || 'ملاحظة'} · {timeLabel(e.seconds)}</button>
            <p className="whitespace-pre-wrap break-words">{e.text}</p><button className="min-h-11 text-sm text-[var(--admin-danger)]" onClick={() => void onRemove(e)}>حذف</button>
          </div>)}
        </>}
        {current === 'ask' && <>
          <p>سؤالك مرتبط باللحظة {timeLabel(seconds)} ويظهر للمدرس في تعليقات الحصة للمراجعة.</p>
          <label className="block">سؤالي<textarea maxLength={1800} rows={3} className="admin-input mt-1" value={text} onChange={e => setText(e.target.value)} /></label>
          <button disabled={busy || !text.trim()} className="admin-btn-primary min-h-11" onClick={() => void saveText('ask')}>إرسال السؤال للمدرس</button>
          {snapshot.entries.filter(e => e.kind === 'ask').map(e => <div key={e.id} className="border-b border-[var(--admin-border)] py-3"><p><button className="min-h-11 text-[var(--admin-primary)]" onClick={() => onSeek(e.seconds)}>{timeLabel(e.seconds)}</button> · {e.text}</p>
            <button className="admin-btn-ghost min-h-11" onClick={async () => { try { const rows = await videoLearningService.replies(videoId, e.id); setTeacherReplies(previous => ({ ...previous, [e.id]: rows })); } catch (failure) { setError(learningError(failure)); } }}>عرض الردود</button>
            {teacherReplies[e.id]?.length === 0 && <p className="text-sm">لا توجد ردود معتمدة بعد.</p>}
            {teacherReplies[e.id]?.map(r => <p key={r.id} className="whitespace-pre-wrap py-2"><strong>{r.author}: </strong>{r.body}</p>)}
          </div>)}
        </>}
        {current === 'aids' && <>
          {activities.filter(a => ['card', 'term', 'experiment'].includes(a.kind)).map(a => <LearningActivityCard key={a.id} activity={a} onSeek={onSeek} />)}
          {!activities.some(a => ['card', 'term', 'experiment'].includes(a.kind)) && <p>المدرس لم يضف مساعدات لهذا الفيديو بعد.</p>}
        </>}
        {current === 'mastery' && <>{activities.filter(a => a.kind === 'concept').map(a => {
          const state = snapshot.entries.find(e => e.kind === 'mastery' && e.activityId === a.id)?.text;
          return <div key={a.id} className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--admin-border)] py-3"><p>{a.title}</p>
            <div className="flex gap-2">{[['understood', 'فاهمها'], ['review', 'هراجعها']].map(([value, label]) => <button key={value} aria-pressed={state === value} disabled={busy} className={`${state === value ? 'admin-btn-primary' : 'admin-btn-ghost'} min-h-11`} onClick={() => void onRecord('mastery', value, '', a.id, a.seconds)}>{label}</button>)}</div>
          </div>;
        })}</>}
        {current === 'ai' && <>
          <p className="text-sm text-[var(--admin-muted)]">مساعدة بالذكاء الاصطناعي من ملخص الفصل الحالي. تقدر ترجع للمدرس لو محتاج توضيح.</p>
          <label className="block">عايز تفهم إيه؟<textarea maxLength={1000} rows={2} className="admin-input mt-1" value={text} onChange={e => setText(e.target.value)} /></label>
          <div className="flex flex-wrap gap-2">{[['simplify', 'بسّطها لي'], ['example', 'هات مثال'], ['quiz', 'اختبرني'], ['foundation', 'ارجع للأساس'], ['ask', 'جاوب سؤالي']].map(([mode, label]) => <button key={mode} disabled={aiBusy || (mode === 'ask' && !text.trim())} className="admin-btn-ghost min-h-11" onClick={() => void askAi(mode)}>{label}</button>)}</div>
          {reply && <p role="status" className="whitespace-pre-wrap break-words leading-8">{reply}</p>}
          {tools.askTeacher && <button className="admin-btn-ghost min-h-11" onClick={() => setTab('ask')}>حوّل سؤالي للمدرس</button>}
        </>}
        {aiBusy && <p role="status">جارٍ تجهيز المساعدة…</p>}
        {error && <p role="alert" className="text-[var(--admin-danger)]">{error}</p>}
      </div>
    </>}
  </section>;
}
