import { createFactoryWorld } from './factory-world.mjs';
const pairs = [
  ['تبريد الصهارة وتصلّبها', 'تكوّن صخر ناري'],
  ['تماسك وتلاحم الرواسب', 'تكوّن صخر رسوبي'],
  ['حرارة وضغط دون انصهار', 'تكوّن صخر متحوّل'],
];
const resultOrder = [1, 2, 0];
const $ = (id) => document.getElementById(id);
let selected = null;
let powered = false;
const connected = new Set();
const controls = [];
let world, frame, previousTime = performance.now();

function chooseCause(index) {
  selected = index;
  $('feedback').textContent = `اختر نتيجة: ${pairs[index][0]}`;
  refresh();
}
function chooseResult(index) {
  if (selected === null) {
    $('feedback').textContent = 'اختار سببًا من اليمين الأول.';
    return;
  }
  if (selected !== index) {
    $('feedback').textContent = 'الوصلة دي مش صحيحة. جرّب نتيجة تانية لنفس السبب.';
    return;
  }
  connected.add(index);
  selected = null;
  $('feedback').textContent = connected.size === pairs.length
    ? 'كل الوصلات صحيحة! اضغط «شغّل الآلة».'
    : 'وصلة صحيحة! كمّل توصيل باقي الأسباب.';
  refresh();
}
function createControl(kind, index) {
  const control = document.createElement('button');
  const label = pairs[index][kind === 'cause' ? 0 : 1];
  control.type = 'button';
  control.className = 'socket';
  control.setAttribute('aria-label', label);
  control.title = label;
  control.textContent = label;
  control.dataset.kind = kind;
  control.onclick = () => kind === 'cause' ? chooseCause(index) : chooseResult(index);
  controls.push({ control, kind, index });
  return control;
}
function createRow(row) {
  const cause = createControl('cause', row);
  cause.dataset.row = row;
  const result = createControl('result', resultOrder[row]);
  result.dataset.row = row;
  $('sockets').append(cause, result);
}
function refresh() {
  world?.setConnections(connected, powered);
  controls.forEach(({ control, kind, index }) => {
    const done = connected.has(index);
    control.disabled = done || powered;
    control.classList.toggle('done', done);
    control.setAttribute('aria-pressed', String(kind === 'cause' && selected === index));
  });
  $('count').textContent = `${connected.size.toLocaleString('ar-EG')} / ٣`;
  $('power').disabled = connected.size !== pairs.length || powered;
}
$('power').onclick = () => {
  powered = true;
  $('feedback').textContent = 'شغّلت الآلة! وصلت الأسباب الثلاثة بنتائجها صح.';
  $('power').textContent = 'الآلة اشتغلت ✓';
  $('again').hidden = false;
  refresh();
};
$('again').onclick = () => {
  selected = null;
  powered = false;
  connected.clear();
  $('feedback').textContent = 'وصّل الأزواج الثلاثة لتشغيل الآلة.';
  $('power').textContent = 'شغّل الآلة';
  $('again').hidden = true;
  refresh();
};
function animate(now) {
  frame = requestAnimationFrame(animate);
  const delta = Math.min((now - previousTime) / 1000, 0.05);
  previousTime = now;
  if (document.hidden) return;
  world.render(delta);
  controls.forEach(({ control, kind }) => {
    const point = world.projectSocket(kind, Number(control.dataset.row));
    control.style.left = `${point.x}%`;
    control.style.top = `${point.y}%`;
    control.style.visibility = point.visible ? 'visible' : 'hidden';
  });
  const powerPoint = world.projectPower();
  $('power').style.left = `${powerPoint.x}%`;
  $('power').style.top = `${powerPoint.y}%`;
  $('power').style.visibility = powerPoint.visible ? 'visible' : 'hidden';
}
try {
  world = createFactoryWorld($('scene'));
  pairs.forEach((_, row) => createRow(row));
  refresh();
  frame = requestAnimationFrame(animate);
} catch (error) {
  console.error('Factory initialization failed:', error);
  $('loadError').hidden = false;
  $('sockets').hidden = true;
}
$('cameraReset').onclick = () => world?.resetCamera();
window.addEventListener('pagehide', () => { cancelAnimationFrame(frame); world?.dispose(); }, { once: true });
