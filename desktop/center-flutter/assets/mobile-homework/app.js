'use strict';
const byId = id => document.getElementById(id);
const token = location.hash.slice(1) || sessionStorage.getItem('massar-homework-token');
if (location.hash) {
  if (sessionStorage.getItem('massar-homework-token') !== token) sessionStorage.removeItem('massar-homework-pending');
  sessionStorage.setItem('massar-homework-token', token);
  history.replaceState(null, '', location.pathname);
}
let selected = null, busy = false, controls = null, cameraEpoch = 0;
let pending = JSON.parse(sessionStorage.getItem('massar-homework-pending') || 'null');
const reader = new ZXingBrowser.BrowserMultiFormatReader();
function notice(message, kind = '') {
  byId('notice').textContent = message;
  byId('notice').className = kind;
}
async function api(path, body) {
  const response = await fetch('/mobile/' + path, {
    method: body ? 'POST' : 'GET', cache: 'no-store',
    headers: {'X-Massar-Mobile-Token': token || '', 'Content-Type': 'application/json'},
    body: body ? JSON.stringify(body) : undefined,
    signal: AbortSignal.timeout(20000)
  });
  const reply = await response.json();
  if (!response.ok) {
    const rejection = new Error(reply.message || 'تعذر الاتصال بالهوست.');
    rejection.definitive = response.status === 400;
    throw rejection;
  }
  return reply;
}
function availability() {
  byId('show').disabled = busy || !!pending;
  byId('code').disabled = busy || !!pending;
  byId('camera').disabled = busy || !!pending;
  byId('photo').disabled = busy || !!pending;
  byId('confirm').disabled = busy || (!pending && (!selected?.present || selected.missing));
  byId('confirm').textContent = pending ? 'إعادة تأكيد نفس التسجيل' : 'تأكيد: ماعملش الواجب';
}
function stopCamera() {
  cameraEpoch++;
  controls?.stop(); controls = null;
  byId('stop').hidden = true;
  byId('camera').hidden = false;
}
function showStudent(student) {
  selected = student;
  for (const [id, text] of Object.entries({name:student.name,'student-code':student.code,group:student.group,attendance:student.present ? 'حاضر في الحصة' : 'غير حاضر في الحصة',status:student.status})) byId(id).textContent = text;
  byId('student').hidden = false;
  availability();
}
async function lookup(code) {
  if (busy || pending) return;
  stopCamera(); selected = null; byId('student').hidden = true;
  busy = true; availability(); notice('جارٍ مراجعة بيانات الطالب…');
  try {
    const student = await api('lookup', {code}); showStudent(student);
    notice(!student.present ? 'لازم تسجّل حضور الطالب في الحصة المختارة أولًا، ثم تعرض بياناته من جديد.' : student.missing ? 'الطالب مسجّل بالفعل: ماعملش الواجب.' : 'راجع بيانات الطالب، ثم اضغط تأكيد.', !student.present ? 'error' : '');
  } catch (error) { notice(error.message || 'تعذر الاتصال بالهوست.', 'error'); }
  finally { busy = false; availability(); }
}
byId('lookup').addEventListener('submit', event => {event.preventDefault(); lookup(byId('code').value.trim());});
byId('code').addEventListener('input', () => {selected = null; byId('student').hidden = true; availability();});
byId('confirm').addEventListener('click', async () => {
  if (busy || (!pending && !selected?.present)) return;
  stopCamera();
  if (!pending) {
    pending = {studentId:selected.id, requestId:crypto.randomUUID()};
    sessionStorage.setItem('massar-homework-pending', JSON.stringify(pending));
  }
  busy = true; availability(); notice('جارٍ تأكيد الحفظ على الهوست…');
  try {
    await api('confirm', pending);
    pending = null; sessionStorage.removeItem('massar-homework-pending');
    selected = null; byId('student').hidden = true; byId('code').value = '';
    notice('تم التسجيل: ماعملش الواجب. جاهز للطالب التالي.', 'success');
  } catch (error) {
    if (error.definitive) {
      pending = null; sessionStorage.removeItem('massar-homework-pending');
      selected = null; byId('student').hidden = true;
      notice(error.message, 'error');
    } else notice((error.message || 'الاتصال انقطع.') + ' اضغط إعادة تأكيد نفس التسجيل؛ لن يتكرر الحفظ.', 'error');
  }
  finally {busy = false; availability(); if (!pending) byId('code').focus();}
});
byId('camera').addEventListener('click', async () => {
  if (!navigator.mediaDevices?.getUserMedia || !window.isSecureContext) {
    notice('الكاميرا تحتاج تثبيت شهادة مسار والثقة بها على الموبايل. يمكنك كتابة الكود أو قراءة صورة الكارت.', 'error'); return;
  }
  stopCamera(); const epoch = cameraEpoch; byId('camera').disabled = true;
  try {
    const started = await reader.decodeFromConstraints({video:{facingMode:{ideal:'environment'}}}, byId('video'), (barcode, error, scanner) => {
      if (barcode && !busy && !pending) {scanner.stop(); lookup(barcode.getText());}
    });
    if (cameraEpoch !== epoch) {started.stop(); return;}
    controls = started;
    byId('stop').hidden = false; byId('camera').hidden = true;
  } catch (error) {notice('تعذر فتح الكاميرا. اسمح للمتصفح باستخدامها أو اكتب الكود.', 'error');}
  finally {availability();}
});
byId('stop').addEventListener('click', stopCamera);
byId('photo').addEventListener('change', async event => {
  const photo = event.target.files[0];
  if (!photo || busy || pending) return;
  stopCamera(); const url = URL.createObjectURL(photo);
  try {const barcode = await reader.decodeFromImageUrl(url); await lookup(barcode.getText());}
  catch (error) {notice('الباركود مش واضح في الصورة. جرّب صورة أقرب أو اكتب الكود.', 'error');}
  finally {URL.revokeObjectURL(url); event.target.value = '';}
});
window.addEventListener('pagehide', stopCamera);
async function initialize() {
  availability();
  try {
    const scope = await api('context');
    byId('scope').textContent = `${scope.group} · ${scope.session} · ${scope.homework}`;
    if (pending) {
      byId('student').hidden = false; byId('name').textContent = 'تسجيل سابق يحتاج تأكيد النتيجة';
      notice('اضغط إعادة تأكيد نفس التسجيل قبل الطالب التالي.', 'error');
    }
  } catch (error) {busy = true; availability(); notice(error.message, 'error');}
}
initialize();
