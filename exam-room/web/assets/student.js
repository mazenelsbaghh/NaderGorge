import {$, $$, api as sendRequest, escapeHtml as e, number, timeLabel, toast, cloneData, formValues, askDialog, downloadBlob} from './common.js';

import {screenGuardActive,screenGuardBlocked,enterExamScreen,screenMeasurement} from './screen-guard.js';
import {createStudentShortcut} from './student-shortcut.js';
import {createStudentDiagnostics} from './student-diagnostics.js';

const diagnostics=createStudentDiagnostics(sendRequest);
const api=diagnostics.request;

const main = $('#student-main');
const joinMarkup = main.innerHTML;
let session, answers = {}, serverRevision = 0, pendingWrite = null, saving = false;
let finishing = false, dirty = false, conflict = false, clockOffset = 0, saveTimer, online = true, joining = false;
let sessionExpired = false, examLayoutObserver, unreceivedDraft = false;
let unreceivedDrafts = [];
let pollTimer, polling = false;
let renderedState = '', renderedId = '', lastLounge = '', questionIndex = 0;
const draftKey = id => `massar-exam-draft:${id}`;
const shortcut=createStudentShortcut(canOfferShortcut, readShortcutRisk);

function canOfferShortcut() {
  return session===null&&!joining&&!sessionExpired&&renderedState==='join';
}
function readShortcutRisk() {
  if(unreceivedDraft||unreceivedDrafts.length)return 'توجد إجابات لم يتأكد وصولها. نزّل النسخة الاحتياطية للمشرف قبل إضافة الاختصار أو تغيير المتصفح.';
  try {
    for(let index=0;index<localStorage.length;index++) {
      const key=localStorage.key(index);
      if(key&&key.indexOf('massar-exam-draft:')===0&&draftNotReceived(JSON.parse(localStorage.getItem(key))))return 'توجد مسودة إجابات لم يتأكد وصولها. نزّلها من أسفل الصفحة للمشرف قبل إضافة الاختصار أو تغيير المتصفح.';
    }
  } catch {return 'تعذر فحص الإجابات المحفوظة في هذا المتصفح. يمكنك تسجيل بياناتك والدخول هنا عاديًا؛ راجع المشرف قبل إضافة الاختصار أو تغيير المتصفح، ولا تمسح بياناته.';}
  return '';
}

function currentDraft() {
  return {answers, serverRevision, pendingWrite, finishing, questionIndex, dirty, attemptId:session.id, examId:session.examId, name:session.name, code:session.code, title:session.title};
}
function saveDraft() {
  if (!session || session.state !== 'running') return;
  try {
    localStorage.setItem(draftKey(session.id), JSON.stringify(currentDraft()));
  } catch {
    toast('التخزين المؤقت على هذا المتصفح غير متاح. تأكد من ظهور «تم الحفظ على الجهاز».', true);
  }
}
function exportAnswerBackup(draft) {
  const backup = {format:'massar-answer-backup-v1', attemptId:draft.attemptId, examId:draft.examId,
    name:draft.name, code:draft.code, title:draft.title, answers:draft.answers};
  downloadBlob(new Blob([JSON.stringify(backup,null,2)],{type:'application/json'}),`Massar-Answers-${draft.attemptId.slice(0,8)}.json`);
  toast('تم تجهيز نسخة الإجابات. احتفظ بالملف وقدّمه للمشرف إذا لم تصل إجاباتك.');
}
function renderDraftDownloads() {
  const drafts=[];
  try {
    for(let index=0;index<localStorage.length;index++) {
      const key=localStorage.key(index);
      if(key.indexOf('massar-exam-draft:')!==0)continue;
      try {
        const draft=JSON.parse(localStorage.getItem(key));
        if(typeof draft?.attemptId==='string'&&draft.attemptId&&draft.examId&&draft.answers&&(draft.dirty||draft.pendingWrite))drafts.push(draft);
      } catch { /* A damaged draft must not hide the other recoverable attempts. */ }
    }
  } catch { return; }
  if(!drafts.length)return;
  const panel=document.createElement('section');panel.className='notice';
  const description=document.createElement('p');description.textContent='توجد مسودات إجابات على هذا المتصفح. نزّل نسخة للمشرف قبل حذف بياناته أو تغيير الجهاز.';panel.appendChild(description);
  drafts.forEach(draft=>{const button=document.createElement('button');button.className='button secondary';button.textContent='تنزيل نسخة: '+draft.title+' — '+draft.code;button.onclick=()=>exportAnswerBackup(draft);panel.appendChild(button);});
  main.appendChild(panel);
}
function downloadAnswerBackup() {
  exportAnswerBackup(currentDraft());
}
function sameAnswers(left, right) {
  const keys = Object.keys(left || {});
  return keys.length === Object.keys(right || {}).length && keys.every(key => left[key] === right[key]);
}
function draftNotReceived(draft) {
  // Another tab can accept different answers at the same revision; the number is not a receipt.
  return Boolean(draft?.answers && typeof draft.answers === 'object' && !Array.isArray(draft.answers) && (draft.pendingWrite || draft.dirty !== false));
}
function storedAttemptDrafts(attemptId) {
  const drafts = [];
  try {
    for (let index = 0; index < localStorage.length; index++) {
      const key = localStorage.key(index);
      if (key !== draftKey(attemptId) && key.indexOf(draftKey(attemptId) + ':unreceived:') !== 0) continue;
      try { const draft = JSON.parse(localStorage.getItem(key)); if (draftNotReceived(draft)) drafts.push(draft); }
      catch { /* A damaged draft must not hide the other recoverable attempts. */ }
    }
  } catch { /* Keep the current page's answers available for export when storage is unavailable. */ }
  return drafts;
}
function retainDraft(draft, nextSession) {
  if (unreceivedDrafts.some(saved => sameAnswers(saved.answers, draft.answers))) return;
  const retained = {...draft, attemptId:nextSession.id, examId:nextSession.examId, name:nextSession.name, code:nextSession.code, title:nextSession.title};
  retained.backupKey = draft.backupKey || draftKey(nextSession.id) + ':unreceived:' + Date.now().toString(36) + Math.random().toString(36).slice(2);
  unreceivedDrafts.push(retained);
  try { localStorage.setItem(retained.backupKey, JSON.stringify(retained)); }
  catch { toast('تعذر حفظ نسخة الإجابات في المتصفح. نزّلها الآن قبل إغلاق الصفحة.', true); }
}
function restoreRetainedDrafts(nextSession) {
  const retained = unreceivedDrafts.filter(draft => draft.attemptId === nextSession.id)
    .concat(storedAttemptDrafts(nextSession.id).filter(draft => draft.backupKey));
  unreceivedDrafts = [];
  retained.forEach(draft => retainDraft(draft, nextSession));
}
function retainUnreceivedDrafts(nextSession) {
  const candidates = unreceivedDrafts.filter(draft => draft.attemptId === nextSession.id)
    .concat(storedAttemptDrafts(nextSession.id));
  if (session?.id === nextSession.id && session.state === 'running' && (dirty || pendingWrite)) candidates.push(cloneData(currentDraft()));
  unreceivedDrafts = [];
  candidates.forEach(draft => retainDraft(draft, nextSession));
}
function renderRetainedDrafts() {
  const panel = $('#retained-drafts');
  if (!panel) return;
  panel.hidden = unreceivedDrafts.length === 0;
  panel.innerHTML = '<strong>توجد نسخة سابقة من إجاباتك تحتاج مراجعة.</strong><p>لم يتأكد حفظ هذه النسخة على جهاز الإدارة. احتفظنا بها منفصلة للمراجعة؛ نزّلها للمشرف قبل حذف بيانات المتصفح. استخدم نافذة واحدة للامتحان.</p>';
  unreceivedDrafts.forEach((draft, index) => {
    const button = document.createElement('button'); button.className = 'button secondary'; button.type = 'button';
    button.textContent = 'تنزيل الإجابات السابقة للمراجعة' + (unreceivedDrafts.length > 1 ? ' ' + number(index + 1) : '');
    button.onclick = () => exportAnswerBackup(draft); panel.appendChild(button);
  });
}
function recoverDraft(nextSession) {
  answers = cloneData(nextSession.answers); serverRevision = nextSession.revision;
  pendingWrite = null; finishing = false; dirty = false; conflict = false; questionIndex = 0;
  restoreRetainedDrafts(nextSession);
  let saved;
  try { saved = JSON.parse(localStorage.getItem(draftKey(nextSession.id)) || 'null'); }
  catch { return; }
  if (!saved) return;
  if (Number.isInteger(saved.questionIndex)) questionIndex = Math.max(0, Math.min(saved.questionIndex, nextSession.questions.length - 1));
  const acknowledged = saved.pendingWrite?.revision === nextSession.revision &&
    sameAnswers(saved.pendingWrite.answers, nextSession.answers);
  if (saved.serverRevision !== nextSession.revision && !acknowledged) {
    if (draftNotReceived(saved) && !sameAnswers(saved.answers, nextSession.answers)) {
      retainDraft(saved, nextSession);
      toast('تم تحميل آخر إجابات محفوظة على جهاز الإدارة، واحتفظنا بالنسخة المختلفة للمراجعة.');
    } else toast('تم تحميل آخر إجابات محفوظة على جهاز الإدارة.');
    return;
  }
  answers = saved.answers; pendingWrite = acknowledged ? null : saved.pendingWrite;
  finishing = saved.finishing;
  dirty = !sameAnswers(answers, nextSession.answers);
}
function setNetwork(connected) {
  diagnostics.connectivity(connected);
  online = connected;
  const banner = $('#network-alert');
  banner.hidden = connected;
  banner.textContent = session?.state === 'running'
    ? 'الاتصال بجهاز الامتحان منقطع. ارجع لواي فاي السنتر واختر البقاء متصلًا بدون إنترنت. الإجابات تنتظر الإرسال؛ نزّل نسخة احتياطية قبل تغيير المتصفح.'
    : 'لم يصل رد من جهاز الامتحان. ابقَ على واي فاي السنتر واختر البقاء متصلًا بدون إنترنت؛ بنحاول تلقائيًا. البيانات اللي كتبتها موجودة.';
  if (!connected) {
    const retry=document.createElement('button');retry.className='button secondary small';retry.textContent='تحقق من الاتصال الآن';retry.onclick=()=>poll();banner.appendChild(retry);
  }
  updateSaveStatus();
}
function acceptSession(nextSession, fromWrite = false) {
  shortcut.hide();
  if (session?.id === nextSession.id &&
      (nextSession.revision < session.revision || ['submitted', 'cancelled'].indexOf(session.state) >= 0 && ['running', 'waiting'].indexOf(nextSession.state) >= 0)) return;
  const changedAttempt = session?.id !== nextSession.id;
  const becameRunning = session?.state !== 'running' && nextSession.state === 'running';
  if (nextSession.state === 'submitted' && (changedAttempt || session.state !== 'submitted')) retainUnreceivedDrafts(nextSession);
  clockOffset = nextSession.serverTime * 1000 - Date.now();
  session = nextSession; sessionExpired = false;
  if (changedAttempt || becameRunning) recoverDraft(nextSession);
  if (!fromWrite && nextSession.state === 'running' && !pendingWrite && !dirty && !saving && nextSession.revision > serverRevision) {
    answers = cloneData(nextSession.answers); serverRevision = nextSession.revision;
    renderedState = '';
  }
  if (nextSession.state === 'submitted') {
    unreceivedDraft = unreceivedDrafts.length > 0;
    finishing = false; dirty = false; pendingWrite = null;
    if (!unreceivedDraft) {
      try { localStorage.removeItem(draftKey(nextSession.id)); } catch { /* Storage may be unavailable. */ }
    }
  }
  if (renderedState !== nextSession.state || renderedId !== nextSession.id) renderSession();
  if (nextSession.state === 'running') {freezeInputs(); if(remainingSeconds()>0&&!finishing) $('#submit-exam').textContent='مراجعة وتسليم ←';}
  const pauseNotice=$('#exam-paused');if(pauseNotice)pauseNotice.hidden=!nextSession.paused;
  updateScreenNotice(); updateTimer(); updateSaveStatus();
}
function renderSession() {
  examLayoutObserver?.disconnect();
  renderedState = session.state; renderedId = session.id;
  if (session.state === 'cancelled') {
    main.innerHTML = `<section class="student-finish"><span class="badge disconnected">محاولة ملغاة</span><h1>تم إلغاء امتحانك، ${e(session.name)}.</h1><p>كود الطالب: <b dir="ltr">${e(session.code)}</b></p><div class="notice"><strong>سبب الإلغاء</strong><p>${e(session.cancelReason)}</p></div><p>راجع المشرف. لا يمكنك استكمال هذه المحاولة.</p></section>`;
    return;
  }
  if (session.state === 'submitted') {
    main.innerHTML = `<section class="student-finish"><div class="success-mark" aria-hidden="true">✓</div><span class="badge complete">اكتمل التسليم</span><h1>وصلت إجاباتك، ${e(session.name)}.</h1><p>تم حفظ محاولتك على جهاز الإدارة.</p><div class="submission-receipt"><strong>${e(session.title)}</strong><span>وقت الاستلام: ${new Date(session.submittedAt * 1000).toLocaleTimeString('ar-EG')}</span><small>مرجع المحاولة: <b dir="ltr">${session.id.slice(0, 8)}</b></small></div><p class="muted">لن تظهر الإجابات أو الدرجات على هذه الصفحة.<br>يمكنك الآن إغلاقها، واستلام النتيجة لاحقًا من المدرس.</p></section>`;
    const nextButton = document.createElement('button');
    nextButton.className = 'button secondary';
    nextButton.textContent = 'دخول امتحان آخر';
    nextButton.onclick = async () => {
      nextButton.disabled = true;
      try {
        await api('/api/leave', {});
        session = null; renderedState = ''; await pollLounge();
      } catch (error) { toast(error.message, true); nextButton.disabled = false; }
    };
    $('.student-finish').appendChild(nextButton);
    if (unreceivedDraft) {
      const warning = document.createElement('p'); warning.className = 'notice'; warning.setAttribute('role', 'alert');
      warning.textContent = 'توجد إجابات محفوظة على هذا المتصفح لم يتأكد وصولها قبل إغلاق المحاولة. النتيجة تعتمد على ما وصل لجهاز الإدارة فقط. لا تمسح بيانات المتصفح وراجع المشرف.';
      $('.student-finish h1').textContent = 'تم إغلاق محاولتك، ' + session.name;
      $('.student-finish > p').textContent = 'تم اعتماد الإجابات التي وصلت إلى جهاز الإدارة.';
      $('.student-finish').insertBefore(warning, $('.submission-receipt'));
      unreceivedDrafts.forEach((draft,index)=>{
        const backupButton=document.createElement('button');backupButton.className='button';backupButton.textContent='تنزيل نسخة احتياطية للإجابات'+(unreceivedDrafts.length>1?' '+number(index+1):'');backupButton.onclick=()=>exportAnswerBackup(draft);
        $('.student-finish').insertBefore(backupButton,nextButton);
      });
    }
    return;
  }
  if (session.state === 'waiting') {
    main.innerHTML = `<section class="student-wait"><div class="waiting-symbol" aria-hidden="true">◷</div><span class="badge waiting">أنت داخل القاعة</span><h1>جاهز يا ${e(session.name)}؟</h1><p>في انتظار المدرس لبدء الامتحان.<br>الأسئلة هتظهر هنا تلقائيًا؛ سيب الصفحة مفتوحة.</p><div class="waiting-exam"><h2>${e(session.title)}</h2><div><span>${number(session.questionCount)} أسئلة</span><span>${number(session.minutes)} دقيقة</span><span>${session.timerMode === 'shared' ? 'نهاية موحدة للجميع' : 'مدة كاملة لكل طالب'}</span></div></div><p class="help">حافظ على اتصالك بنفس شبكة الواي فاي.</p></section>`;
    return;
  }
  if (session.state !== 'running') return;
  main.innerHTML = `<div class="exam-sticky"><div class="exam-header-details"><div class="student-identity"><strong>${e(session.name)}</strong><span>كود الطالب: <b dir="ltr">${e(session.code)}</b></span></div><strong class="exam-header-title">${e(session.title)}</strong><span id="save-status" role="status">تم الحفظ على الجهاز</span></div><div class="timer-box"><small>الوقت المتبقي</small><strong id="exam-timer" dir="ltr">--:--</strong></div></div>
    <div class="exam-paper"><div class="student-exam-heading"><span class="context-label">بالتوفيق يا ${e(session.name)}</span><h1>سؤال بخطوة، لحد ما تكمّل.</h1>${session.instructions ? `<p class="instructions">${e(session.instructions)}</p>` : ''}<nav class="question-nav" aria-label="خطوات الامتحان">${session.questions.map((question, index) => `<button type="button" data-nav="${question.id}" data-index="${index}" aria-label="السؤال ${index + 1}">${number(index + 1)}</button>`).join('')}</nav><div class="step-summary"><strong id="step-position" aria-live="polite"></strong><span id="answer-progress"></span></div><progress id="step-progress" aria-label="تقدمك في أسئلة الامتحان" max="${session.questions.length}" value="1"></progress></div>
    <div id="screen-guard-notice" class="notice" hidden><strong>مراقبة شاشة الامتحان</strong><p>سيُسجّل مقاس العرض وعلامة هذا المتصفح. الخروج من ملء الشاشة أو انخفاض المساحة قد يوقف الحل؛ الوقت يستمر.</p><button class="button secondary" id="enter-exam-screen">نعم، افتح شاشة الامتحان</button><p id="screen-support" class="help"></p></div><div id="exam-paused" class="notice" role="alert" hidden><strong>الامتحان موقوف مؤقتًا</strong><p>إجاباتك محفوظة والوقت ما زال شغالًا. اطلب من المشرف الضغط على «استكمال الطالب».</p></div><div id="answer-conflict" class="notice" hidden><strong>تغيرت الإجابات في نافذة أخرى.</strong><p>استخدم نافذة واحدة للامتحان. أعد تحميل الصفحة لتحميل آخر حفظ قبل المتابعة.</p><a class="button secondary" href="/">إعادة تحميل الصفحة</a></div>
    <section id="retained-drafts" class="notice" role="status" hidden></section>
    ${session.questions.map((question, index) => questionMarkup(question, index)).join('')}
    <button type="button" class="button secondary" id="answer-backup">تنزيل نسخة احتياطية للإجابات</button><p class="help">احتفظ بالملف قبل تغيير الموبايل أو المتصفح. النسخة لا تُسلّم الامتحان؛ اعتمادها يكون بواسطة المشرف.</p><p class="help paper-note">تأكد من ظهور «تم الحفظ على الجهاز» قبل التسليم. عند انتهاء الوقت تُعتمد آخر إجابات وصلت لجهاز الإدارة.</p></div>
    <footer class="exam-submit-bar step-controls"><button class="button secondary" id="previous-question">→ السابق</button><button class="button" id="next-question">التالي ←</button><button class="button" id="submit-exam" hidden>مراجعة وتسليم ←</button></footer>`;
  $$('[data-answer]').forEach(input => input.addEventListener(input.type === 'radio' ? 'change' : 'input', () => {
    answers[input.dataset.answer] = input.type === 'radio' ? Number(input.value) : input.value;
    dirty = true; saveDraft(); updateProgress(); updateSaveStatus();
    clearTimeout(saveTimer); saveTimer = setTimeout(flushAnswers, input.type === 'radio' ? 0 : 350);
  }));
  $('#enter-exam-screen').onclick = async()=>{try{const supported=await enterExamScreen();$('#screen-support').textContent=supported?'':'هذا المتصفح لا يدعم ملء الشاشة. سنراقب مساحة العرض فقط؛ أبلغ المشرف.';await announceVisibility();freezeInputs();updateScreenNotice();}catch{toast('تعذر فتح ملء الشاشة. افتح الرابط في متصفح يدعمه أو راجع المشرف.',true);}};
  $('#answer-backup').onclick = downloadAnswerBackup;
  $('#submit-exam').onclick = requestSubmission;
  $('#previous-question').onclick = () => moveQuestion(questionIndex - 1);
  $('#next-question').onclick = () => moveQuestion(questionIndex + 1);
  $$('[data-nav]').forEach(button => button.onclick = () => moveQuestion(Number(button.dataset.index)));
  showQuestion(); updateProgress(); freezeInputs(); renderRetainedDrafts();
  measureExamLayout();
}
function measureExamLayout() {
  const controls = $('.exam-submit-bar');
  const header = $('.exam-sticky');
  const update = () => {
    main.style.setProperty('--exam-controls-height', `${controls.getBoundingClientRect().height}px`);
    main.style.setProperty('--exam-header-height', `${header.getBoundingClientRect().height}px`);
  };
  update();
  if(typeof ResizeObserver==='function'){
    examLayoutObserver=new ResizeObserver(update);examLayoutObserver.observe(controls);examLayoutObserver.observe(header);
  }else{
    window.addEventListener('resize',update);
    examLayoutObserver={disconnect:()=>window.removeEventListener('resize',update)};
  }
}
function moveQuestion(index) {
  if (index < 0 || index >= session.questions.length || finishing) return;
  questionIndex = index;
  saveDraft(); showQuestion(); flushAnswers();
  const heading = $('.student-question:not([hidden]) h2');
  heading.focus();
  const banner = $('#network-alert');
  const covered = $('.exam-sticky').getBoundingClientRect().height + (banner.hidden ? 0 : banner.getBoundingClientRect().height);
  window.scrollTo(0, Math.max(0, heading.getBoundingClientRect().top + window.pageYOffset - covered - 16));
}
function showQuestion() {
  $$('.student-question').forEach((section, index) => { section.hidden = index !== questionIndex; });
  $$('[data-nav]').forEach((button, index) => {
    if (index === questionIndex) button.setAttribute('aria-current', 'step');
    else button.removeAttribute('aria-current');
  });
  $('#step-position').textContent = `السؤال ${number(questionIndex + 1)} من ${number(session.questions.length)}`;
  $('#step-progress').value = questionIndex + 1;
  $('#previous-question').disabled = questionIndex === 0;
  $('#next-question').hidden = questionIndex === session.questions.length - 1;
  $('#submit-exam').hidden = questionIndex !== session.questions.length - 1;
}
function questionMarkup(question, index) {
  const answer = answers[question.id];
  const input = question.kind === 'mcq' ? `<fieldset class="student-options"><legend class="sr-only">اختر إجابة السؤال ${index + 1}</legend>${(question.optionOrder || question.options.map((_,i)=>i)).map(choice => `<label class="student-option"><input type="radio" data-answer="${question.id}" name="q-${question.id}" value="${choice}" ${answer === choice ? 'checked' : ''}><span>${e(question.options[choice])}</span></label>`).join('')}</fieldset>` : `<label class="essay-label" for="answer-${question.id}">إجابتك<textarea id="answer-${question.id}" data-answer="${question.id}" rows="5" maxlength="10000" placeholder="اكتب إجابتك هنا…">${e(answer || '')}</textarea></label>`;
  return `<section class="student-question" id="q-${question.id}"><div class="question-meta"><span>السؤال ${number(index + 1)}</span><span>${number(question.points)} درجات · ${question.kind === 'mcq' ? 'اختيار من متعدد' : 'مقالي'}</span></div><h2 tabindex="-1">${e(question.text)}</h2>${input}</section>`;
}
function hasAnswer(question) {
  const answer = answers[question.id];
  return question.kind === 'mcq' ? Number.isInteger(answer) : typeof answer === 'string' && answer.trim().length > 0;
}
function updateProgress() {
  if (!$('#answer-progress')) return;
  const answered = session.questions.filter(hasAnswer).length;
  $('#answer-progress').textContent = `أجبت عن ${number(answered)} من ${number(session.questions.length)}`;
  session.questions.forEach(question => $(`[data-nav="${question.id}"]`)?.classList.toggle('answered', hasAnswer(question)));
}
function updateSaveStatus() {
  const label = $('#save-status');
  if (!label) return;
  label.textContent = conflict ? 'تحتاج إعادة تحميل الصفحة' : !online ? 'لم تصل آخر التغييرات؛ نحاول الاتصال…' : saving ? 'جارٍ حفظ الإجابات…' : dirty || pendingWrite ? 'تغييرات تنتظر الحفظ…' : '✓ تم الحفظ على الجهاز';
  label.className = online && !dirty && !pendingWrite && !saving ? 'saved' : 'unsaved';
}
function remainingSeconds() { return Math.max(0, (session.deadline * 1000 - Date.now() - clockOffset) / 1000); }
function updateTimer() {
  if (!session || session.state !== 'running' || !$('#exam-timer')) return;
  const remaining = remainingSeconds();
  $('#exam-timer').textContent = timeLabel(Math.ceil(remaining));
  $('.timer-box').classList.toggle('urgent', remaining <= 60);
  if (remaining === 0) {
    $('#exam-timer').textContent = '00:00'; freezeInputs();
    $('#submit-exam').textContent = 'انتهى الوقت؛ ننتظر تأكيد الاستلام';
  }
}
function freezeInputs() {
  const screenBlocked=screenGuardBlocked(session);
  const frozen = screenBlocked || session?.paused || finishing || conflict || remainingSeconds() <= 0;
  $$('[data-answer]').forEach(input => { input.disabled = frozen; });
  $$('[data-nav], #next-question').forEach(button => { button.disabled = screenBlocked || session?.paused || finishing; });
  if ($('#previous-question')) $('#previous-question').disabled = screenBlocked || session?.paused || finishing || questionIndex === 0;
  if ($('#submit-exam')) {
    $('#submit-exam').disabled = screenBlocked || session?.paused || conflict || remainingSeconds() <= 0 || (finishing && saving);
    if (finishing && remainingSeconds() > 0) $('#submit-exam').textContent = online ? 'جارٍ تأكيد التسليم…' : 'إعادة محاولة التسليم';
  }
}
async function flushAnswers() {
  if (!session || session.state !== 'running' || session.paused || screenGuardBlocked(session) || saving || conflict || (!dirty && !pendingWrite && !finishing)) return;
  const attemptId = session.id;
  if (!pendingWrite) {
    pendingWrite = {answers: cloneData(answers), revision: serverRevision + 1, submit: finishing};
    saveDraft();
  }
  saving = true; updateSaveStatus(); freezeInputs();
  try {
    const sent = pendingWrite;
    const response = await api('/api/answers', sent);
    if (session?.id !== attemptId || response.session.id !== attemptId) return;
    serverRevision = response.session.revision;
    if (response.answersReceived === true) {
      pendingWrite = null;
      dirty = !sameAnswers(answers, sent.answers);
      saveDraft();
    }
    setNetwork(true); acceptSession(response.session, true);
    if (session.state === 'running') saveDraft();
  } catch (error) {
    if (session?.id !== attemptId || session.state !== 'running') return;
    if (error.status === 423) {
      try {const response=await api(`/api/session?visible=${document.hidden?0:1}`);if(session?.id===attemptId&&response.session?.id===attemptId)acceptSession(response.session);}
      catch {if(session?.id===attemptId)setNetwork(false);}
    } else if (error.status === 409 || error.status === 400) {
      conflict = true;
      retainUnreceivedDrafts(session); renderRetainedDrafts();
      $('#answer-conflict').hidden = false;
      toast(error.message, true);
    } else if (error.status === 401) {
      await expiredSession(error.message);
    } else setNetwork(false);
  } finally {
    saving = false; updateSaveStatus();
    if (session?.state === 'running') {
      freezeInputs();
      if (online && !session.paused && !conflict && (dirty || finishing)) setTimeout(flushAnswers, 150);
    }
  }
}
async function requestSubmission() {
  if (finishing) { await flushAnswers(); return; }
  const missing = session.questions.filter(question => !hasAnswer(question)).length;
  const dialog = $('#submit-dialog');
  $('#submit-copy').textContent = missing ? `لسه فيه ${number(missing)} أسئلة بدون إجابة. تقدر ترجع تكملها أو تسلم الآن.` : 'أجبت عن كل الأسئلة. سنرسل الإجابات ونؤكد حفظها قبل إنهاء المحاولة.';
  const approved=await askDialog(dialog,$('#submit-copy').textContent);
  if (!approved || session?.state !== 'running') return;
  finishing = true; saveDraft(); freezeInputs(); await flushAnswers();
}
async function joinRoom(identity, button) {
  for (let attempt = 0; attempt < 3; attempt++) {
    try { return await api('/api/join', identity); }
    catch (error) {
      if (attempt === 2 || error.status && error.status < 500) throw error;
      button.textContent = 'الاتصال متقطع؛ بنحاول الدخول مرة تانية…';
      await new Promise(resolve => setTimeout(resolve, 1000 + Math.random() * 500));
    }
  }
}
function renderJoin(lounge = null) {
  if (joining) return;
  const rooms = lounge ? lounge.rooms || (lounge.exam ? [lounge.exam] : []) : [];
  const key = JSON.stringify(lounge ? [rooms,lounge.centerIntegrated] : null);
  if (renderedState === 'join' && lastLounge === key) return;
  const firstRender = renderedState !== 'join';
  if (!$('#join-form')) main.innerHTML = joinMarkup;
  examLayoutObserver?.disconnect();
  lastLounge = key; renderedState = 'join'; renderedId = '';
  const needsChoice = rooms.length > 1;
  const current = rooms.length === 1 ? rooms[0] : null;
  const canJoin = !lounge || rooms.length > 0;
  const lateBlocked = current?.state === 'running' && !current.allowLate;
  const status = $('#join-room-status');
  status.className = 'badge ' + (lounge && canJoin ? 'waiting' : 'quiet');
  status.textContent = !lounge ? 'اكتب بياناتك للدخول' : canJoin ? 'القاعة مفتوحة' : 'في انتظار فتح القاعة';
  $('#join-room-title').textContent = needsChoice ? 'اختر قاعتك للدخول' : current?.title || (lounge ? 'الامتحان هيظهر هنا قريب' : 'قاعة امتحانات مسار');
  const choice = $('#join-room-choice');
  const selectedRoom = $('[name=examId]', choice)?.value;
  choice.hidden = !needsChoice;
  choice.innerHTML = needsChoice ? `<label>قاعة الامتحان<select name="examId" required><option value="">اختر القاعة</option>${rooms.map(room => `<option value="${e(room.id)}" ${selectedRoom === room.id ? 'selected' : ''}>${e(room.label || room.title)}</option>`).join('')}</select></label><p class="help">يوجد أكثر من قاعة مفتوحة. اختار المجموعة والحصة الصحيحة قبل الدخول.</p>` : '';
  const notice = $('#join-room-notice');
  notice.hidden = canJoin && !lateBlocked;
  notice.textContent = lateBlocked ? 'الدخول للطلاب الجدد مقفول. لو المشرف استعاد دخولك، استخدم نفس الكود والرقم.' : 'المدرس لم يفتح القاعة بعد. الصفحة تتحدث تلقائيًا.';
  if (firstRender) renderDraftDownloads();
  if (lounge?.centerIntegrated) { renderCenterEntry(canJoin); return; }
  shortcut.render($('#join-form [name=name]').parentElement);
  $('#join-form button[type=submit]').disabled = !canJoin;
  $('#join-form').onsubmit = async event => {
    event.preventDefault();
    if (joining) return;
    const form = event.currentTarget; const button = $('button[type="submit"]', form);
    joining = true; shortcut.hide(); button.disabled = true; button.textContent = 'جارٍ الدخول…'; $('#join-error').hidden = true;
    try {
      const response = await joinRoom(formValues(form), button);
      $('#toast').hidden = true; setNetwork(true); acceptSession(response.session);
    } catch (error) {
      $('#join-error').textContent = error.message || 'تعذر الاتصال بجهاز الامتحان. ابقَ على شبكة السنتر وحاول تاني.';
      $('#join-error').hidden = false;
    } finally { joining = false; if (document.documentElement.contains(button)) { button.disabled = !canJoin; button.textContent = 'دخول قاعة السنتر ←'; shortcut.render($('[name=name]',form).parentElement); } }
  };
}
function renderCenterEntry(canJoin) {
  const form=$('#join-form'),name=$('[name=name]',form),phone=$('[name=phone]',form),code=$('[name=code]',form),button=$('button[type=submit]',form);
  name.required=false;name.disabled=true;name.parentElement.hidden=true;
  code.required=false;code.parentElement.hidden=true;
  phone.removeAttribute('pattern');phone.removeAttribute('minlength');phone.maxLength=25;
  $('.student-intro p').textContent='اكتب رقم الموبايل المسجل في السنتر، وهتظهر بياناتك قبل الدخول.';
  let official=null;
  let confirmation=$('#center-identity');
  if(!confirmation){confirmation=document.createElement('p');confirmation.id='center-identity';confirmation.className='notice';confirmation.hidden=true;button.before(confirmation);}
  const reset=event=>{official=null;confirmation.hidden=true;button.textContent='عرض بياناتي';if(event?.target?.name==='phone'||event?.target?.name==='examId'){code.value='';code.parentElement.hidden=true;}};
  form.oninput=reset;form.onchange=reset;reset();button.disabled=!canJoin;
  form.onsubmit=async event=>{
    event.preventDefault();if(joining)return;joining=true;button.disabled=true;$('#join-error').hidden=true;
    try {
      const supplied=formValues(form);
      if(!official){
        official=await api('/api/center/lookup',supplied);
        confirmation.textContent=official.name+' · الكود '+official.code;confirmation.hidden=false;
        button.textContent='دي بياناتي — دخول الامتحان';
      } else {
        const response=await joinRoom({examId:official.examId,phone:supplied.phone,code:official.code},button);
        $('#toast').hidden=true;setNetwork(true);acceptSession(response.session);
      }
    }catch(error){official=null;button.textContent='عرض بياناتي';if(error.status===409){code.parentElement.hidden=false;code.focus();}
      $('#join-error').textContent=error.message||'تعذر الاتصال بجهاز الامتحان. حاول مرة أخرى.';$('#join-error').hidden=false;
    }finally{joining=false;if(document.documentElement.contains(button))button.disabled=!canJoin;}
  };
}
async function expiredSession(message) {
  const firstNotice = !sessionExpired;
  sessionExpired = true;
  saveDraft();
  session = null;
  if (renderedState !== 'join') renderedState = '';
  if (firstNotice) toast(message, true);
  await pollLounge();
}
async function pollLounge() {
  const lounge = await api('/api/lounge');
  if (!session && !joining) renderJoin(lounge);
}
async function poll() {
  if (polling) return;
  polling = true; clearTimeout(pollTimer);
  let expectedSession = session;
  try {
    if (joining) return;
    if (sessionExpired) { await pollLounge(); setNetwork(true); return; }
    const measurement=screenMeasurement(session);
    const response = measurement ? await api('/api/presence',{visible:!document.hidden,screen:measurement}) : await api(`/api/session?visible=${document.hidden?0:1}`);
    if (joining || session !== expectedSession) return;
    setNetwork(true);
    if (response.session) acceptSession(response.session);
    else { session = null; expectedSession = null; await pollLounge(); }
    if (session?.state === 'running') flushAnswers();
  } catch (error) {
    if (joining || session !== expectedSession) return;
    if (error.status === 401) {
      try { await expiredSession(error.message); } catch { setNetwork(false); }
    } else setNetwork(false);
  } finally { polling = false; pollTimer = setTimeout(poll, 2200 + Math.random() * 800); }
}
window.addEventListener('beforeunload', event => {
  if (session?.state === 'running' && (dirty || pendingWrite || finishing)) {
    saveDraft(); event.preventDefault(); event.returnValue = '';
  }
});
window.addEventListener('online', () => {poll();flushAnswers();});
async function announceVisibility() {
 if (!session || session.state!=='running') return;
 const attemptId = session.id;
 if(document.hidden){saveDraft();flushAnswers();}
 try {
  const response=await api('/api/presence',{visible:!document.hidden,screen:screenMeasurement(session)});
  if(session?.id===attemptId&&response.session?.id===attemptId)acceptSession(response.session);
 }catch{/* Regular polling reports connectivity when the page returns. */}
}
document.addEventListener('visibilitychange',announceVisibility);
document.addEventListener('fullscreenchange',()=>{updateScreenNotice();announceVisibility();});
let screenResizeTimer;
window.addEventListener('resize',()=>{clearTimeout(screenResizeTimer);screenResizeTimer=setTimeout(announceVisibility,300);});
function updateScreenNotice(){
 const notice=$('#screen-guard-notice');if(!notice)return;
 const active=screenGuardActive(session);
 notice.hidden=!active;
 const button=$('#enter-exam-screen');
 button.hidden=!screenGuardBlocked(session)&&(!document.fullscreenEnabled||Boolean(document.fullscreenElement));
 button.textContent=screenGuardBlocked(session)?'نعم، افتح شاشة الامتحان':'ارجع لملء الشاشة';
}

window.addEventListener('pagehide',()=>{
  saveDraft(); flushAnswers();
  if (session?.state !== 'running') return;
  const presence = {visible:false};
  if (typeof fetch === 'function' && typeof Request === 'function' && 'keepalive' in Request.prototype) {
    const send = (path, payload) => fetch(path,{method:'POST',headers:{'Content-Type':'application/json','X-Exam-Request':'1'},
      body:JSON.stringify(payload),credentials:'same-origin',keepalive:true}).catch(()=>{});
    // A duplicate revision is idempotent; the draft remains until the server acknowledges it.
    if (pendingWrite && !session.paused && !conflict && !screenGuardBlocked(session)) send('/api/answers',pendingWrite);
    send('/api/presence',presence);
  } else api('/api/presence',presence).catch(()=>{});
});
setInterval(updateTimer, 500);
renderJoin();
diagnostics.ready();
poll();
