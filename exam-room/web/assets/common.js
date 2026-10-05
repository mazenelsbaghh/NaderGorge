export const $ = (selector, parent = document) => parent.querySelector(selector);
export const $$ = (selector, parent = document) => [...parent.querySelectorAll(selector)];
export const escapeHtml = (text = '') => String(text).replace(/[&<>"']/g, character => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[character]));
export const number = value => new Intl.NumberFormat('ar-EG', {maximumFractionDigits: 2}).format(value);
export const timeLabel = seconds => `${String(Math.floor(Math.max(0, seconds) / 60)).padStart(2, '0')}:${String(Math.floor(Math.max(0, seconds) % 60)).padStart(2, '0')}`;
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
  element.replaceChildren(text, close);
  element.className = `toast${error ? ' error' : ''}`;
  element.hidden = false;
  clearTimeout(toastTimeout);
  toastTimeout = setTimeout(() => { element.hidden = true; }, 6000);
}
export async function api(path, payload, token, options = {}) {
  const headers = {'X-Exam-Request': '1'};
  if (token) headers['X-Admin-Token'] = token;
  if (payload !== undefined) headers['Content-Type'] = 'application/json';
  const body = payload === undefined ? undefined : JSON.stringify(payload);
  const response = await fetch(path, {method: payload === undefined ? 'GET' : 'POST',
    headers, body, keepalive: options.keepalive === true && new Blob([body || '']).size < 60000,
    cache: 'no-store', signal: AbortSignal.timeout(options.timeoutMs || 12000)});
  const result = await response.json();
  if (!response.ok) {
    const error = new Error(result.error || 'تعذر إكمال العملية');
    error.status = response.status;
    throw error;
  }
  return result;
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
  link.href = url; link.download = filename; link.click();
  setTimeout(() => URL.revokeObjectURL(url), 30000);
}
export function confirmAction(title, description) {
  const dialog = $('#confirm-dialog');
  $('#confirm-title').textContent = title;
  $('#confirm-copy').textContent = description;
  dialog.returnValue = 'cancel';
  dialog.showModal();
  return new Promise(resolve => dialog.addEventListener('close', () => resolve(dialog.returnValue === 'yes'), {once: true}));
}
