import {$, $$, api, escapeHtml as e, number, stateLabels, toast, copyText, downloadBlob, confirmAction} from './common.js';
import {renderEditor} from './editor.js';
import {renderStorage} from './storage.js';
import {QrCode} from './qr.js';

const main = $('#main');
let bootstrap, exams = [], selectedId, dashboard, view = 'room', roomTab = 'students', search = '', busy = false;
const request = (path, payload) => api(path, payload, bootstrap.token);
const currentExam = () => dashboard.exam;
let editorDirty = () => false;
let librarySearch = '', libraryState = 'all', rosterFilter = 'all';
async function canLeaveEditor() {
  return view !== 'editor' || !editorDirty() || await confirmAction('مغادرة التعديلات بدون حفظ؟', 'التغييرات التي كتبتها لم تُحفظ. ارجع واضغط حفظ الامتحان للاحتفاظ بها.');
}
window.addEventListener('beforeunload', event => { if (view === 'editor' && editorDirty()) { event.preventDefault(); event.returnValue = ''; } });

function statusBadge(state) { return `<span class="badge ${state}">${e(stateLabels[state])}</span>`; }
function qrMarkup(text) {
  const qr = QrCode.encodeText(text, QrCode.Ecc.MEDIUM);
  const cells = [];
  for (let y = 0; y < qr.size; y++) for (let x = 0; x < qr.size; x++) {
    if (qr.getModule(x, y)) cells.push(`M${x + 4},${y + 4}h1v1h-1z`);
  }
  return `<svg class="qr" viewBox="0 0 ${qr.size + 8} ${qr.size + 8}" role="img" aria-label="رمز الدخول لشبكة الامتحان"><rect width="100%" height="100%" fill="white"/><path d="${cells.join('')}" fill="#0A1D3D"/></svg>`;
}
async function runAction(button, action) {
  if (busy) return;
  busy = true; button.disabled = true;
  try { await action(); }
  catch (error) { toast(error.message || 'تعذر الاتصال بالجهاز', true); }
  finally { busy = false; if (button.isConnected) button.disabled = false; }
}
async function loadExam(examId) {
  selectedId = examId;
  dashboard = await request(`/api/exams/${examId}`);
  view = 'room'; roomTab = dashboard.exam.state === 'closed' ? 'results' : 'students'; search = ''; rosterFilter = 'all';
  paint();
}
async function refreshExams() { exams = (await request('/api/exams')).exams; }
function paint() {
  $$('.nav-item').forEach(button => button.classList.toggle('active', button.dataset.view === view));
  $('#page-location').textContent = ({room:'قاعة الامتحان', exams:'مكتبة الامتحانات', storage:'الإعدادات والبيانات', guide:'دليل التشغيل', editor:'تجهيز امتحان'})[view];
  if (view === 'storage') return renderStorage(main, bootstrap.token);
  if (view === 'guide') return renderGuide();
  if (view === 'exams') return renderExams();
  if (!dashboard) return renderWelcome();
  renderRoom();
}
function renderWelcome() {
  main.innerHTML = `<div class="welcome"><span class="badge quiet">أول امتحان يبدأ من هنا</span><h1>قاعة مسار، على جهازك.</h1><p>جهّز امتحانك، استقبل الطلاب على الشبكة المحلية، وتابع إجاباتهم من مكان واحد.</p><div class="actions"><button class="button" id="new-exam">إنشاء امتحان</button><button class="button secondary" id="demo-exam">ابدأ بامتحان تجريبي</button></div><div class="welcome-path"><div><b>١</b><strong>جهّز الأسئلة</strong><span>اختيارات ومقالي ووقت تحدده.</span></div><div><b>٢</b><strong>افتح القاعة</strong><span>رابط محلي وكود الطالب في السنتر.</span></div><div><b>٣</b><strong>ابدأ وتابع</strong><span>حفظ الإجابات وتصحيح الاختيارات.</span></div></div><p class="help">التجربة الجاهزة فيها ٤ أسئلة. الطالب يدخل بكوده الحالي في السنتر. لن تبدأ إلا لما تضغط «ابدأ الامتحان».</p></div>`;
  $('#new-exam').onclick = () => editExam();
  $('#demo-exam').onclick = event => runAction(event.currentTarget, async () => {
    const created = await request('/api/demo', {}); await refreshExams(); await loadExam(created.id);
  });
}
function renderExams() {
  main.innerHTML = `<div class="page-heading"><div><span class="context-label">كل جلسة، في مكانها</span><h1>مكتبة الامتحانات</h1><p>افتح جلسة، أكمل تجهيز مسودة، أو ارجع لنتائج امتحان سابق.</p></div><button class="button" id="new-exam">＋ إنشاء امتحان</button></div>
    <div class="library-summary"><span><b>${number(exams.length)}</b> امتحان محفوظ</span><span><b>${number(exams.filter(exam => ['waiting','running'].includes(exam.state)).length)}</b> جلسة مفتوحة</span><span><b>${number(exams.reduce((total, exam) => total + exam.submitted, 0))}</b> محاولة مسلّمة</span></div>
    <section class="panel library-panel"><div class="library-toolbar"><label class="search-field"><span class="sr-only">بحث في الامتحانات</span><input id="exam-search" placeholder="ابحث باسم الامتحان…" value="${e(librarySearch)}"></label><label class="filter-label">الحالة<select id="exam-state"><option value="all">كل الامتحانات</option>${Object.entries(stateLabels).map(([state,label]) => `<option value="${state}" ${libraryState === state ? 'selected' : ''}>${e(label)}</option>`).join('')}</select></label></div><div class="exam-list" id="exam-list"></div></section>`;
  $('#new-exam').onclick = () => editExam();
  $('#exam-search').oninput = event => { librarySearch = event.target.value; renderExamList(); };
  $('#exam-state').onchange = event => { libraryState = event.target.value; renderExamList(); };
  renderExamList();
}
function renderExamList() {
  const filtered = exams.filter(exam => (libraryState === 'all' || exam.state === libraryState) && exam.title.toLowerCase().includes(librarySearch.toLowerCase()));
  $('#exam-list').innerHTML = filtered.length ? filtered.map(exam => `<button class="exam-list-row" data-exam="${exam.id}"><div><strong>${e(exam.title)}</strong><small>${new Date(exam.createdAt * 1000).toLocaleDateString('ar-EG')} · ${number(exam.students)} طالب · ${number(exam.submitted)} تسليم</small></div>${statusBadge(exam.state)}<span class="exam-open-label">فتح الامتحان ←</span></button>`).join('') : '<div class="empty"><h2>لا توجد امتحانات في هذا العرض</h2><p>غيّر البحث أو الحالة، أو أنشئ امتحانك الأول.</p></div>';
  $$('[data-exam]').forEach(button => button.onclick = () => runAction(button, () => loadExam(button.dataset.exam)));
}
function renderRoom() {
  const exam = currentExam();
  const titles = {draft: 'جهّز امتحانك، ثم افتح القاعة.', waiting: 'القاعة جاهزة لاستقبال الطلاب.', running: 'الامتحان جارٍ. تابع القاعة.', closed: 'انتهى الامتحان. راجع النتائج.'};
  const studentUrl = bootstrap.studentUrls[0];
  main.innerHTML = `<div class="page-heading"><div><span class="context-label">${e(titles[exam.state])}</span><h1>${e(exam.config.title)}</h1><div class="exam-meta">${statusBadge(exam.state)}<span>${number(exam.config.questionCount)} أسئلة</span><span>${number(exam.config.minutes)} دقيقة</span><span>${exam.config.timerMode === 'shared' ? 'نهاية موحدة' : 'وقت مستقل لكل طالب'}</span></div></div>
    <div class="actions">${exam.state === 'draft' ? '<button class="button" data-state="publish">فتح قاعة الانتظار ←</button>' : exam.state === 'waiting' ? '<button class="button" data-state="start">ابدأ الامتحان للجميع ←</button>' : ''}
    ${['waiting','running'].includes(exam.state) ? '<button class="button secondary danger-text" data-state="close">إنهاء الجلسة</button>' : ''}
    ${exam.state === 'closed' ? '<button class="button secondary" id="duplicate-exam">إنشاء نسخة جديدة</button>' : ''}</div></div>
    <ol class="exam-steps" aria-label="مراحل الامتحان">${[['draft','تجهيز الأسئلة'],['waiting','استقبال الطلاب'],['running','حل الامتحان'],['closed','مراجعة النتائج']].map(([state,label], index) => `<li class="${exam.state === state ? 'current' : ''}"><span>${number(index + 1)}</span>${label}</li>`).join('')}</ol>
    <div class="room-overview room-state-${exam.state}"><section class="join-panel"><div><span class="context-label">دخول الطلاب</span><h2>رابط دخول الطلاب</h2><p>الطالب يتصل بالواي فاي، يمسح الرمز، ثم يدخل اسمه ورقمه وكوده.</p>${studentUrl ? `<label class="sr-only" for="student-url">رابط الطلاب</label><div class="link-field"><input id="student-url" readonly dir="ltr" value="${e(studentUrl)}"><button class="button secondary small" id="copy-link">نسخ الرابط</button></div>` : '<p class="inline-error">لا توجد شبكة محلية متصلة. اتصل بالراوتر وأعد تشغيل البرنامج.</p>'}<a class="text-link" href="${e(bootstrap.localStudentUrl)}" target="_blank" rel="noopener">فتح صفحة طالب على هذا الجهاز ↗</a><p class="help">شارك الرابط بعد اتصال الطلاب بنفس الشبكة. الفتح التلقائي غير مفعّل.</p></div>${studentUrl ? qrMarkup(studentUrl) : ''}</section>
    <section class="attendance-summary" aria-label="ملخص الحضور"><div><span>دخل القاعة</span><strong id="count-joined">٠</strong></div><div><span>متصل الآن</span><strong id="count-online">٠</strong></div><div><span>سلّم الامتحان</span><strong id="count-submitted">٠</strong></div><div><span>ينتظر تصحيح المقالي</span><strong id="count-pending">٠</strong></div></section></div>
    <section class="panel roster-panel"><div class="panel-toolbar"><div class="tabs" role="group" aria-label="عرض القاعة"><button data-tab="students" class="${roomTab === 'students' ? 'active' : ''}">الطلاب والحضور</button><button data-tab="questions" class="${roomTab === 'questions' ? 'active' : ''}">الأسئلة</button><button data-tab="results" class="${roomTab === 'results' ? 'active' : ''}">التصحيح والنتائج</button></div><span class="muted" id="roster-count"></span></div><div id="room-content"></div></section>`;
  $('#copy-link')?.addEventListener('click', () => copyText(studentUrl).catch(error => toast(error.message, true)));
  $$('[data-tab]').forEach(button => button.onclick = () => { roomTab = button.dataset.tab; renderRoom(); });
  $$('[data-state]').forEach(button => button.onclick = () => runAction(button, async () => {
    const action = button.dataset.state;
    const confirmed = action === 'close' ? await confirmAction('إنهاء الجلسة للجميع؟', 'ستُقفل المحاولات وتُعتمد آخر إجابات وصلت للجهاز. لا يمكن استئناف نفس الامتحان بعد الإنهاء.') : action === 'start' ? await confirmAction('بدء الامتحان الآن؟', 'سيبدأ الوقت لكل الطلاب الموجودين. تأكد أن الطلاب مستعدون.') : true;
    if (!confirmed) return;
    await request(`/api/exams/${exam.id}/${action}`, {});
    await refreshExams(); dashboard = await request(`/api/exams/${exam.id}`); renderRoom();
    toast(action === 'start' ? 'بدأ الامتحان' : action === 'publish' ? 'قاعة الانتظار مفتوحة الآن' : 'انتهى الامتحان وحُفظت الإجابات');
  }));
  $('#duplicate-exam')?.addEventListener('click', event => runAction(event.currentTarget, async () => {
    const created = await request(`/api/exams/${exam.id}/duplicate`, {}); await refreshExams(); await loadExam(created.id); editExam(true);
  }));
  renderRoomContent(); updateCounts();
}
function updateCounts() {
  const joined = dashboard.attempts.filter(attempt => attempt.joined_at);
  $('#count-joined').textContent = number(joined.length);
  $('#count-online').textContent = number(joined.filter(attempt => dashboard.serverTime - attempt.last_seen < 15).length);
  $('#count-submitted').textContent = number(joined.filter(attempt => attempt.submitted_at).length);
  $('#count-pending').textContent = number(joined.filter(attempt => attempt.submitted_at && attempt.pending > 0).length);
  $('#roster-count').textContent = `${number(dashboard.attempts.length)} طالب في الجلسة`;
}
function renderRoomContent() {
  const content = $('#room-content');
  if (roomTab === 'questions') return renderQuestions(content);
  content.innerHTML = `<div class="roster-tools"><label class="search-field"><span class="sr-only">بحث بالاسم أو الكود</span><input id="student-search" placeholder="ابحث باسم الطالب أو كوده…" value="${e(search)}"></label>
    ${roomTab === 'students' ? `<a class="button secondary small" download href="/api/exams/${selectedId}/students.csv">تنزيل كشف الطلاب ↓</a>` : `<a class="button secondary small" download href="/api/exams/${selectedId}/results.csv">تنزيل النتائج CSV ↓</a>`}</div>
    ${roomTab === 'results' ? `<p class="results-note">الاختيارات تُصحح تلقائيًا. راجع المقالي قبل طباعة التقارير. الدرجات لا تظهر للطلاب.</p>
    <section class="essay-batch-panel" aria-label="التصحيح الجماعي للمقالي"><div class="section-top"><div><span class="context-label">تصحيح جماعي</span><h2>المقالي لكل الطلاب</h2><p class="help">يصحح الإجابات المسلّمة غير المصححة فقط. التقدير صح أو غلط، ويمكنك تعديل كل درجة يدويًا.</p></div><button class="button" id="show-ai-setup" ${currentExam().state !== 'closed' ? 'disabled' : ''}>تصحيح المقالي للجميع ✦</button></div>
    <p class="help" id="essay-batch-status" role="status">جارٍ قراءة حالة التصحيح…</p>
    <form id="ai-grade-setup" hidden><label>مفتاح Gemini API<input name="apiKey" type="password" autocomplete="off" placeholder="الصق المفتاح هنا"></label><p class="help">يحتاج إنترنت على جهاز الإدارة. سيُرسل إلى Gemini نص السؤال والإجابة النموذجية وإجابة الطالب فقط. يُحفظ المفتاح في ملف .env داخل مجلد البيانات، ولا يُحفظ في SQLite. قد تُحسب طلبات Gemini على حسابك. <a href="https://ai.google.dev/gemini-api/docs/api-key" target="_blank" rel="noopener noreferrer">الحصول على مفتاح ↗</a></p><div class="actions"><button class="button" type="submit">ابدأ التصحيح</button><button class="button secondary" type="button" id="use-saved-ai-key">استخدم المفتاح المحفوظ</button><button class="button secondary" type="button" id="cancel-ai-setup">إلغاء</button></div></form></section>` : ''}
    <div class="roster-filters" role="group" aria-label="تصفية الطلاب">${[['all','الكل'],['active','لم يسلّم'],['submitted','تم التسليم'],['pending','بانتظار التصحيح']].map(([key,label])=>`<button class="filter-chip ${rosterFilter === key ? 'active' : ''}" data-roster-filter="${key}">${label}</button>`).join('')}</div><div class="table-scroll"><table><thead><tr><th>الطالب</th><th>كود الطالب</th><th>الحالة</th><th>${roomTab === 'results' ? 'الدرجة' : 'آخر ظهور'}</th><th>الإجراء</th></tr></thead><tbody id="student-rows"></tbody></table></div><div id="review-panel"></div>`;
  $('#student-search').oninput = event => { search = event.target.value; renderRows(); };
  $$('[data-roster-filter]').forEach(button => button.onclick = () => { rosterFilter = button.dataset.rosterFilter; $$('[data-roster-filter]').forEach(chip => chip.classList.toggle('active', chip === button)); renderRows(); });
  if (roomTab === 'results') bindAiPanel();
  renderRows();
}
function bindAiPanel() {
  const form = $('#ai-grade-setup');
  const launch = payload => runAction($('#show-ai-setup'), async () => {
    await request(`/api/exams/${selectedId}/essay-batch`, payload);
    form.elements.apiKey.value = ''; form.hidden = true;
    toast('بدأ تصحيح المقالي. يمكن متابعة التقدم هنا.');
    await updateAiPanel();
  });
  $('#show-ai-setup').onclick = () => {
    if ($('#show-ai-setup').dataset.keyConfigured === 'true') launch({});
    else { form.hidden = false; form.elements.apiKey.focus(); }
  };
  $('#cancel-ai-setup').onclick = () => { form.elements.apiKey.value = ''; form.hidden = true; };
  $('#use-saved-ai-key').onclick = () => launch({});
  form.onsubmit = event => {
    event.preventDefault(); launch({apiKey: form.elements.apiKey.value.trim()});
  };
  updateAiPanel().catch(error => { $('#essay-batch-status').textContent = error.message; });
}
async function updateAiPanel() {
  const examId = selectedId;
  const response = await request(`/api/exams/${examId}/essay-batch`);
  if (view !== 'room' || roomTab !== 'results' || examId !== selectedId || !$('#essay-batch-status')) return;
  const job = response.job;
  const pending = dashboard.attempts.reduce((sum, attempt) => sum + (attempt.pending || 0), 0);
  const running = job?.state === 'running';
  $('#show-ai-setup').dataset.keyConfigured = String(response.keyConfigured);
  $('#show-ai-setup').disabled = currentExam().state !== 'closed' || running || pending === 0;
  const summary = !job ? `${number(pending)} إجابة مقالية تنتظر التصحيح.` :
    `${{running:'التصحيح جارٍ',completed:'اكتمل التصحيح',partial:'اكتمل مع إجابات للمراجعة',interrupted:'توقف التصحيح'}[job.state] || 'حالة التصحيح'} · ${number(job.processed)} من ${number(job.total)} سؤال · ${number(job.graded)} صُحح · ${number(job.skipped)} كان مصححًا · ${number(job.needs_review + job.failed)} يحتاج مراجعة. ${e(job.message || '')}`;
  $('#essay-batch-status').innerHTML = `${summary} ${number(pending)} إجابة ما زالت بدون درجة.${running ? ` <button class="text-button" id="stop-ai-batch">إيقاف بعد السؤال الحالي</button>` : ''}`;
  $('#stop-ai-batch')?.addEventListener('click', event => runAction(event.currentTarget, async () => {
    await request(`/api/exams/${examId}/essay-batch/stop`, {});
    toast('سيقف التصحيح بعد السؤال الحالي');
  }));
}
function attemptStatus(attempt) {
  if (!attempt.joined_at) return ['quiet', 'لم يدخل بعد'];
  if (attempt.submitted_at) return ['complete', 'تم التسليم'];
  if (dashboard.serverTime - attempt.last_seen >= 15) return ['disconnected', 'غير متصل'];
  return ['waiting', currentExam().state === 'waiting' ? 'في الانتظار' : 'يحل الآن'];
}
function renderRows() {
  const attempts = dashboard.attempts.filter(attempt => (rosterFilter === 'all' || (rosterFilter === 'active' && !attempt.submitted_at) || (rosterFilter === 'submitted' && attempt.submitted_at) || (rosterFilter === 'pending' && attempt.submitted_at && attempt.pending > 0)) &&
    `${attempt.name || ''} ${attempt.code} ${attempt.phone || ''}`.toLowerCase().includes(search.toLowerCase()));
  $('#student-rows').innerHTML = attempts.length ? attempts.map(attempt => {
    const [statusClass, label] = attemptStatus(attempt);
    const seen = attempt.last_seen ? (dashboard.serverTime - attempt.last_seen < 15 ? 'الآن' : `منذ ${number(Math.floor((dashboard.serverTime - attempt.last_seen) / 60))} دقيقة`) : '—';
    const score = attempt.submitted_at ? `${number(attempt.score)} / ${number(attempt.maximum)}${attempt.pending ? ` <small>متبقي ${number(attempt.pending)} مقالي</small>` : ''}` : 'لم يسلّم';
    return `<tr><td><strong>${e(attempt.name || 'طالب')}</strong><small dir="ltr">${e(attempt.phone || 'لم تُسجل بياناته')}</small></td><td><button class="code-button" data-copy="${attempt.code}" title="نسخ الكود">${attempt.code}</button></td><td><span class="badge ${statusClass}">${label}</span></td><td>${roomTab === 'results' ? score : seen}</td><td>${roomTab === 'results' && attempt.submitted_at ? `<button class="text-button" data-review="${attempt.id}">مراجعة وتصحيح ←</button>` : attempt.joined_at && !attempt.submitted_at ? `<button class="text-button" data-reset="${attempt.id}">استعادة الدخول</button>` : '<span class="muted">—</span>'}</td></tr>`;
  }).join('') : `<tr><td colspan="5" class="empty">${search || rosterFilter !== 'all' ? 'لا يوجد طلاب مطابقون للبحث أو التصفية.' : 'الطالب يظهر هنا بعد دخوله باسمه ورقمه وكوده في السنتر.'}</td></tr>`;
  $$('[data-copy]').forEach(button => button.onclick = () => copyText(button.dataset.copy).catch(error => toast(error.message, true)));
  $$('[data-review]').forEach(button => button.onclick = () => renderReview(button.dataset.review));
  $$('[data-reset]').forEach(button => button.onclick = () => runAction(button, async () => {
    if (!await confirmAction('استعادة دخول الطالب؟', 'سيتم تسجيل خروج الجلسة القديمة. يدخل الطالب بنفس الكود والرقم، وتبقى الإجابات والوقت كما هما.')) return;
    await request(`/api/attempts/${button.dataset.reset}/reset-login`, {}); toast('يمكن للطالب الدخول مجددًا بنفس الكود والرقم');
  }));
}
function renderQuestions(container) {
  container.innerHTML = `<div class="section-top padded"><p class="muted">${currentExam().state === 'draft' ? 'راجع الإجابات والدرجات قبل فتح القاعة.' : 'الأسئلة مقفولة لهذه الجلسة.'}</p>${currentExam().state === 'draft' ? '<button class="button secondary small" id="edit-exam">تعديل الأسئلة والإعدادات</button>' : ''}</div><div class="question-previews">${currentExam().config.questions.map((question, index) => `<article><div class="section-top"><h3>${number(index + 1)}. ${e(question.text)}</h3><span class="badge quiet">${number(question.points)} درجات</span></div><p class="model-answer-preview">الإجابة النموذجية: ${e(question.kind === 'mcq' ? question.options[question.correct] : question.modelAnswer)}</p></article>`).join('')}</div>`;
  $('#edit-exam')?.addEventListener('click', () => editExam(true));
}
function renderReview(attemptId) {
  const attempt = dashboard.attempts.find(attempt => attempt.id === attemptId);
  const questions = attempt.questionIds.map(id => currentExam().config.questions.find(question => question.id === id));
  const panel = $('#review-panel');
  panel.innerHTML = `<div class="review-heading"><div><h2>ورقة ${e(attempt.name)}</h2><p>الدرجة المصححة: ${number(attempt.score)} من ${number(attempt.maximum)}</p></div><div class="actions">${attempt.pending ? '<span class="badge waiting">أكمل المقالي لفتح التقرير</span>' : `<a class="button secondary small" href="/report/${attempt.id}">تقرير التصحيح / PDF ↗</a>`}<button class="text-button" id="close-review">إغلاق</button></div></div>${questions.map((question, index) => {
    const answer = attempt.answers[question.id]; const grade = attempt.grades[question.id];
    return `<article class="review-question"><h3>${number(index + 1)}. ${e(question.text)}</h3><p class="answer-text"><strong>إجابة الطالب:</strong> ${e(question.kind === 'mcq' ? (answer == null ? 'لم يجب' : question.options[answer]) : answer || 'لم يجب')}</p><p class="model-answer-preview"><strong>الإجابة النموذجية:</strong> ${e(question.kind === 'mcq' ? question.options[question.correct] : question.modelAnswer)}</p>${question.kind === 'essay' ? `<form class="grade-form" data-question="${question.id}"><label>الدرجة من ${number(question.points)}<input name="score" type="number" required min="0" max="${question.points}" step="0.5" value="${grade?.score ?? ''}"></label><label>ملاحظة التصحيح<input name="feedback" maxlength="2000" value="${e(grade?.feedback || '')}"></label><button class="button small" type="submit">حفظ الدرجة</button></form>` : `<span class="badge complete">${number(grade.score)} / ${number(question.points)}</span>`}</article>`;
  }).join('')}`;
  $('#close-review').onclick = () => { panel.innerHTML = ''; };
  $$('.grade-form').forEach(form => form.onsubmit = event => {
    event.preventDefault(); runAction($('button', form), async () => {
      await request(`/api/attempts/${attemptId}/grade`, {questionId: form.dataset.question,
        score: Number(form.elements.score.value), feedback: form.elements.feedback.value});
      dashboard = await request(`/api/exams/${selectedId}`); updateCounts(); renderRows(); renderReview(attemptId); updateAiPanel(); toast('تم حفظ التصحيح');
    });
  });
  panel.scrollIntoView({behavior: 'smooth', block: 'start'});
}
function editExam(existing = false) {
  view = 'editor';
  $$('.nav-item').forEach(button => button.classList.remove('active'));
  $('#page-location').textContent = 'تجهيز امتحان';
  editorDirty = renderEditor(main, existing ? currentExam().config : null, async config => {
    const created = await request(existing ? `/api/exams/${selectedId}/save` : '/api/exams', config);
    await refreshExams(); await loadExam(created.id); toast('تم حفظ الامتحان');
  }, async () => { if (await canLeaveEditor()) { view = 'room'; paint(); } });
}
function renderGuide() {
  main.innerHTML = `<div class="page-heading"><div><span class="context-label">قبل أول تجربة</span><h1>جهّز القاعة بخطوات بسيطة.</h1><p>ابدأ بعدد قليل من الموبايلات، ثم زوّد العدد تدريجيًا.</p></div></div><section class="panel guide"><ol><li><h2>وصّل الكمبيوتر والطلاب بنفس الشبكة</h2><p>يفضل توصيل الكمبيوتر بكابل شبكة. اسمح لبرنامج مسار باستقبال الاتصالات على الشبكة الخاصة إذا طلب جدار الحماية ذلك. لا تفتح أي منافذ على الإنترنت.</p></li><li><h2>جهّز الامتحان وافتح القاعة</h2><p>الطالب يكتب اسمه ورقمه وكوده الحالي في السنتر بنفسه. الكود لا يُراجع مقابل كشف مسبق، ويُمنع تكراره داخل نفس الامتحان. افتح قاعة الانتظار، ثم شارك رابط الطلاب أو QR الظاهر في لوحة القاعة.</p></li><li><h2>ابدأ وتابع الحفظ</h2><p>راقب عدد المتصلين، ثم ابدأ الامتحان. الطالب يشوف حالة حفظ الإجابات. لو اتصاله انقطع، يرجع بنفس المتصفح؛ ولو غيّر جهازه استخدم «استعادة الدخول».</p></li><li><h2>راجع واحتفظ بنسخة</h2><p>بعد انتهاء الامتحان تُصحح الاختيارات تلقائيًا، ويمكنك تصحيح كل المقالات عبر Gemini أو يدويًا وطباعة تقرير كل طالب أو حفظه PDF. نزّل نسخة احتياطية لقرص آخر بعد التجربة.</p></li></ol><div class="notice"><strong>حدود نسخة التجربة</strong><p>الفتح التلقائي يحتاج راوتر يدعم بوابة الدخول. تصحيح Gemini يحتاج إنترنت على جهاز الإدارة، وواتساب لم يُربط بعد. الكمبيوتر يجب أن يظل مفتوحًا ولا يدخل وضع النوم. إعادة التشغيل لا تعيد الوقت؛ تُحسب المدة المنقضية. عند انتهاء الوقت أو إنهاء المدرس، يُعتمد آخر حفظ وصل للجهاز.</p></div></section>`;
}
async function poll() {
  try {
    if (view === 'room' && selectedId && !busy) {
      const requestedId = selectedId;
      const fresh = await request(`/api/exams/${requestedId}`);
      if (view !== 'room' || requestedId !== selectedId || busy) return;
      const stateChanged = fresh.exam.state !== dashboard.exam.state;
      dashboard = fresh;
      if (stateChanged) { await refreshExams(); renderRoom(); }
      else { updateCounts(); if ($('#student-rows')) renderRows(); if (roomTab === 'results') await updateAiPanel(); }
    }
    else await request('/api/status');
    $('#admin-connection').classList.remove('offline');
    $('#admin-connection').innerHTML = '<span class="status-dot"></span> متصل بجهاز الإدارة';
  } catch (error) {
    $('#admin-connection').classList.add('offline');
    $('#admin-connection').textContent = error.status === 401 ? 'أعد تحميل الصفحة بعد إعادة التشغيل' : 'انقطع الاتصال بجهاز الإدارة';
  } finally { setTimeout(poll, 3000); }
}
async function start() {
  try {
    bootstrap = await api('/api/bootstrap'); await refreshExams();
    $$('.nav-item').forEach(button => button.onclick = () => runAction(button, async () => {
      if (!await canLeaveEditor()) return;
      view = button.dataset.view; if (view === 'exams') await refreshExams(); paint();
    }));
    $('#backup').onclick = event => runAction(event.currentTarget, async () => {
      const response = await fetch('/api/backup', {method: 'POST', headers: {'Content-Type': 'application/json', 'X-Exam-Request': '1', 'X-Admin-Token': bootstrap.token}, body: '{}'});
      if (!response.ok) throw new Error((await response.json()).error);
      downloadBlob(await response.blob(), `massar-backup-${new Date().toISOString().slice(0,10)}.sqlite3`); toast('تم تجهيز النسخة الاحتياطية للتنزيل');
    });
    if (exams.length) await loadExam((exams.find(exam => ['waiting','running'].includes(exam.state)) || exams[0]).id);
    else paint();
    poll();
  } catch (error) {
    main.innerHTML = `<div class="empty"><h1>تعذر فتح الإدارة</h1><p>${e(error.message)}</p><a class="button" href="/">إعادة المحاولة</a></div>`;
  }
}
start();
