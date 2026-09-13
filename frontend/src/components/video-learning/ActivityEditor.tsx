'use client';

import { useState } from 'react';
import { learningCenterService } from '@/services/learning-center-service';
import { learningError, type LearningActivity } from '@/services/video-learning-service';

export function ActivityEditor({ activity: a, lessonId, onChange, onRemove }: {
  activity: LearningActivity; lessonId: string; onChange: (activity: LearningActivity) => void; onRemove: () => void;
}) {
  const [bank, setBank] = useState<Awaited<ReturnType<typeof learningCenterService.questions>> | null>(null);
  const [error, setError] = useState('');
  const change = (patch: Partial<LearningActivity>) => onChange({ ...a, ...patch });
  return <fieldset className="space-y-4 rounded-xl border border-[var(--admin-border)] p-4">
    <legend className="px-2 font-bold">{a.title || 'نشاط جديد'}</legend>
    <div className="grid gap-4 sm:grid-cols-3">
      <label>نوع النشاط<select className="admin-input mt-1" value={a.kind} onChange={e => change({ kind: e.target.value as LearningActivity['kind'] })}>
        <option value="question">سؤال</option><option value="card">كارت مراجعة</option><option value="term">مصطلح</option>
        <option value="experiment">تجربة صغيرة</option><option value="concept">مفهوم للإتقان</option>
      </select></label>
      <label>الظهور<select className="admin-input mt-1" value={a.placement} onChange={e => change({ placement: e.target.value as LearningActivity['placement'] })}>
        <option value="moment">أثناء الفيديو</option><option value="chapter">بعد نهاية فصل</option><option value="end">ختام الفيديو</option>
      </select></label>
      <label>المفهوم<input maxLength={160} className="admin-input mt-1" value={a.concept} onChange={e => change({ concept: e.target.value })} /></label>
      <label>التوقيت بالثواني<input type="number" min={0} max={86400} className="admin-input mt-1" value={a.seconds} onChange={e => change({ seconds: Number(e.target.value), endSeconds: Math.max(a.endSeconds, Number(e.target.value)) })} /></label>
      <label>حتى الثانية<input type="number" min={a.seconds} max={86400} className="admin-input mt-1" value={a.endSeconds} onChange={e => change({ endSeconds: Number(e.target.value) })} /></label>
    </div>
    <label className="block">العنوان أو نص السؤال<input maxLength={300} className="admin-input mt-1" value={a.title} onChange={e => change({ title: e.target.value })} /></label>
    <label className="block">الشرح أو التعليمات<textarea maxLength={2000} className="admin-input mt-1" rows={2} value={a.body} onChange={e => change({ body: e.target.value })} /></label>
    {a.kind === 'question' && <>
      <div className="space-y-2">{a.options.map((option, index) => <div key={index} className="flex items-center gap-2">
        <input aria-label={`الاختيار ${index + 1} هو الصحيح`} type="radio" name={`correct-${a.id}`} checked={a.correctOption === index} onChange={() => change({ correctOption: index })} className="h-5 w-5" />
        <input aria-label={`الاختيار ${index + 1}`} maxLength={500} value={option} className="admin-input" onChange={e => change({ options: a.options.map((o, i) => i === index ? e.target.value : o) })} />
      </div>)}</div>
      <div className="flex flex-wrap gap-2">
        <button type="button" className="admin-btn-ghost min-h-11" disabled={a.options.length >= 6} onClick={() => change({ options: [...a.options, ''] })}>إضافة اختيار</button>
        <button type="button" className="admin-btn-ghost min-h-11" disabled={a.options.length <= 2} onClick={() => change({ options: a.options.slice(0, -1), correctOption: Math.min(a.correctOption ?? 0, a.options.length - 2) })}>حذف آخر اختيار</button>
        <button type="button" className="admin-btn-ghost min-h-11" onClick={async () => {
          try { setBank(await learningCenterService.questions({ lessonId, page: 1, pageSize: 100 })); } catch (e) { setError(learningError(e)); }
        }}>اختيار من بنك أسئلة الحصة</button>
      </div>
      {bank && <label className="block">سؤال من البنك<select className="admin-input mt-1" value="" onChange={e => {
        const question = bank.items.find(q => q.id === e.target.value); if (!question) return;
        const options = question.options.filter(o => o.text.trim());
        change({ title: question.text, concept: question.concept, options: options.map(o => o.text),
          correctOption: options.findIndex(o => o.isCorrect), answer: question.correction ?? '', questionBankId: question.id }); setBank(null);
      }}><option value="">اختار سؤالًا ({bank.totalCount})</option>{bank.items.filter(q => q.type === 0).map(q => <option key={q.id} value={q.id}>{q.text}</option>)}</select></label>}
      <label className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={a.required} onChange={e => change({ required: e.target.checked })} />إيقاف الفيديو حتى الإجابة، مع إظهار التفسير بعدها</label>
    </>}
    {(a.kind === 'question' || a.kind === 'card' || a.kind === 'term') && <label className="block">{a.kind === 'question' ? 'تفسير الإجابة بعد الحل' : 'الإجابة أو المعنى والمثال'}<textarea maxLength={2000} className="admin-input mt-1" rows={3} value={a.answer} onChange={e => change({ answer: e.target.value })} /></label>}
    {a.kind === 'experiment' && <div className="grid gap-3 sm:grid-cols-3">
      <label>قالب التجربة<select className="admin-input mt-1" value={a.experiment} onChange={e => change({ experiment: e.target.value as LearningActivity['experiment'] })}>
        <option value="linear">النتيجة = المعامل × س + ثابت</option><option value="product">النتيجة = المعامل × س² + ثابت</option><option value="ratio">النتيجة = المعامل ÷ س + ثابت</option>
      </select></label>
      {(['factor', 'offset', 'minimum', 'maximum'] as const).map((key, i) => <label key={key}>{['المعامل', 'الثابت', 'أقل قيمة لس', 'أكبر قيمة لس'][i]}<input type="number" step="any" min={-100000} max={100000} className="admin-input mt-1" value={a[key]} onChange={e => change({ [key]: Number(e.target.value) })} /></label>)}
    </div>}
    {error && <p role="alert" className="text-[var(--admin-danger)]">{error}</p>}
    <button type="button" className="admin-btn-ghost min-h-11 text-[var(--admin-danger)]" onClick={onRemove}>حذف النشاط من المسودة</button>
  </fieldset>;
}
