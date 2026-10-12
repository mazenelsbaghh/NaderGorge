import React, {useEffect, useState} from 'react';
import {number, toast} from '../../web/assets/common.js';

const emptyQuestion = kind => ({kind, text: '', points: 2, options: ['', '', '', ''], correct: 0, modelAnswer: ''});
const initial = {title: '', instructions: '', minutes: 15, timerMode: 'shared', allowLate: true,
  shuffleOptions: true, shuffle: false, questionCount: 1, mcqCount: 1, essayCount: 0, questions: [emptyQuestion('mcq')]};

const bankCount = (draft, kind) => draft.questions.filter(question => question.kind === kind).length;
const editorDraft = config => {
  const draft = structuredClone(config || initial);
  if (draft.mcqCount == null && draft.questionCount === draft.questions.length) {
    draft.mcqCount = bankCount(draft, 'mcq');
    draft.essayCount = bankCount(draft, 'essay');
  }
  return draft;
};

export default function Editor({config, onSave, onCancel, onDirty}) {
  const [draft, setDraft] = useState(() => editorDraft(config));
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);
  useEffect(() => {onDirty(dirty);}, [dirty, onDirty]);
  const edit = update => {setDraft(previous => {
    const next = update(structuredClone(previous));
    if (next.mcqCount != null) next.questionCount = next.mcqCount + next.essayCount;
    return next;
  }); setDirty(true);};
  const byKind = draft.mcqCount != null;
  const selectByKind = enabled => edit(next => {
    if (enabled) {next.mcqCount = bankCount(next, 'mcq'); next.essayCount = bankCount(next, 'essay');}
    else {delete next.mcqCount; delete next.essayCount;}
    return next;
  });
  const field = (key, value) => edit(next => ({...next, [key]: value}));
  const questionField = (index, key, value) => edit(next => {next.questions[index][key] = value; return next;});
  const optionField = (index, choice, value) => edit(next => {next.questions[index].options[choice] = value; return next;});
  const add = kind => {
    if (draft.questions.length >= 100) {toast('الحد الأقصى ١٠٠ سؤال', true); return;}
    edit(next => {
      const allShown = byKind ? next[kind+'Count'] === bankCount(next, kind) : next.questionCount === next.questions.length;
      next.questions.push(emptyQuestion(kind));
      if (allShown) {if (byKind) next[kind+'Count']++; else next.questionCount++;}
      return next;
    });
  };
  const remove = index => edit(next => {
    const kind = next.questions[index].kind;
    next.questions.splice(index, 1);
    if (byKind) next[kind+'Count'] = Math.min(next[kind+'Count'], bankCount(next, kind));
    else next.questionCount = Math.min(next.questionCount, next.questions.length);
    return next;
  });
  const submit = async event => {
    event.preventDefault();
    if (draft.questionCount < 1) {toast('اختر سؤالًا واحدًا على الأقل لكل طالب', true); return;}
    setSaving(true);
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
          {!byKind && <label>عدد الأسئلة اللي تظهر لكل طالب<input name="questionCount" type="number" min="1" max={draft.questions.length}
            required value={draft.questionCount} onChange={event => field('questionCount', Number(event.target.value))} /></label>}
          <label>نظام الوقت<select name="timerMode" value={draft.timerMode} onChange={event => field('timerMode', event.target.value)}>
            <option value="shared">نهاية موحدة للجميع</option><option value="individual">مدة كاملة لكل طالب</option></select></label>
        </div>
        <label className="check-label"><input type="checkbox" checked={byKind}
          onChange={event => selectByKind(event.target.checked)} />تحديد عدد الاختياري والمقالي كل نوع لوحده</label>
        {byKind && <div className="form-grid">
          <label>عدد الاختياري لكل طالب<input name="mcqCount" type="number" min="0" max={bankCount(draft, 'mcq')}
            required value={draft.mcqCount} onChange={event => field('mcqCount', Number(event.target.value))} />
            <span className="help">متاح في البنك: {number(bankCount(draft, 'mcq'))} سؤال اختياري</span></label>
          <label>عدد المقالي لكل طالب<input name="essayCount" type="number" min="0" max={bankCount(draft, 'essay')}
            required value={draft.essayCount} onChange={event => field('essayCount', Number(event.target.value))} />
            <span className="help">متاح في البنك: {number(bankCount(draft, 'essay'))} سؤال مقالي</span></label>
        </div>}
        <div className="question-bank-summary" aria-live="polite"><strong>بنك الأسئلة: {number(draft.questions.length)} سؤال · يظهر لكل طالب: {number(draft.questionCount)} سؤال</strong>
          {byKind && <p>اختياري: {number(draft.mcqCount)} من {number(bankCount(draft, 'mcq'))} · مقالي: {number(draft.essayCount)} من {number(bankCount(draft, 'essay'))}</p>}
          <p>{byKind ? 'أضف الأسئلة في البنك تحت، ثم حدد العدد المعروض من كل نوع؛ مثلًا ٥ اختياري من ٢٠، و١ مقالي من ٢. صفر يعني عدم عرض أسئلة من هذا النوع.' : 'تُسحب الأسئلة من البنك كله دون تحديد عدد لكل نوع.'} السحب عشوائي لكل طالب بدون تكرار السؤال داخل محاولته، وأسئلته تظل محفوظة عند الخروج والعودة.</p></div>
        <p className="help">في النهاية الموحدة، الطالب المتأخر يأخذ الوقت المتبقي. {byKind ? 'عند سحب جزء من نوع معين، لازم درجات أسئلة النوع ده تتساوى. ممكن درجة المقالي تختلف عن الاختياري.' : 'عند سحب جزء من البنك كله، لازم درجات الأسئلة كلها تتساوى.'}</p>
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
