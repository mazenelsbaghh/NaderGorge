import {$, $$, escapeHtml as e, number, toast} from './common.js';

const emptyQuestion = kind => ({kind, text: '', points: 2, options: ['', '', '', ''], correct: 0, modelAnswer: ''});
export function renderEditor(container, config, save, cancel) {
  let dirty = false;
  let draft = structuredClone(config || {title: '', instructions: '', minutes: 15, timerMode: 'shared',
    allowLate: true, shuffle: false, questionCount: 1, questions: [emptyQuestion('mcq')]});
  function questionMarkup(question, index) {
    const choices = question.kind === 'mcq' ? `<fieldset class="options-editor"><legend>الاختيارات · حدد الإجابة الصحيحة</legend>${question.options.map((option, choice) => `<label class="option-editor"><input type="radio" name="correct-${index}" value="${choice}" ${question.correct === choice ? 'checked' : ''} aria-label="الاختيار الصحيح ${choice + 1}"><input class="option-text" value="${e(option)}" maxlength="1000" required aria-label="نص الاختيار ${choice + 1}" placeholder="الاختيار ${number(choice + 1)}"></label>`).join('')}</fieldset>` :
      `<label>الإجابة النموذجية ومعايير التصحيح<textarea class="model-answer" rows="3" maxlength="10000" required placeholder="اكتب النقاط التي تُمنح عليها الدرجة…">${e(question.modelAnswer)}</textarea></label>`;
    return `<section class="question-editor" data-index="${index}"><div class="section-top"><h3>السؤال ${number(index + 1)} <span class="badge quiet">${question.kind === 'mcq' ? 'اختيار من متعدد' : 'مقالي'}</span></h3><button type="button" class="text-button danger-text" data-remove="${index}" ${draft.questions.length === 1 ? 'disabled' : ''}>حذف السؤال</button></div>
      <div class="question-fields"><label>نص السؤال<textarea class="question-text" required rows="2" maxlength="5000" placeholder="اكتب السؤال هنا…">${e(question.text)}</textarea></label><label>الدرجة<input class="question-points" type="number" min="1" max="100" value="${question.points}" required></label></div>${choices}</section>`;
  }
  function collect() {
    const form = $('#exam-editor');
    const controls = form.elements;
    return {title: controls.title.value, instructions: controls.instructions.value,
      minutes: Number(controls.minutes.value), timerMode: controls.timerMode.value,
      allowLate: controls.allowLate.checked, shuffle: controls.shuffle.checked,
      questionCount: Number(controls.questionCount.value),
      questions: $$('.question-editor', form).map((section, index) => ({kind: draft.questions[index].kind,
        text: $('.question-text', section).value, points: Number($('.question-points', section).value),
        ...(draft.questions[index].kind === 'mcq' ? {options: $$('.option-text', section).map(input => input.value),
          correct: Number($('input[type=radio]:checked', section).value)} : {modelAnswer: $('.model-answer', section).value})}))};
  }
  function paint() {
    container.innerHTML = `<div class="page-heading"><div><span class="context-label">تجهيز الامتحان</span><h1>${config ? 'تعديل الامتحان' : 'امتحان جديد'}</h1><p>جهّز الأسئلة أولًا، ثم افتح القاعة لاستقبال الطلاب.</p></div><button class="button secondary" id="cancel-editor">رجوع للقاعة</button></div>
      <form id="exam-editor"><section class="panel editor-basics"><h2>تفاصيل الامتحان</h2>
      <label>اسم الامتحان<input name="title" required maxlength="150" value="${e(draft.title)}" placeholder="مثلًا: مراجعة الفصل الأول"></label>
      <label>تعليمات للطلاب <span class="muted">اختياري</span><textarea name="instructions" rows="2" maxlength="2000" placeholder="أي تعليمات يحتاجها الطالب قبل الحل">${e(draft.instructions)}</textarea></label>
      <div class="form-grid"><label>المدة بالدقائق<input name="minutes" type="number" min="1" max="240" value="${draft.minutes}" required></label>
      <label>الأسئلة المعروضة لكل طالب<input name="questionCount" type="number" min="1" max="${draft.questions.length}" value="${draft.questionCount}" required></label>
      <label>نظام الوقت<select name="timerMode"><option value="shared" ${draft.timerMode === 'shared' ? 'selected' : ''}>نهاية موحدة للجميع</option><option value="individual" ${draft.timerMode === 'individual' ? 'selected' : ''}>مدة كاملة لكل طالب</option></select></label></div>
      <p class="help">في النهاية الموحدة، الطالب المتأخر يأخذ الوقت المتبقي. لو عرضت جزءًا من الأسئلة، تُسحب عشوائيًا ويجب أن تتساوى درجاتها.</p>
      <div class="check-row"><label class="check-label"><input type="checkbox" name="allowLate" ${draft.allowLate ? 'checked' : ''}>السماح بالدخول بعد البداية</label><label class="check-label"><input type="checkbox" name="shuffle" ${draft.shuffle ? 'checked' : ''}>تغيير ترتيب الأسئلة بين الطلاب</label></div></section>
      <div class="section-top questions-heading"><h2>الأسئلة <span class="muted">(${number(draft.questions.length)})</span></h2><span class="muted">الإجابات النموذجية تظهر للإدارة فقط</span></div>
      <div id="question-list">${draft.questions.map(questionMarkup).join('')}</div>
      <div class="add-questions"><button class="button secondary" type="button" data-add="mcq">＋ سؤال اختيارات</button><button class="button secondary" type="button" data-add="essay">＋ سؤال مقالي</button></div>
      <div class="editor-footer"><p>حفظ كمسودة؛ الطلاب لن يروا الامتحان حتى تفتح القاعة.</p><button class="button" type="submit">حفظ الامتحان</button></div></form>`;
    $('#cancel-editor').onclick = cancel;
    $('#exam-editor').oninput = () => { dirty = true; };
    $('#exam-editor').onchange = () => { dirty = true; };
    $$('[data-add]').forEach(button => button.onclick = () => {
      if (draft.questions.length >= 100) { toast('الحد الأقصى ١٠٠ سؤال', true); return; }
      dirty = true; draft = collect();
      const allShown = draft.questionCount === draft.questions.length;
      draft.questions.push(emptyQuestion(button.dataset.add));
      if (allShown) draft.questionCount++;
      paint();
      $$('.question-text').at(-1).focus();
    });
    $$('[data-remove]').forEach(button => button.onclick = () => {
      dirty = true; draft = collect(); draft.questions.splice(Number(button.dataset.remove), 1);
      draft.questionCount = Math.min(draft.questionCount, draft.questions.length); paint();
    });
    $('#exam-editor').onsubmit = async event => {
      event.preventDefault();
      const button = $('button[type=submit]', event.currentTarget);
      button.disabled = true; button.textContent = 'جارٍ الحفظ…';
      try { await save(collect()); dirty = false; }
      catch (error) { toast(error.message, true); button.disabled = false; button.textContent = 'حفظ الامتحان'; }
    };
  }
  paint();
  return () => dirty;
}
