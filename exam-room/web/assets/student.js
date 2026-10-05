import {$, $$, api, escapeHtml as e, number, timeLabel, toast} from './common.js';

import {screenGuardActive,screenGuardBlocked,enterExamScreen,screenMeasurement} from './screen-guard.js';

const main = $('#student-main');
let session, answers = {}, serverRevision = 0, pendingWrite = null, saving = false;
let finishing = false, dirty = false, conflict = false, clockOffset = 0, saveTimer, online = true, joining = false;
let sessionExpired = false, examLayoutObserver;
let renderedState = '', renderedId = '', lastLounge = '', questionIndex = 0;
const draftKey = id => `massar-exam-draft:${id}`;

function saveDraft() {
  if (!session || session.state !== 'running') return;
  try {
    localStorage.setItem(draftKey(session.id), JSON.stringify({answers, serverRevision, pendingWrite, finishing, questionIndex}));
  } catch {
    toast('التخزين المؤقت على هذا المتصفح غير متاح. تأكد من ظهور «تم الحفظ على الجهاز».', true);
  }
}
function recoverDraft(nextSession) {
  answers = structuredClone(nextSession.answers); serverRevision = nextSession.revision;
  pendingWrite = null; finishing = false; dirty = false; conflict = false; questionIndex = 0;
  let saved;
  try { saved = JSON.parse(localStorage.getItem(draftKey(nextSession.id)) || 'null'); }
  catch { return; }
  if (!saved) return;
  if (Number.isInteger(saved.questionIndex)) questionIndex = Math.max(0, Math.min(saved.questionIndex, nextSession.questions.length - 1));
  const acknowledged = saved.pendingWrite?.revision === nextSession.revision &&
    JSON.stringify(saved.pendingWrite.answers) === JSON.stringify(nextSession.answers);
  if (saved.serverRevision !== nextSession.revision && !acknowledged) {
    toast('تم تحميل آخر إجابات محفوظة على جهاز الإدارة.'); return;
  }
  answers = saved.answers; pendingWrite = acknowledged ? null : saved.pendingWrite;
  finishing = saved.finishing;
  dirty = JSON.stringify(answers) !== JSON.stringify(nextSession.answers);
}
function setNetwork(connected) {
  online = connected;
  const banner = $('#network-alert');
  banner.hidden = connected;
  banner.textContent = 'الاتصال بجهاز الامتحان منقطع. ابقَ على نفس الواي فاي؛ سنحاول إرسال الإجابات عند عودة الاتصال. آخر حفظ وصل للجهاز هو المعتمد عند انتهاء الوقت.';
  updateSaveStatus();
}
function acceptSession(nextSession, fromWrite = false) {
  const changedAttempt = session?.id !== nextSession.id;
  const becameRunning = session?.state !== 'running' && nextSession.state === 'running';
  clockOffset = nextSession.serverTime * 1000 - Date.now();
  session = nextSession; sessionExpired = false;
  if (changedAttempt || becameRunning) recoverDraft(nextSession);
  if (!fromWrite && nextSession.state === 'running' && !pendingWrite && !dirty && !saving && nextSession.revision > serverRevision) {
    answers = structuredClone(nextSession.answers); serverRevision = nextSession.revision;
    renderedState = '';
  }
  if (nextSession.state === 'submitted') {
    finishing = false; dirty = false; pendingWrite = null;
    try { localStorage.removeItem(draftKey(nextSession.id)); } catch { /* No draft to remove in private storage. */ }
  }
  if (renderedState !== nextSession.state || renderedId !== nextSession.id) renderSession();
  if (nextSession.state === 'running') {freezeInputs(); if(remainingSeconds()>0) $('#submit-exam').textContent='مراجعة وتسليم ←';}
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
    $('.student-finish').append(nextButton);
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
    ${session.questions.map((question, index) => questionMarkup(question, index)).join('')}
    <p class="help paper-note">تأكد من ظهور «تم الحفظ على الجهاز» قبل التسليم. عند انتهاء الوقت تُعتمد آخر إجابات وصلت لجهاز الإدارة.</p></div>
    <footer class="exam-submit-bar step-controls"><button class="button secondary" id="previous-question">→ السابق</button><button class="button" id="next-question">التالي ←</button><button class="button" id="submit-exam" hidden>مراجعة وتسليم ←</button></footer>`;
  $$('[data-answer]').forEach(input => input.addEventListener(input.type === 'radio' ? 'change' : 'input', () => {
    answers[input.dataset.answer] = input.type === 'radio' ? Number(input.value) : input.value;
    dirty = true; saveDraft(); updateProgress(); updateSaveStatus();
    clearTimeout(saveTimer); saveTimer = setTimeout(flushAnswers, 650);
  }));
  $('#enter-exam-screen').onclick = async()=>{try{const supported=await enterExamScreen();$('#screen-support').textContent=supported?'':'هذا المتصفح لا يدعم ملء الشاشة. سنراقب مساحة العرض فقط؛ أبلغ المشرف.';await announceVisibility();freezeInputs();updateScreenNotice();}catch{toast('تعذر فتح ملء الشاشة. افتح الرابط في متصفح يدعمه أو راجع المشرف.',true);}};
  $('#submit-exam').onclick = requestSubmission;
  $('#previous-question').onclick = () => moveQuestion(questionIndex - 1);
  $('#next-question').onclick = () => moveQuestion(questionIndex + 1);
  $$('[data-nav]').forEach(button => button.onclick = () => moveQuestion(Number(button.dataset.index)));
  showQuestion(); updateProgress(); freezeInputs();
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
  examLayoutObserver = new ResizeObserver(update);
  examLayoutObserver.observe(controls);
  examLayoutObserver.observe(header);
}
function moveQuestion(index) {
  if (index < 0 || index >= session.questions.length || finishing) return;
  questionIndex = index;
  saveDraft(); showQuestion(); flushAnswers();
  const heading = $('.student-question:not([hidden]) h2');
  heading.focus({preventScroll: true});
  heading.scrollIntoView({block: 'start', behavior: 'instant'});
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
  if (!pendingWrite) {
    pendingWrite = {answers: structuredClone(answers), revision: serverRevision + 1, submit: finishing};
    saveDraft();
  }
  saving = true; updateSaveStatus(); freezeInputs();
  try {
    const sent = pendingWrite;
    const response = await api('/api/answers', sent, undefined, {keepalive:true});
    serverRevision = response.session.revision;
    pendingWrite = null;
    dirty = JSON.stringify(answers) !== JSON.stringify(sent.answers);
    setNetwork(true); acceptSession(response.session, true);
    if (session.state === 'running') saveDraft();
  } catch (error) {
    if (error.status === 423) {
      const response=await api(`/api/session?visible=${document.hidden?0:1}`);acceptSession(response.session);
    } else if (error.status === 409 || error.status === 400) {
      conflict = true;
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
  dialog.returnValue = 'cancel'; dialog.showModal();
  const approved = await new Promise(resolve => dialog.addEventListener('close', () => resolve(dialog.returnValue === 'yes'), {once: true}));
  if (!approved || session?.state !== 'running') return;
  finishing = true; saveDraft(); freezeInputs(); await flushAnswers();
}
function renderJoin(lounge) {
  if (joining) return;
  const rooms = lounge.rooms || (lounge.exam ? [lounge.exam] : []);
  const needsChoice = rooms.length > 1;
  const current = rooms.length === 1 ? rooms[0] : null;
  const key = JSON.stringify(rooms);
  if (renderedState === 'join' && lastLounge === key) return;
  const oldForm = $('#join-form');
  const typed = oldForm ? Object.fromEntries(new FormData(oldForm)) : {};
  examLayoutObserver?.disconnect();
  lastLounge = key; renderedState = 'join'; renderedId = '';
  const canJoin = rooms.length > 0;
  const lateBlocked = current?.state === 'running' && !current.allowLate;
  main.innerHTML = `<section class="join-layout"><div class="student-intro"><span class="badge quiet">خطوتك الأولى</span><h1>أهلًا بيك<br>في قاعة مسار.</h1><p>اكتب اسمك ورقمك وكودك المسجل في السنتر. جاهز؟ خلّينا نبدأ.</p><div class="intro-rule"></div><p class="help">الامتحان يعمل على شبكة المكان.<br>خليك متصل بنفس الواي فاي لحد ما تسلّم.</p></div><form id="join-form" class="join-form"><span class="badge ${canJoin ? 'waiting' : 'quiet'}">${canJoin ? 'القاعة مفتوحة' : 'في انتظار فتح القاعة'}</span><h2>${e(needsChoice ? 'اختر قاعتك للدخول' : current?.title || 'الامتحان هيظهر هنا قريب')}</h2>${needsChoice ? `<label>قاعة الامتحان<select name="examId" required><option value="">اختر القاعة</option>${rooms.map(room => `<option value="${e(room.id)}" ${typed.examId === room.id ? 'selected' : ''}>${e(room.label || room.title)}</option>`).join('')}</select></label><p class="help">يوجد أكثر من قاعة مفتوحة. اختار المجموعة والحصة الصحيحة قبل الدخول.</p>` : ''}${!canJoin || lateBlocked ? `<p class="notice">${current ? 'الدخول للطلاب الجدد مقفول. لو المشرف استعاد دخولك، استخدم نفس الكود والرقم.' : 'المدرس لم يفتح القاعة بعد. الصفحة تتحدث تلقائيًا.'}</p>` : ''}<label>اسمك بالكامل<input name="name" autocomplete="name" minlength="2" maxlength="100" required value="${e(typed.name || '')}" placeholder="اكتب اسمك"></label><label>رقم الموبايل<input name="phone" inputmode="tel" autocomplete="tel" minlength="11" maxlength="11" pattern="[0-9٠-٩۰-۹]{11}" required value="${e(typed.phone || '')}" placeholder="01xxxxxxxxx" dir="ltr"></label><label>كود الطالب<input class="entry-code" name="code" autocomplete="off" autocapitalize="characters" spellcheck="false" maxlength="40" required value="${e(typed.code || '')}" placeholder="كودك في السنتر" dir="ltr"></label><p id="join-error" role="alert" class="inline-error" hidden></p><button class="button full" type="submit" ${!canJoin ? 'disabled' : ''}>دخول قاعة السنتر ←</button><p class="help">لك محاولة واحدة لكل امتحان. اكتب كودك بدقة، واستخدم نفس المتصفح لو انقطع الاتصال قبل التسليم.</p></form></section>`;
  $('#join-form').onsubmit = async event => {
    event.preventDefault(); const form = event.currentTarget; const button = $('button', form);
    joining = true; button.disabled = true; button.textContent = 'جارٍ الدخول…'; $('#join-error').hidden = true;
    try {
      const response = await api('/api/join', Object.fromEntries(new FormData(form)));
      $('#toast').hidden = true; setNetwork(true); acceptSession(response.session);
    } catch (error) {
      $('#join-error').textContent = error.status ? error.message : 'تعذر الاتصال. تأكد من الواي فاي وحاول تاني.';
      $('#join-error').hidden = false;
    } finally { joining = false; if (button.isConnected) { button.disabled = !canJoin; button.textContent = 'دخول قاعة السنتر ←'; } }
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
async function pollLounge() { renderJoin(await api('/api/lounge')); }
async function poll() {
  try {
    if (joining) return;
    if (sessionExpired) { await pollLounge(); setNetwork(true); return; }
    const previousSession = session;
    const measurement=screenMeasurement(session);
    const response = measurement ? await api('/api/presence',{visible:!document.hidden,screen:measurement}) : await api(`/api/session?visible=${document.hidden?0:1}`);
    if (joining || session !== previousSession) return;
    setNetwork(true);
    if (response.session) acceptSession(response.session);
    else { session = null; await pollLounge(); }
    if (session?.state === 'running') flushAnswers();
  } catch (error) {
    if (error.status === 401) {
      try { await expiredSession(error.message); } catch { setNetwork(false); }
    } else setNetwork(false);
  } finally { setTimeout(poll, 2200 + Math.random() * 800); }
}
window.addEventListener('beforeunload', event => {
  if (session?.state === 'running' && (dirty || pendingWrite || finishing)) {
    saveDraft(); event.preventDefault(); event.returnValue = '';
  }
});
window.addEventListener('online', () => flushAnswers());
async function announceVisibility() {
 if (!session || session.state!=='running') return;
 if(document.hidden){saveDraft();flushAnswers();}
 try {
  const response=await fetch('/api/presence',{method:'POST',headers:{'Content-Type':'application/json','X-Exam-Request':'1'},body:JSON.stringify({visible:!document.hidden,screen:screenMeasurement(session)}),keepalive:true,cache:'no-store'});
  if(response.ok){const payload=await response.json();if(payload.session&&payload.session.id===session?.id)acceptSession(payload.session);}
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

window.addEventListener('pagehide',()=>{saveDraft();flushAnswers();if(session?.state==='running')fetch('/api/presence',{method:'POST',headers:{'Content-Type':'application/json','X-Exam-Request':'1'},body:JSON.stringify({visible:false}),keepalive:true}).catch(()=>{});});
setInterval(updateTimer, 500);
poll();
