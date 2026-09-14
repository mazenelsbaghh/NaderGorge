'use client';

import { useState } from 'react';
import { timeLabel, type LearningActivity, type LearningEntry } from '@/services/video-learning-service';

export function LearningActivityCard({ activity: a, answer, explanation, onAnswer, onSeek, busy = false, preview = false }: {
  activity: LearningActivity; answer?: LearningEntry; explanation?: string; onAnswer?: (option: number) => void;
  onSeek?: (seconds: number) => void; busy?: boolean; preview?: boolean;
}) {
  const [selected, setSelected] = useState<number | null>(null);
  const [revealed, setRevealed] = useState(false);
  const [value, setValue] = useState(a.minimum);
  const result = a.experiment === 'ratio' ? a.factor / value + a.offset : a.factor * (a.experiment === 'product' ? value * value : value) + a.offset;
  return <article className="min-w-0 space-y-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 text-[var(--admin-text)]">
    <div className="flex flex-wrap items-center justify-between gap-2">
      <h3 className="break-words text-lg font-bold">{a.title}</h3>
      <button type="button" disabled={!onSeek} onClick={() => onSeek?.(a.seconds)} className="min-h-11 px-2 text-sm text-[var(--admin-primary)]">{timeLabel(a.seconds)}</button>
    </div>
    {a.body && <p className="whitespace-pre-wrap break-words leading-7">{a.body}</p>}
    {a.kind === 'question' && <>
      <fieldset disabled={busy || !!answer} className="space-y-2"><legend className="sr-only">اختيارات الإجابة</legend>
        {a.options.map((option, i) => <label key={i} className="flex min-h-11 items-center gap-3 rounded-lg bg-[var(--admin-card-soft)] px-3 py-2">
          <input type="radio" name={`answer-${a.id}`} checked={answer ? Number(answer.text) === i : selected === i} onChange={() => setSelected(i)} className="h-5 w-5 shrink-0" /><span className="break-words">{option}</span>
        </label>)}
      </fieldset>
      {!answer && <button type="button" className="admin-btn-primary min-h-11" disabled={selected === null || busy} onClick={() => preview ? setRevealed(true) : selected !== null && onAnswer?.(selected)}>تأكيد الإجابة</button>}
      {(answer || (preview && revealed)) && <div role="status" className="space-y-2">
        <p className="font-bold">{(answer ? answer.correct : selected === a.correctOption) ? 'إجابة صحيحة 💡' : 'راجع تفسير النقطة دي'}</p>
        <p className="whitespace-pre-wrap">{explanation || (preview ? a.answer : '')}</p>
        {answer?.correct === false && onSeek && <button className="admin-btn-ghost min-h-11" onClick={() => onSeek(Math.max(0, a.seconds - 15))}>ارجع للشرح قبل السؤال</button>}
      </div>}
    </>}
    {(a.kind === 'card' || a.kind === 'term') && <>
      <button className="admin-btn-ghost min-h-11" aria-expanded={revealed} onClick={() => setRevealed(v => !v)}>{revealed ? 'إخفاء' : a.kind === 'term' ? 'المعنى والمثال' : 'كشف الإجابة'}</button>
      {revealed && <p className="whitespace-pre-wrap leading-7">{a.answer}</p>}
    </>}
    {a.kind === 'experiment' && <div className="space-y-3">
      <label className="block">قيمة س: {value.toLocaleString('ar-EG', { maximumFractionDigits: 3 })}
        <input aria-label="غيّر قيمة س" type="range" min={a.minimum} max={a.maximum} step={(a.maximum - a.minimum) / 100} value={value} onChange={e => setValue(Number(e.target.value))} className="mt-3 min-h-11 w-full accent-[var(--admin-primary)]" />
      </label>
      <output className="block rounded-lg bg-[var(--admin-card-soft)] p-4 text-xl font-bold">النتيجة: {Number.isFinite(result) ? result.toLocaleString('ar-EG', { maximumFractionDigits: 3 }) : 'القيمة غير معرّفة'}</output>
    </div>}
  </article>;
}
