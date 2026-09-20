import { lesson } from './lesson.mjs';

const $ = (id) => document.getElementById(id);
const storageKey = 'massar-mim-playtest-v1';
const freshProgress = () => ({ completed: 0, solved: [[], [], []], mistakes: 0, enabled: false });
let progress = freshProgress();
let missionIndex = 0;
let selectedTask = null;
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
    if (valid) progress = saved;
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
    seal.textContent = index < progress.completed ? '✦' : '○'; seal.title = mission.reward;
    $('seals').append(seal);
  });
  $('questTitle').textContent = progress.completed === 3 ? 'اكتملت الأختام الثلاثة!' : `مهمتك: ${lesson.missions[progress.completed].title}`;
  $('victoryWorld').hidden = progress.completed !== 3;
  $('interact').textContent = progress.completed === 3 ? 'شوف إنجازك' : 'افتح المهمة';
}

function startGame() {
  $('intro').hidden = true; $('finish').hidden = true; $('game').hidden = false;
  playing = true; paused = false; $('pauseLayer').hidden = true;
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

function renderTasks() {
  const mission = lesson.missions[missionIndex];
  $('taskCards').replaceChildren(); $('puzzleProgress').replaceChildren();
  mission.tasks.forEach((task, index) => {
    const solved = progress.solved[missionIndex].includes(index);
    const button = document.createElement('button');
    button.className = `task-card ${solved ? 'solved' : ''} ${selectedTask === index ? 'selected' : ''}`;
    button.disabled = solved; button.setAttribute('aria-pressed', String(selectedTask === index));
    button.innerHTML = `<span class="task-icon">${solved ? '✓' : task.icon}</span>${task.label}`;
    button.onclick = () => { selectedTask = index; renderTasks(); };
    $('taskCards').append(button);
    const dot = document.createElement('span'); dot.className = solved ? 'done' : '';
    $('puzzleProgress').append(dot);
  });
  document.querySelectorAll('.choice').forEach(button => { button.disabled = selectedTask === null; });
}

function openMission(index) {
  if (paused || index > progress.completed) return;
  missionIndex = index; selectedTask = null; pressedKeys.clear(); destination = null;
  const mission = lesson.missions[index];
  $('puzzleTitle').textContent = mission.title; $('puzzleStep').textContent = `المهمة ${index + 1} من ٣`;
  $('puzzleInstructions').textContent = mission.instruction; $('source').textContent = mission.source;
  $('choices').replaceChildren();
  mission.choices.forEach((label, choiceIndex) => {
    const button = document.createElement('button'); button.className = 'choice'; button.textContent = label;
    button.onclick = () => submitChoice(choiceIndex); $('choices').append(button);
  });
  $('feedback').className = 'feedback'; $('feedback').textContent = 'اختار عنصرًا من فوق، ثم مكانه المناسب.';
  $('nextMission').hidden = true; renderTasks();
  if (progress.solved[index].length === mission.tasks.length) completeMission();
  $('puzzle').showModal();
}

function submitChoice(choiceIndex) {
  if (selectedTask === null) return;
  const task = lesson.missions[missionIndex].tasks[selectedTask];
  if (choiceIndex !== task.answer) {
    progress.mistakes += 1; saveProgress();
    $('feedback').className = 'feedback error';
    $('feedback').textContent = `لسه محتاجة تفكير. ${task.why} جرّب توصيلها تاني.`;
    return;
  }
  progress.solved[missionIndex].push(selectedTask); selectedTask = null;
  $('feedback').className = 'feedback success'; $('feedback').textContent = `توصيل صحيح! ${task.why}`;
  playChime(); renderTasks();
  if (progress.solved[missionIndex].length === lesson.missions[missionIndex].tasks.length) completeMission();
  saveProgress();
}

function completeMission() {
  progress.completed = Math.max(progress.completed, missionIndex + 1);
  $('feedback').className = 'feedback success';
  $('feedback').textContent = `✦ حصلت على ${lesson.missions[missionIndex].reward}! ${missionIndex < 2 ? 'الطريق للمحطة التالية اتفتح.' : 'أقفال الباب اتفتحت. المدينة عادت للحياة!'}`;
  $('nextMission').hidden = false; $('nextMission').textContent = missionIndex === 2 ? 'شوف إنجازك ✦' : 'ارجع للمدينة ←';
  renderMap();
}

function showFinish() {
  $('game').hidden = true; $('finish').hidden = false; playing = false;
  $('finishStats').textContent = `✦ ٣ أختام مكتملة · ١٠ تحديات محلولة · ${progress.mistakes} محاولات تعلّم إضافية`;
}

function setPause() {
  paused = !paused; pressedKeys.clear(); destination = null; pendingStation = null;
  $('pauseLayer').hidden = !paused;
  if (paused) $('resume').focus(); else $('world').focus({ preventScroll: true });
}

function updateSettings() {
  $('enabled').checked = progress.enabled;
  $('visibility').textContent = progress.enabled ? 'المحاكاة: اللعبة مفعّلة لهذه الحصة.' : 'المحاكاة: اللعبة غير مفعّلة لهذه الحصة.';
}

function resetGame() {
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

function moveFromPointer(event) {
  if (event.target.closest('button') || paused) return;
  const bounds = $('world').getBoundingClientRect(); pendingStation = null;
  destination = { x: Math.max(7, Math.min(93, (event.clientX - bounds.left) / bounds.width * 100)),
    y: Math.max(58, Math.min(88, (event.clientY - bounds.top) / bounds.height * 100)) };
  showDestination(); $('world').focus({ preventScroll: true });
}

function interact() {
  if (progress.completed === 3) return showFinish();
  travelToStation(progress.completed);
}

function bindControls() {
  $('start').onclick = startGame; $('world').onclick = moveFromPointer; $('interact').onclick = interact;
  $('closePuzzle').onclick = () => { $('puzzle').close(); $('world').focus(); };
  $('nextMission').onclick = () => { $('puzzle').close(); if (missionIndex === 2) showFinish(); else $('world').focus(); };
  $('hint').onclick = () => notify(lesson.missions[Math.min(progress.completed, 2)].hint);
  $('puzzleHint').onclick = () => { $('feedback').className = 'feedback'; $('feedback').textContent = lesson.missions[missionIndex].hint; };
  $('pause').onclick = setPause; $('resume').onclick = setPause;
  $('settingsButton').onclick = () => { pressedKeys.clear(); updateSettings(); $('settings').showModal(); };
  $('closeSettings').onclick = () => $('settings').close();
  $('enabled').onchange = () => { progress.enabled = $('enabled').checked; saveProgress(); updateSettings(); };
  $('preview').onclick = () => { $('settings').close(); startGame(); };
  $('reset').onclick = () => { $('settings').close(); resetGame(); };
  $('replay').onclick = resetGame; $('returnMap').onclick = startGame;
  $('sound').onclick = async () => {
    audioContext ??= new AudioContext(); await audioContext.resume(); soundEnabled = !soundEnabled;
    $('sound').textContent = soundEnabled ? 'الصوت: شغّال' : 'الصوت: مغلق'; $('sound').setAttribute('aria-pressed', String(soundEnabled));
    playChime();
  };
}

document.addEventListener('keydown', event => {
  if (!playing || $('puzzle').open || $('settings').open || paused) return;
  const key = event.key.toLowerCase();
  if (['arrowup', 'arrowdown', 'arrowleft', 'arrowright', 'w', 'a', 's', 'd'].includes(key)) {
    event.preventDefault(); pressedKeys.add(key);
  }
  if (key === 'e' && !event.repeat) interact();
});
document.addEventListener('keyup', event => pressedKeys.delete(event.key.toLowerCase()));
window.addEventListener('blur', () => pressedKeys.clear());
document.addEventListener('visibilitychange', () => { pressedKeys.clear(); if (document.hidden && playing && !paused) setPause(); });
let lastFrame = performance.now();
function animate(now) {
  const elapsed = Math.min((now - lastFrame) / 1000, 0.05); lastFrame = now;
  if (playing && !paused && !$('puzzle').open && !$('settings').open) updateMovement(elapsed);
  $('player').style.left = `${playerPosition.x}%`; $('player').style.top = `${playerPosition.y}%`;
  requestAnimationFrame(animate);
}
restoreProgress(); bindControls(); renderMap();
if (progress.solved.some(answers => answers.length)) $('start').textContent = 'كمّل المغامرة ←';
requestAnimationFrame(animate);
