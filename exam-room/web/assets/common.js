export const $ = (selector, parent = document) => parent.querySelector(selector);
export const $$ = (selector, parent = document) => Array.prototype.slice.call(parent.querySelectorAll(selector));
export const escapeHtml = (text = '') => String(text).replace(/[&<>"']/g, character => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[character]));
export const number = value => new Intl.NumberFormat('ar-EG', {maximumFractionDigits: 2}).format(value);
const padTime = value => value < 10 ? '0' + value : String(value);
export const timeLabel = seconds => `${padTime(Math.floor(Math.max(0, seconds) / 60))}:${padTime(Math.floor(Math.max(0, seconds) % 60))}`;
export const stateLabels = {draft: 'مسودة', waiting: 'قاعة الانتظار مفتوحة', running: 'الامتحان جارٍ', closed: 'انتهى الامتحان'};
let toastTimeout;
export function toast(message, error = false) {
  const element = $('#toast');
  if (!element.hidden && element.dataset.message === message) return;
  element.dataset.message = message;
  const text = document.createElement('span'); text.textContent = message;
  const close = document.createElement('button'); close.type = 'button'; close.className = 'toast-close';
  close.setAttribute('aria-label', 'إغلاق الرسالة'); close.textContent = '×';
  close.onclick = () => {clearTimeout(toastTimeout);element.hidden = true;};
  element.textContent = '';
  element.appendChild(text); element.appendChild(close);
  element.className = `toast${error ? ' error' : ''}`;
  element.hidden = false;
  clearTimeout(toastTimeout);
  toastTimeout = setTimeout(() => { element.hidden = true; }, 6000);
}
export function api(path, payload, token, options = {}) {
  return new Promise((resolve, reject) => {
    const request = new XMLHttpRequest();
    request.open(payload === undefined ? 'GET' : 'POST', path, true);
    request.withCredentials = true;
    request.timeout = options.timeoutMs || 12000;
    request.setRequestHeader('X-Exam-Request', '1');
    if (token) request.setRequestHeader('X-Admin-Token', token);
    if (payload !== undefined) request.setRequestHeader('Content-Type', 'application/json');
    request.onerror = () => reject(new Error('تعذر الوصول لجهاز الامتحان. ابقَ على شبكة الامتحان وحاول مرة أخرى.'));
    request.ontimeout = () => reject(new Error('انتهت مهلة الاتصال بجهاز الامتحان. حاول مرة أخرى.'));
    request.onabort = () => reject(new Error('انقطع طلب الاتصال قبل تأكيد الاستلام.'));
    request.onload = () => {
      let response;
      try { response = JSON.parse(request.responseText); }
      catch {
        const error = new Error('وصل رد غير مكتمل من جهاز الامتحان. حاول مرة أخرى.');
        if (request.status >= 400) error.status = request.status;
        reject(error); return;
      }
      if (request.status < 200 || request.status >= 300) {
        const error = new Error(response.error || 'تعذر إكمال العملية');
        error.status = request.status; reject(error); return;
      }
      resolve(response);
    };
    request.send(payload === undefined ? null : JSON.stringify(payload));
  });
}
// Exam data consists only of JSON values; copying must work on older phones.
export const cloneData=value=>JSON.parse(JSON.stringify(value));
export function formValues(form) {
  const values = {};
  for (let index = 0; index < form.elements.length; index++) {
    const field = form.elements[index];
    if (field.name && !field.disabled && field.type !== 'submit' && field.type !== 'button') values[field.name] = field.value;
  }
  return values;
}
export function askDialog(dialog,message){
  if(typeof dialog.showModal!=='function')return Promise.resolve(window.confirm(message));
  dialog.returnValue='cancel';dialog.showModal();
  return new Promise(resolve=>dialog.addEventListener('close',()=>resolve(dialog.returnValue==='yes'),{once:true}));
}
export async function copyText(text) {
  if (navigator.clipboard && window.isSecureContext) await navigator.clipboard.writeText(text);
  else {
    const field = document.createElement('textarea');
    field.value = text;
    document.body.append(field);
    field.select();
    const copied = document.execCommand('copy');
    field.remove();
    if (!copied) throw new Error('انسخ الرابط يدويًا من الحقل');
  }
  toast('تم النسخ');
}
export function downloadBlob(blob, filename) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url; link.download = filename; document.body.appendChild(link); link.click(); link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 30000);
}
async function validateDownload(blob, expectedType) {
  const signature = new Uint8Array(await blob.slice(0,16).arrayBuffer());
  const prefix = String.fromCharCode(...signature);
  if (!blob.size) throw new Error('الملف فارغ؛ لم يتم حفظه. أعد التنزيل.');
  let valid;
  if (expectedType === 'application/pdf') valid = prefix.indexOf('%PDF-') === 0;
  else if (expectedType === 'application/octet-stream') valid = prefix === 'SQLite format 3\0';
  else if (expectedType === 'text/csv') valid = !/^[\s\ufeff]*[<{\[]/.test(await blob.slice(0,256).text());
  else {
    const tail = new Uint8Array(await blob.slice(-65557).arrayBuffer());
    valid = signature[0] === 80 && signature[1] === 75 && tail.some((byte,index)=>
      byte===80&&tail[index+1]===75&&tail[index+2]===5&&tail[index+3]===6&&index+22<=tail.length&&
      index+22+tail[index+20]+tail[index+21]*256===tail.length);
  }
  if (!valid) throw new Error('الملف غير مكتمل أو غير صالح؛ لم يتم حفظه. أعد التنزيل من البرنامج.');
}
export async function downloadFile(path, filename, expectedType) {
  const safeName = filename.replace(/[\\/:*?"<>|]/g,'-');
  const nativeDownload = window.pywebview?.api?.download_exam_file;
  if (nativeDownload) return (await nativeDownload(path,safeName,expectedType)).saved;
  const {token}=await api('/api/bootstrap');
  const controller=typeof AbortController==='function'?new AbortController():null;
  const timer=controller?setTimeout(()=>controller.abort(),300000):null;
  try {
    const headers={'X-Exam-Request':'1','X-Admin-Token':token};
    const createBackup=path==='/api/backup';
    if(createBackup)headers['Content-Type']='application/json';
    const response=await fetch(path,{headers,credentials:'same-origin',cache:'no-store',
      ...(createBackup?{method:'POST',body:'{}'}:{}),...(controller?{signal:controller.signal}:{})});
    if(!response.ok){
      const failure=await response.json().catch(()=>({}));
      throw new Error(failure.error||'لم يكتمل تنزيل الملف؛ حاول مرة أخرى.');
    }
    if(response.headers.get('Content-Type')?.split(';')[0]!==expectedType)throw new Error('الاستجابة ليست الملف المطلوب؛ لم يتم حفظ ملف غير صالح.');
    const blob=await response.blob();
    await validateDownload(blob,expectedType);
    downloadBlob(blob,safeName);
    return true;
  } finally {if(timer)clearTimeout(timer);}
}

export function confirmAction(title, description) {
  const dialog = $('#confirm-dialog');
  $('#confirm-title').textContent = title;
  $('#confirm-copy').textContent = description;
  return askDialog(dialog, `${title}\n${description}`);
}
