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
function createControl(kind, index, className) {
  const control = document.createElement('button');
  const label = pairs[index][kind === 'cause' ? 0 : 1];
  control.type = 'button';
  control.className = className;
  control.setAttribute('aria-label', label);
  control.title = label;
  if (className === 'choice') control.textContent = label;
  control.onclick = () => kind === 'cause' ? chooseCause(index) : chooseResult(index);
  controls.push({ control, kind, index });
  return control;
}
function createRow(row) {
  const cause = createControl('cause', row, 'socket');
  cause.dataset.row = row;
  const result = createControl('result', resultOrder[row], 'socket');
  result.dataset.row = row;
  $('sockets').append(cause, result);
  $('causes').append(createControl('cause', row, 'choice'));
  $('results').append(createControl('result', resultOrder[row], 'choice'));
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
function showGame() {
  world.setPuzzle(true);
  document.body.classList.add('playing');
  $('sockets').hidden = $('panel').hidden = false;
  $('play').hidden = true;
  $('view').hidden = false;
}
function showExploration() {
  world.setPuzzle(false);
  document.body.classList.remove('playing');
  $('sockets').hidden = $('panel').hidden = true;
  $('play').hidden = false;
  $('play').textContent = 'ارجع للعبة المصنع';
  $('view').hidden = true;
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
  showGame();
  refresh();
};
$('play').onclick = showGame;
$('view').onclick = showExploration;
function animate(now) {
  frame = requestAnimationFrame(animate);
  const delta = Math.min((now - previousTime) / 1000, 0.05);
  previousTime = now;
  if (document.hidden) return;
  world.render(delta);
  if ($('sockets').hidden) return;
  controls.filter(({ control }) => control.classList.contains('socket')).forEach(({ control, kind }) => {
    const point = world.projectSocket(kind, Number(control.dataset.row));
    control.style.left = `${point.x}%`;
    control.style.top = `${point.y}%`;
    control.style.visibility = point.visible ? 'visible' : 'hidden';
  });
}
try {
  world = createFactoryWorld($('scene'));
  pairs.forEach((_, row) => createRow(row));
  refresh();
  frame = requestAnimationFrame(animate);
} catch (error) {
  console.error('Factory initialization failed:', error);
  $('loadError').hidden = false;
  $('play').disabled = true;
}
$('cameraReset').onclick = () => world?.resetCamera();
window.addEventListener('pagehide', () => { cancelAnimationFrame(frame); world?.dispose(); }, { once: true });
