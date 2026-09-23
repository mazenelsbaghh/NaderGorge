import { createFactoryWorld } from './factory-world.mjs?v=11';

const pairs = [
  ['تبريد الصهارة وتصلّبها', 'تكوّن صخر ناري'],
  ['تماسك وتلاحم الرواسب', 'تكوّن صخر رسوبي'],
  ['حرارة وضغط دون انصهار', 'تكوّن صخر متحوّل'],
];
const hints = [
  'الصهارة لما تبرد وتتصلّب بتعمل صخر ناري.',
  'الرواسب لما تتماسك وتتلاحم بتعمل صخر رسوبي.',
  'الحرارة والضغط من غير انصهار بيعملوا صخر متحوّل.',
];
const answerLabels = [pairs[1][1], pairs[2][1], pairs[0][1]];
const answers = [2, 0, 1];
const $ = (id) => document.getElementById(id);
const connected = new Set();
let energyReady = false, mistakes = 0, world, frame;
let previousTime = performance.now();
const tell = (message) => { $('feedback').textContent = message; };

function refresh() {
  world?.setConnections(connected, connected.size === 3);
  const stage = connected.size;
  $('count').textContent = `${stage.toLocaleString('ar-EG')} / ٣`;
  $('energy').value = stage;
  $('phase').textContent = stage === 3 ? 'المهمة مكتملة' : energyReady ? 'اركض لبوابة الإجابة' : 'تعلّق واجمع نقطة الطاقة';
  $('question').textContent = stage < 3 && energyReady ? `إيه نتيجة: ${pairs[stage][0]}؟` : '';
  $('answers').hidden = !energyReady || stage === 3;
}
function collectEnergy(stage) {
  if (stage !== connected.size || energyReady) return;
  energyReady = true;
  tell('وصلت للطاقة! بص على السؤال واجري جوّه بوابة الإجابة الصح.');
  refresh();
}
function enterAnswer(index) {
  if (!energyReady) return;
  const stage = connected.size;
  if (index !== answers[stage]) {
    mistakes++;
    world.rejectPad(index);
    tell(`جرّب بوابة تانية. ${hints[stage]}`);
    return;
  }
  connected.add(stage);
  energyReady = false;
  if (connected.size === 3) {
    tell('شغّلت دوائر المصنع كلها!');
    $('victory').hidden = $('again').hidden = false;
    $('resultSummary').textContent = mistakes ? `صحّحت ${mistakes.toLocaleString('ar-EG')} محاولة ووصلت للنهاية.` : '٣ إجابات صحيحة من أول محاولة.';
  } else tell(`${hints[stage]} نقطة الطاقة الجاية نورت فوقك؛ اتعلّق بيها.`);
  refresh();
}
$('again').onclick = () => {
  connected.clear(); energyReady = false; mistakes = 0;
  $('victory').hidden = $('again').hidden = true;
  world.resetGame();
  tell('اتعلّق بنقطة الطاقة المضيئة، وبعدها اختار بوابة الإجابة بالحركة.');
  refresh();
};
function animate(now) {
  frame = requestAnimationFrame(animate);
  const delta = Math.min((now - previousTime) / 1000, 0.05);
  previousTime = now;
  if (document.hidden) return;
  world.render(delta);
  if (!energyReady) return;
  for (const marker of document.querySelectorAll('.answer-marker')) {
    const point = world.projectPad(Number(marker.dataset.index));
    marker.style.left = `${point.x}%`;
    marker.style.top = `${point.y}%`;
    marker.style.visibility = point.visible ? 'visible' : 'hidden';
  }
}
try {
  world = createFactoryWorld($('scene'), collectEnergy, enterAnswer);
  answerLabels.forEach((label, index) => {
    const marker = document.createElement('div');
    marker.className = 'answer-marker'; marker.dataset.index = index;
    marker.textContent = label; $('answers').append(marker);
  });
  refresh(); frame = requestAnimationFrame(animate);
} catch (error) {
  console.error('Factory initialization failed:', error);
  $('loadError').hidden = false;
}
$('cameraReset').onclick = () => world?.resetCamera();
window.addEventListener('pagehide', () => { cancelAnimationFrame(frame); world?.dispose(); }, { once: true });
