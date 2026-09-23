import { createFactoryWorld } from './factory-world.mjs';
const pairs = [
  ['تبريد الصهارة وتصلّبها', 'تكوّن صخر ناري'],
  ['تماسك وتلاحم الرواسب', 'تكوّن صخر رسوبي'],
  ['حرارة وضغط دون انصهار', 'تكوّن صخر متحوّل'],
];
const hints = ['الصهارة مادة منصهرة؛ لما تبرد بتكوّن الصخور النارية.', 'الرواسب تتراكم وتتلاحم، ومنها تتكوّن الصخور الرسوبية.', 'الحرارة والضغط يغيّروا الصخر الموجود من غير ما ينصهر.'];
const resultOrder = [1, 2, 0];
const $ = (id) => document.getElementById(id);
const connected = new Set(), controls = [];
let selected = null, powered = false, walking = false, mistakes = 0;
let world, frame, previousTime = performance.now();
const tell = (message) => { $('feedback').textContent = message; };
function approach(kind, action) {
  walking = true; tell(kind === 'cause' ? 'ميم رايح يلقط الكابل…' : kind === 'result' ? 'ميم بيوصل الكابل للآلة…' : 'ميم رايح يشغّل المصنع…');
  refresh();
  world.walkToMachine(kind, (arrived) => {
    walking = false;
    if (arrived) action(); else tell('وقفت المشي. اضغط على الآلة لما تكون جاهز.');
    refresh();
  });
}
function chooseCause(index) {
  approach('cause', () => {
    selected = index; world.carryCable(index);
    tell('الكابل معاك! اختار آلة النتيجة الزرقاء المناسبة، وميم هيمشي لها.');
  });
}
function chooseResult(index) {
  if (selected === null) { tell('القط كابل السبب المضيء الأول.'); return; }
  approach('result', () => {
    if (selected !== index) {
      mistakes++; world.rejectCable();
      tell(`التوصيلة محتاجة تتغيّر. ${hints[selected]}`);
      return;
    }
    connected.add(index); selected = null; world.carryCable(null);
    tell(connected.size === 3 ? 'دوائر الطاقة الثلاثة جاهزة! اضغط مفتاح تشغيل الآلة.' : `اشتغلت دائرة جديدة! ${hints[index]} التقط الكابل التالي.`);
  });
}
function createControl(kind, index, row) {
  const control = document.createElement('button');
  control.type = 'button'; control.className = 'socket';
  control.setAttribute('aria-label', pairs[index][kind === 'cause' ? 0 : 1]);
  const caption = document.createElement('small'); caption.textContent = kind === 'cause' ? 'التقط الكابل' : 'وصّل هنا';
  const label = document.createElement('span'); label.textContent = pairs[index][kind === 'cause' ? 0 : 1];
  control.append(caption, label); control.dataset.kind = kind; control.dataset.row = row;
  control.onclick = () => kind === 'cause' ? chooseCause(index) : chooseResult(index);
  controls.push({ control, kind, index }); $('sockets').append(control);
}
function refresh() {
  world?.setConnections(connected, powered);
  controls.forEach(({ control, kind, index }) => {
    const done = connected.has(index), current = index === connected.size;
    control.disabled = done || powered || walking || (kind === 'cause' && (!current || selected !== null));
    control.classList.toggle('done', done);
    control.classList.toggle('dormant', kind === 'cause' && !current && !done);
    control.setAttribute('aria-pressed', String(kind === 'cause' && selected === index));
  });
  $('count').textContent = `${connected.size.toLocaleString('ar-EG')} / ٣`;
  $('phase').textContent = powered ? 'المصنع عاد للحياة' : connected.size === 3 ? 'الخطوة الأخيرة · شغّل المحرّك' : selected === null ? '١ · التقط كابل السبب' : '٢ · وصّل الكابل بالنتيجة';
  $('power').disabled = connected.size !== 3 || powered || walking;
  $('release').hidden = selected === null || walking;
  $('energy').value = connected.size;
}
$('power').onclick = () => approach('power', () => {
  powered = true; world.carryCable(null);
  tell('أعدت الطاقة للمصنع! التروس والماكينة شغّالين.');
  $('power').textContent = 'الطاقة مكتملة ✓'; $('again').hidden = false;
  $('victory').hidden = false;
  $('resultSummary').textContent = mistakes === 0 ? '٣ دوائر صحيحة من أول محاولة. توصيل متقن!' : `٣ دوائر شغّالة. محاولات احتاجت تصحيح: ${mistakes.toLocaleString('ar-EG')}.`;
});
$('release').onclick = () => { selected = null; world.carryCable(null); tell('رجّعت الكابل. التقطه لما تكون جاهز.'); refresh(); };
$('again').onclick = () => {
  selected = null; powered = false; mistakes = 0; connected.clear();
  world.carryCable(null); world.resetCamera();
  $('victory').hidden = $('again').hidden = true;
  $('power').textContent = 'شغّل المصنع';
  tell('المصنع ساكت… التقط الكابل البرتقالي الأول علشان نرجّع الطاقة.'); refresh();
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
  pairs.forEach((_, row) => { createControl('cause', row, row); createControl('result', resultOrder[row], row); });
  refresh(); frame = requestAnimationFrame(animate);
} catch (error) {
  console.error('Factory initialization failed:', error);
  $('loadError').hidden = false; $('sockets').hidden = true;
}
$('cameraReset').onclick = () => world?.resetCamera();
window.addEventListener('pagehide', () => { cancelAnimationFrame(frame); world?.dispose(); }, { once: true });
