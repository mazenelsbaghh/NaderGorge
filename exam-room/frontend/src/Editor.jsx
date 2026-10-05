import React, {useEffect, useState} from 'react';
import {number, toast} from '../../web/assets/common.js';

const emptyQuestion = kind => ({kind, text: '', points: 2, options: ['', '', '', ''], correct: 0, modelAnswer: ''});
const initial = {title: '', instructions: '', minutes: 15, timerMode: 'shared', allowLate: true,
  shuffleOptions: true, shuffle: false, questionCount: 1, questions: [emptyQuestion('mcq')]};

export default function Editor({config, onSave, onCancel, onDirty}) {
  const [draft, setDraft] = useState(() => structuredClone(config || initial));
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);
  useEffect(() => {onDirty(dirty);}, [dirty, onDirty]);
  const edit = update => {setDraft(previous => update(structuredClone(previous))); setDirty(true);};
  const field = (key, value) => edit(next => ({...next, [key]: value}));
  const questionField = (index, key, value) => edit(next => {next.questions[index][key] = value; return next;});
  const optionField = (index, choice, value) => edit(next => {next.questions[index].options[choice] = value; return next;});
  const add = kind => {
    if (draft.questions.length >= 100) {toast('الحد الأقصى ١٠٠ سؤال', true); return;}
    edit(next => {const allShown = next.questionCount === next.questions.length; next.questions.push(emptyQuestion(kind));
      if (allShown) next.questionCount++; return next;});
  };
  const remove = index => edit(next => {next.questions.splice(index, 1);
    next.questionCount = Math.min(next.questionCount, next.questions.length); return next;});
  const submit = async event => {
    event.preventDefault(); setSaving(true);
    try {await onSave(draft); setDirty(false);}
    catch (error) {toast(error.message, true);}
    finally {setSaving(false);}
  };
  return <>
    <div className="page-heading"><div><span className="context-label">تجهيز الامتحان</span>
      <h1>{config ? 'تعديل الامتحان' : 'امتحان جديد'}</h1><p>سمِّ الامتحان وجهّز أسئلته هنا، ثم اختره مع الحصة في قاعة السنتر.</p></div>
      <button className="button secondary" onClick={onCancel}>رجوع للمكتبة</button></div>
    <form id="exam-editor" onSubmit={submit}>
      <section className="panel editor-basics"><h2>تفاصيل الامتحان</h2>
        <label>اسم الامتحان<input name="title" required maxLength="150" value={draft.title}
          onChange={event => field('title', event.target.value)} placeholder="مثلًا: مراجعة الفصل الأول" /></label>
        <label>تعليمات للطلاب <span className="muted">اختياري</span><textarea name="instructions" rows="2"
          maxLength="2000" value={draft.instructions} onChange={event => field('instructions', event.target.value)} /></label>
        <div className="form-grid">
          <label>المدة بالدقائق<input name="minutes" type="number" min="1" max="240" required value={draft.minutes}
            onChange={event => field('minutes', Number(event.target.value))} /></label>
          <label>عدد الأسئلة اللي تظهر لكل طالب<input name="questionCount" type="number" min="1" max={draft.questions.length}
            required value={draft.questionCount} onChange={event => field('questionCount', Number(event.target.value))} /></label>
          <label>نظام الوقت<select name="timerMode" value={draft.timerMode} onChange={event => field('timerMode', event.target.value)}>
            <option value="shared">نهاية موحدة للجميع</option><option value="individual">مدة كاملة لكل طالب</option></select></label>
        </div>
        <div className="question-bank-summary" aria-live="polite"><strong>بنك الأسئلة: {number(draft.questions.length)} سؤال · يظهر لكل طالب: {number(draft.questionCount)} سؤال</strong><p>{draft.questionCount<draft.questions.length?'تُسحب الأسئلة عشوائيًا لكل طالب من البنك. يمكن أن تتكرر بعض الأسئلة بين الطلاب، وأسئلة الطالب نفسه تظل محفوظة عند الخروج والعودة.':'كل طالب يرى جميع أسئلة البنك. قلل العدد المعروض لو عايز سحبًا عشوائيًا؛ مثلًا أضف ٢٠ سؤالًا واختر عرض ١٠.'}</p></div>
        <p className="help">في النهاية الموحدة، الطالب المتأخر يأخذ الوقت المتبقي. لو عرضت جزءًا من الأسئلة، تُسحب عشوائيًا ويجب أن تتساوى درجاتها.</p>
        <div className="check-row"><label className="check-label"><input type="checkbox" checked={draft.allowLate}
          onChange={event => field('allowLate', event.target.checked)} />السماح بالدخول بعد البداية</label>
          <label className="check-label"><input type="checkbox" checked={draft.shuffle}
            onChange={event => field('shuffle', event.target.checked)} />تغيير ترتيب الأسئلة بين الطلاب</label><label className="check-label"><input type="checkbox" checked={draft.shuffleOptions!==false} onChange={e=>field('shuffleOptions',e.target.checked)}/>تغيير ترتيب الاختيارات بين الطلاب</label></div>
      </section>
      <div className="section-top questions-heading"><h2>بنك الأسئلة <span className="muted">({number(draft.questions.length)})</span></h2>
        <span className="muted">الإجابات النموذجية تظهر للإدارة فقط</span></div>
      <div id="question-list">{draft.questions.map((question, index) => <section className="question-editor" key={question.id || index}>
        <div className="section-top"><h3>السؤال {number(index + 1)} <span className="badge quiet">{question.kind === 'mcq' ? 'اختيار من متعدد' : 'مقالي'}</span></h3>
          <button type="button" className="text-button danger-text" disabled={draft.questions.length === 1} onClick={() => remove(index)}>حذف السؤال</button></div>
        <div className="question-fields"><label>نص السؤال<textarea required rows="2" maxLength="5000" value={question.text}
          onChange={event => questionField(index, 'text', event.target.value)} placeholder="اكتب السؤال هنا…" /></label>
          <label>الدرجة<input type="number" min="1" max="100" required value={question.points}
            onChange={event => questionField(index, 'points', Number(event.target.value))} /></label></div>
        {question.kind === 'mcq' ? <fieldset className="options-editor"><legend>الاختيارات · حدد الإجابة الصحيحة</legend>
          {question.options.map((option, choice) => <label className="option-editor" key={choice}>
            <input type="radio" name={`correct-${index}`} checked={question.correct === choice}
              onChange={() => questionField(index, 'correct', choice)} aria-label={`الاختيار الصحيح ${choice + 1}`} />
            <input className="option-text" value={option} maxLength="1000" required
              onChange={event => optionField(index, choice, event.target.value)}
              aria-label={`نص الاختيار ${choice + 1}`} placeholder={`الاختيار ${number(choice + 1)}`} />
          </label>)}</fieldset> : <label>الإجابة النموذجية ومعايير التصحيح<textarea rows="3" maxLength="10000"
            required value={question.modelAnswer} onChange={event => questionField(index, 'modelAnswer', event.target.value)}
            placeholder="اكتب النقاط التي تُمنح عليها الدرجة…" /></label>}
      </section>)}</div>
      <div className="add-questions"><button className="button secondary" type="button" onClick={() => add('mcq')}>＋ سؤال اختيارات</button>
        <button className="button secondary" type="button" onClick={() => add('essay')}>＋ سؤال مقالي</button></div>
      <div className="editor-footer"><p>الطلاب لن يروا هذا الامتحان حتى تختاره مع حصة وتفتح القاعة.</p>
        <button className="button" type="submit" disabled={saving}>{saving ? 'جارٍ الحفظ…' : 'حفظ الامتحان'}</button></div>
    </form>
  </>;
}
