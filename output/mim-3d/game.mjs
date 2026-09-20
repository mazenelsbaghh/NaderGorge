import { lesson } from './lesson.mjs';
import { createWorld } from './world.mjs';
lesson.missions.forEach((mission,index) => { mission.x = 20 + index * 30; mission.y = 45; });

const $ = (id) => document.getElementById(id);
const storageKey = 'massar-mim-3d-v1';
const freshProgress = () => ({ completed: 0, solved: [[], [], []], mistakes: 0, hearts: 3, enabled: false });
let progress = freshProgress();
let missionIndex = 0;
let selectedTask = null;
let puzzleActive = false;
let selectedChoice = 0;
let awaitingNext = false;
let playing = false;
let paused = false;
let soundEnabled = false;
let audioContext;
let pendingStation = null;
let destination = null;
let toastTimer;
let playerPosition = { x: 50, y: 80 };
const pressedKeys = new Set();

function restoreProgress() {
  try {
    const saved = JSON.parse(localStorage.getItem(storageKey));
    if (!saved) return;
    const valid = Number.isInteger(saved.completed) && saved.completed >= 0 && saved.completed <= 3
      && Number.isInteger(saved.mistakes) && saved.mistakes >= 0 && typeof saved.enabled === 'boolean'
      && Array.isArray(saved.solved) && saved.solved.length === 3
      && saved.solved.every((answers, index) => Array.isArray(answers) && new Set(answers).size === answers.length
        && answers.every(answer => Number.isInteger(answer) && answer >= 0 && answer < lesson.missions[index].tasks.length))
      && saved.solved.every((answers, index) => index >= saved.completed || answers.length === lesson.missions[index].tasks.length);
    if (valid) progress = { ...saved, hearts: Number.isInteger(saved.hearts) && saved.hearts >= 0 && saved.hearts <= 3 ? saved.hearts : 3 };
  } catch (error) {
    if (!(error instanceof SyntaxError || error instanceof DOMException)) throw error;
    $('saveStatus').textContent = 'الحفظ المحلي غير متاح؛ تقدر تلعب في الجلسة الحالية.';
  }
}

function saveProgress() {
  try { localStorage.setItem(storageKey, JSON.stringify(progress)); }
  catch (error) {
    if (!(error instanceof DOMException)) throw error;
    $('saveStatus').textContent = 'التقدم محفوظ في الجلسة فقط؛ المتصفح منع التخزين.';
  }
}

function notify(message) {
  clearTimeout(toastTimer);
  $('toast').textContent = message;
  $('toast').hidden = false;
  toastTimer = setTimeout(() => { $('toast').hidden = true; }, 4500);
}

function playChime() {
  if (!soundEnabled) return;
  const startTime = audioContext.currentTime;
  [523, 659, 784].forEach((frequency, index) => {
    const oscillator = audioContext.createOscillator();
    const gain = audioContext.createGain();
    oscillator.connect(gain); gain.connect(audioContext.destination);
    oscillator.frequency.value = frequency;
    gain.gain.setValueAtTime(0.045, startTime + index * 0.09);
    gain.gain.exponentialRampToValueAtTime(0.001, startTime + index * 0.09 + 0.3);
    oscillator.start(startTime + index * 0.09); oscillator.stop(startTime + index * 0.09 + 0.3);
  });
}

function renderVitals() {
  $('hearts').replaceChildren();
  for (let index = 0; index < 3; index++) {
    const heart = document.createElement('span');
    heart.className = index < progress.hearts ? 'heart full' : 'heart empty';
    heart.textContent = index < progress.hearts ? '♥' : '♡';
    heart.setAttribute('aria-hidden', 'true'); $('hearts').append(heart);
  }
  $('hearts').setAttribute('aria-label', `${progress.hearts} من 3 قلوب متبقية`);
  const solved = progress.solved.reduce((total, answers) => total + answers.length, 0);
  const total = lesson.missions.reduce((count, mission) => count + mission.tasks.length, 0);
  $('journeyProgress').max = total; $('journeyProgress').value = solved;
  $('journeyCaption').textContent = `${solved} / ${total} · رحلة بناء الدولة`;
}

function recoverHearts() {
  progress.hearts = 3; saveProgress(); renderVitals(); paused = false;
  $('recoverLayer').hidden = true; $('world').focus({ preventScroll: true });
}

function loseHeart() {
  progress.mistakes++; progress.hearts--; saveProgress(); renderVitals();
  if (progress.hearts > 0) return;
  paused = true; pressedKeys.clear(); destination = null; pendingStation = null;
  $('recoverLayer').hidden = false; $('recover').focus();
}

function renderMap() {
  $('stations').replaceChildren(); $('seals').replaceChildren();
  lesson.missions.forEach((mission, index) => {
    const button = document.createElement('button');
    button.className = `station ${index < progress.completed ? 'complete' : ''}`;
    button.style.left = `${mission.x}%`; button.style.top = `${mission.y}%`;
    button.disabled = index > progress.completed;
    button.innerHTML = `<span class="marker"><i>${index < progress.completed ? '✓' : index > progress.completed ? '⌑' : mission.icon}</i></span><span class="station-label">${mission.title}</span>`;
    button.setAttribute('aria-label', `${mission.title}${index < progress.completed ? '، مكتملة' : ''}`);
    button.onclick = () => travelToStation(index);
    $('stations').append(button);
    const seal = document.createElement('span');
    seal.className = `seal ${index < progress.completed ? 'earned' : ''}`;
    seal.textContent = ['◈', '⚖', '△'][index];
    seal.title = `${mission.reward} · ${index < progress.completed ? 'تم جمعه' : 'لم يُجمع بعد'}`;
    seal.setAttribute('role', 'img'); seal.setAttribute('aria-label', seal.title);
    $('seals').append(seal);
  });
  renderVitals();
  $('questTitle').textContent = progress.completed === 3 ? 'اكتملت الأختام الثلاثة!' : `مهمتك: ${lesson.missions[progress.completed].title}`;
  $('victoryWorld').hidden = progress.completed !== 3;
  $('interact').textContent = progress.completed === 3 ? 'شوف إنجازك' : 'افتح المهمة';
}

function startGame() {
  $('intro').hidden = true; $('finish').hidden = true; $('game').hidden = false;document.body.classList.add('immersive');
  playing = true; paused = progress.hearts === 0; $('pauseLayer').hidden = true;
  $('recoverLayer').hidden = !paused;
  closeMission();
  renderMap(); $('world').focus({ preventScroll: true });
}

function travelToStation(index) {
  if (paused || index > progress.completed) return;
  pendingStation = index;
  destination = { x: lesson.missions[index].x, y: lesson.missions[index].y + 13 };
  showDestination();
}

function showDestination() {
  $('destination').hidden = false;
  $('destination').style.left = `${destination.x}%`; $('destination').style.top = `${destination.y}%`;
}

function showCurrentTask() {
  const mission = lesson.missions[missionIndex];
  selectedTask = mission.tasks.findIndex((task,index) => !progress.solved[missionIndex].includes(index));
  if (selectedTask === -1) {world3d.showPuzzle({label:mission.reward,choices:mission.choices,choice:0});completeMission();return;}
  selectedChoice = 0; awaitingNext = false;
  const task = mission.tasks[selectedTask];
  world3d.showPuzzle({ label:task.label, choices:mission.choices, choice:selectedChoice });
  $('wheelTask').textContent = task.label;
  $('wheelFeedback').textContent = 'لف العجلة لتوجيه المسار، ثم اضغط تشغيل.';
  $('wheelFeedback').className = '';
  $('wheelConfirm').textContent = 'تشغيل المسار';
  $('wheelPrev').disabled = false; $('wheelNext').disabled = false;
  updateWheelLabel();
}

function updateWheelLabel() {
  $('wheelChoice').textContent = lesson.missions[missionIndex].choices[selectedChoice];
  $('wheelCount').textContent = `${progress.solved[missionIndex].length} / ${lesson.missions[missionIndex].tasks.length}`;
}

function rotateWheel(direction) {
  if (!puzzleActive || awaitingNext || paused) return;
  const count = lesson.missions[missionIndex].choices.length;
  selectedChoice = (selectedChoice + direction + count) % count;
  world3d.rotatePuzzle(selectedChoice); updateWheelLabel();
}

function openMission(index) {
  if (paused || index > progress.completed) return;
  missionIndex=index; puzzleActive=true; pressedKeys.clear(); destination=null;
  $('wheelHud').hidden=false; $('stations').hidden=true; $('world').classList.add('mechanism-mode');
  $('questTitle').textContent=lesson.missions[index].title;
  $('questText').textContent='تحكم بالعجلة لتوصيل العنصر إلى وجهته. Q و R للدوران، E للتشغيل.';
  showCurrentTask();
}

function closeMission() {
  puzzleActive=false; awaitingNext=false; world3d.hidePuzzle(); $('wheelHud').hidden=true;
  $('stations').hidden=false; $('world').classList.remove('mechanism-mode');
  $('questText').textContent='اضغط على علامة المهمة، وميم هيوصلك ليها.';
  renderMap(); $('world').focus({preventScroll:true});
}

function confirmWheel() {
  if (!puzzleActive || paused) return;
  if (awaitingNext) {
    if(progress.solved[missionIndex].length===lesson.missions[missionIndex].tasks.length) {closeMission();return;}
    showCurrentTask();return;
  }
  const task=lesson.missions[missionIndex].tasks[selectedTask];
  if(selectedChoice!==task.answer) {
    loseHeart();
    $('wheelFeedback').textContent=`المسار محتاج تعديل. ${task.why}`;
    $('wheelFeedback').className='wrong';return;
  }
  progress.solved[missionIndex].push(selectedTask);awaitingNext=true;world3d.celebrate();playChime();
  $('wheelFeedback').textContent=`المسار اشتغل! ${task.why}`; $('wheelFeedback').className='correct';
  $('wheelConfirm').textContent='العنصر التالي ←'; $('wheelPrev').disabled=true; $('wheelNext').disabled=true;
  updateWheelLabel(); renderVitals();
  if(progress.solved[missionIndex].length===lesson.missions[missionIndex].tasks.length) completeMission();
  saveProgress();
}

function completeMission() {
  progress.completed=Math.max(progress.completed,missionIndex+1);awaitingNext=true;
  $('wheelFeedback').textContent=`حصلت على ${lesson.missions[missionIndex].reward}! ${missionIndex===2?'باب المتحف اتفتح.':'المحطة التالية جاهزة.'}`;
  $('wheelConfirm').textContent='ارجع للعالم ←'; $('wheelPrev').disabled=true; $('wheelNext').disabled=true;
  renderMap();saveProgress();
}

function showFinish() {
  $('game').hidden = true; $('finish').hidden = false; playing = false;document.body.classList.remove('immersive');
  $('finishStats').textContent = `✦ ٣ أختام مكتملة · ١٠ تحديات محلولة · ${progress.mistakes} محاولات تعلّم إضافية`;
}

function setPause() {
  if (progress.hearts === 0) return;
  paused = !paused; pressedKeys.clear(); destination = null; pendingStation = null;
  $('pauseLayer').hidden = !paused;
  if (paused) $('resume').focus(); else $('world').focus({ preventScroll: true });
}

function updateSettings() {
  $('enabled').checked = progress.enabled;
  $('visibility').textContent = progress.enabled ? 'المحاكاة: اللعبة مفعّلة لهذه الحصة.' : 'المحاكاة: اللعبة غير مفعّلة لهذه الحصة.';
}

function resetGame() {
  closeMission();
  const enabled = progress.enabled;
  progress = { ...freshProgress(), enabled }; saveProgress();
  pendingStation = null; destination = null; playerPosition = { x: 50, y: 80 };
  startGame(); notify('بدأت رحلة جديدة مع ميم.');
}

function updateMovement(seconds) {
  const horizontal = Number(pressedKeys.has('arrowright') || pressedKeys.has('d')) - Number(pressedKeys.has('arrowleft') || pressedKeys.has('a'));
  const vertical = Number(pressedKeys.has('arrowdown') || pressedKeys.has('s')) - Number(pressedKeys.has('arrowup') || pressedKeys.has('w'));
  if (horizontal || vertical) {
    destination = null; pendingStation = null;
    const length = Math.hypot(horizontal, vertical);
    playerPosition.x += horizontal / length * seconds * 24;
    playerPosition.y += vertical / length * seconds * 30;
  } else if (destination) advanceToDestination(seconds);
  playerPosition.x = Math.max(7, Math.min(93, playerPosition.x));
  playerPosition.y = Math.max(58, Math.min(88, playerPosition.y));
  $('player').classList.toggle('walking', Boolean(horizontal || vertical || destination));
  $('destination').hidden = !destination;
}

function advanceToDestination(seconds) {
  const deltaX = destination.x - playerPosition.x; const deltaY = destination.y - playerPosition.y;
  const distance = Math.hypot(deltaX, deltaY);
  if (distance < 0.6) {
    destination = null;
    if (pendingStation !== null) { const index = pendingStation; pendingStation = null; openMission(index); }
    return;
  }
  const step = Math.min(distance, seconds * 30);
  playerPosition.x += deltaX / distance * step; playerPosition.y += deltaY / distance * step;
}


function interact() {
  if (progress.completed === 3) return showFinish();
  travelToStation(progress.completed);
}

function bindControls() {
  $('start').onclick = startGame; $('interact').onclick = interact;
  $('wheelExit').onclick=closeMission; $('wheelPrev').onclick=()=>rotateWheel(-1); $('wheelNext').onclick=()=>rotateWheel(1); $('wheelConfirm').onclick=confirmWheel;
  $('hint').onclick = () => notify(lesson.missions[Math.min(progress.completed, 2)].hint);

  $('recover').onclick = recoverHearts;
  $('pause').onclick = setPause; $('resume').onclick = setPause;
  $('settingsButton').onclick = () => { pressedKeys.clear(); updateSettings(); $('settings').showModal(); };
  $('closeSettings').onclick = () => $('settings').close();
  $('enabled').onchange = () => { progress.enabled = $('enabled').checked; saveProgress(); updateSettings(); };
  $('preview').onclick = () => { $('settings').close(); startGame(); };
  $('reset').onclick = () => { $('settings').close(); resetGame(); };
  $('replay').onclick = resetGame; $('returnMap').onclick = startGame;
  document.querySelectorAll('[data-move]').forEach(button=>{
    const key=button.dataset.move;button.onpointerdown=event=>{event.preventDefault();button.setPointerCapture(event.pointerId);if(!paused&&!puzzleActive)pressedKeys.add(key);};
    button.onpointerup=()=>pressedKeys.delete(key);button.onpointercancel=()=>pressedKeys.delete(key);button.onlostpointercapture=()=>pressedKeys.delete(key);
  });
  $('sound').onclick = async () => {
    audioContext ??= new AudioContext(); await audioContext.resume(); soundEnabled = !soundEnabled;
    $('sound').setAttribute('aria-label', soundEnabled ? 'إغلاق الصوت' : 'تشغيل الصوت'); $('sound').title = soundEnabled ? 'إغلاق الصوت' : 'تشغيل الصوت'; $('sound').setAttribute('aria-pressed', String(soundEnabled));
    playChime();
  };
}

document.addEventListener('keydown', event => {
  if (!playing || $('settings').open || paused) return;
  if(puzzleActive) { if(event.key.toLowerCase()==='q')rotateWheel(-1); if(event.key.toLowerCase()==='r')rotateWheel(1); if(event.key.toLowerCase()==='e'&&!event.repeat)confirmWheel(); return; }
  const key = event.key.toLowerCase();
  if (['arrowup', 'arrowdown', 'arrowleft', 'arrowright', 'w', 'a', 's', 'd'].includes(key)) {
    event.preventDefault(); pressedKeys.add(key);
  }
  if (key === 'e' && !event.repeat) interact();
});
document.addEventListener('keyup', event => pressedKeys.delete(event.key.toLowerCase()));
window.addEventListener('blur', () => pressedKeys.clear());
document.addEventListener('visibilitychange', () => { pressedKeys.clear(); if (document.hidden && playing && !paused) setPause(); });
const world3d = createWorld($('world'), { rotate:rotateWheel, move: point => { if (!playing || paused || puzzleActive || $('settings').open) return; pendingStation = null; destination = point; } });
let lastFrame = performance.now();
function animate(now) {
  const elapsed = Math.min((now - lastFrame) / 1000, 0.05); lastFrame = now;
  if (playing && !paused && !puzzleActive && !$('settings').open) updateMovement(elapsed);
  if (playing) {
    const moving = !paused && !puzzleActive && !$('settings').open && Boolean(destination || pressedKeys.size);
    world3d.render(playerPosition, moving, progress.completed, paused || $('settings').open ? 0 : elapsed);
    document.querySelectorAll('.station').forEach((station,index)=>{ const point=world3d.project(index);station.style.left=`${point.x}%`;station.style.top=`${point.y}%`;station.style.visibility=point.visible?'visible':'hidden'; });
  }
  requestAnimationFrame(animate);
}
restoreProgress(); bindControls(); renderMap();
if (progress.solved.some(answers => answers.length)) $('start').textContent = 'كمّل المغامرة ←';
requestAnimationFrame(animate);
