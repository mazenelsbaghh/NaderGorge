import { createWorld } from './world.mjs';

const $ = (id) => document.getElementById(id);
const allowedIcons = new Set([
  'book',
  'lightbulb',
  'target',
  'shield',
  'clock',
  'globe',
  'scale',
  'building',
  'flag',
]);
const iconGlyph = {
  book: '▤',
  lightbulb: '✦',
  target: '◎',
  shield: '⬡',
  clock: '◷',
  globe: '◉',
  scale: '⚖',
  building: '▥',
  flag: '⚑',
};
const controls = new AbortController();
let lesson;
let storageKey;
let mode = 'student';
let world3d;
let animationFrame = 0;
let disposed = false;
let progress;
let missionIndex = 0;
let selectedTask = -1;
let selectedChoice = 0;
let puzzleActive = false;
let awaitingNext = false;
let playing = false;
let paused = false;
let soundEnabled = false;
let audioContext;
let pendingStation = null;
let destination = null;
let toastTimer;
let playerPosition = { x: 50, y: 80 };
let lastFrame = performance.now();
const pressedKeys = new Set();
const stickResetters = [];
const stickInput = { move: { x: 0, y: 0 }, camera: { x: 0, y: 0 } };

function safeText(value, max) {
  return (
    typeof value === 'string' &&
    value.trim() &&
    value.length <= max &&
    !/[<>]/.test(value) &&
    !/^\s*(?:javascript:|https?:\/\/)/i.test(value)
  );
}
function validRef(value) {
  return (
    value &&
    safeText(value.videoId, 80) &&
    safeText(value.chapterId, 80) &&
    Number.isInteger(value.startTime) &&
    value.startTime >= 0 &&
    Number.isInteger(value.endTime) &&
    value.endTime >= value.startTime
  );
}
function validTask(value, count) {
  return (
    value &&
    safeText(value.label, 240) &&
    allowedIcons.has(value.icon) &&
    Number.isInteger(value.correctChoiceIndex) &&
    value.correctChoiceIndex >= 0 &&
    value.correctChoiceIndex < count &&
    safeText(value.explanation, 400)
  );
}
function validMission(value) {
  return (
    value &&
    safeText(value.title, 100) &&
    safeText(value.instruction, 500) &&
    safeText(value.hint, 300) &&
    safeText(value.reward, 100) &&
    allowedIcons.has(value.icon) &&
    Array.isArray(value.sourceRefs) &&
    value.sourceRefs.length > 0 &&
    value.sourceRefs.every(validRef) &&
    Array.isArray(value.choices) &&
    value.choices.length >= 2 &&
    value.choices.length <= 4 &&
    value.choices.every((item) => safeText(item, 160)) &&
    Array.isArray(value.tasks) &&
    value.tasks.length >= 3 &&
    value.tasks.length <= 5 &&
    value.tasks.every((item) => validTask(item, value.choices.length))
  );
}
function validContent(value) {
  return (
    value &&
    value.schemaVersion === 1 &&
    safeText(value.title, 120) &&
    safeText(value.intro, 500) &&
    safeText(value.sourceLabel, 160) &&
    Array.isArray(value.missions) &&
    value.missions.length === 3 &&
    value.missions.every(validMission)
  );
}

function post(type, message) {
  parent.postMessage(
    { source: 'massar-mim-game', type, message },
    location.origin
  );
}
function freshProgress() {
  return { completed: 0, solved: [[], [], []], mistakes: 0, hearts: 3 };
}
function restoreProgress() {
  progress = freshProgress();
  try {
    const saved = JSON.parse(localStorage.getItem(storageKey));
    const valid =
      saved &&
      Number.isInteger(saved.completed) &&
      saved.completed >= 0 &&
      saved.completed <= 3 &&
      Number.isInteger(saved.mistakes) &&
      saved.mistakes >= 0 &&
      Array.isArray(saved.solved) &&
      saved.solved.length === 3 &&
      saved.solved.every(
        (answers, index) =>
          Array.isArray(answers) &&
          new Set(answers).size === answers.length &&
          answers.every(
            (answer) =>
              Number.isInteger(answer) &&
              answer >= 0 &&
              answer < lesson.missions[index].tasks.length
          )
      ) &&
      saved.solved.every(
        (answers, index) =>
          index >= saved.completed ||
          answers.length === lesson.missions[index].tasks.length
      );
    if (valid)
      progress = {
        ...saved,
        hearts:
          Number.isInteger(saved.hearts) &&
          saved.hearts >= 0 &&
          saved.hearts <= 3
            ? saved.hearts
            : 3,
      };
  } catch {
    $('saveStatus').textContent = 'التقدم محفوظ في الجلسة الحالية فقط.';
  }
}
function saveProgress() {
  try {
    localStorage.setItem(storageKey, JSON.stringify(progress));
  } catch {
    $('saveStatus').textContent =
      'المتصفح منع الحفظ المحلي؛ يمكنك اللعب في هذه الجلسة.';
  }
}
function notify(message) {
  clearTimeout(toastTimer);
  $('toast').textContent = message;
  $('toast').hidden = false;
  toastTimer = setTimeout(() => {
    $('toast').hidden = true;
  }, 4500);
}
function playChime() {
  if (!soundEnabled || !audioContext) return;
  const start = audioContext.currentTime;
  [523, 659, 784].forEach((frequency, index) => {
    const oscillator = audioContext.createOscillator();
    const gain = audioContext.createGain();
    oscillator.connect(gain);
    gain.connect(audioContext.destination);
    oscillator.frequency.value = frequency;
    gain.gain.setValueAtTime(0.045, start + index * 0.09);
    gain.gain.exponentialRampToValueAtTime(0.001, start + index * 0.09 + 0.3);
    oscillator.start(start + index * 0.09);
    oscillator.stop(start + index * 0.09 + 0.3);
  });
}
function renderVitals() {
  $('hearts').replaceChildren();
  for (let index = 0; index < 3; index++) {
    const heart = document.createElement('span');
    heart.className = index < progress.hearts ? 'heart full' : 'heart empty';
    heart.textContent = index < progress.hearts ? '♥' : '♡';
    heart.setAttribute('aria-hidden', 'true');
    $('hearts').append(heart);
  }
  $('hearts').setAttribute('aria-label', `${progress.hearts} من 3 قلوب متبقية`);
  const solved = progress.solved.reduce(
    (sum, answers) => sum + answers.length,
    0
  );
  const total = lesson.missions.reduce(
    (sum, mission) => sum + mission.tasks.length,
    0
  );
  $('journeyProgress').max = total;
  $('journeyProgress').value = solved;
  $('journeyCaption').textContent = `${solved} / ${total} · ${lesson.title}`;
}
function stationButton(mission, index) {
  const button = document.createElement('button');
  button.className = `station ${index < progress.completed ? 'complete' : ''}`;
  button.style.left = `${20 + index * 30}%`;
  button.style.top = '45%';
  button.disabled = index > progress.completed;
  const marker = document.createElement('span');
  marker.className = 'marker';
  const glyph = document.createElement('i');
  glyph.textContent =
    index < progress.completed
      ? '✓'
      : index > progress.completed
        ? '⌑'
        : iconGlyph[mission.icon];
  marker.append(glyph);
  const label = document.createElement('span');
  label.className = 'station-label';
  label.textContent = mission.title;
  button.append(marker, label);
  button.setAttribute(
    'aria-label',
    `${mission.title}${index < progress.completed ? '، مكتملة' : ''}`
  );
  button.onclick = () => travelToStation(index);
  return button;
}
function renderMap() {
  $('stations').replaceChildren();
  $('seals').replaceChildren();
  lesson.missions.forEach((mission, index) => {
    $('stations').append(stationButton(mission, index));
    const seal = document.createElement('span');
    seal.className = `seal ${index < progress.completed ? 'earned' : ''}`;
    seal.textContent = iconGlyph[mission.icon];
    seal.title = `${mission.reward} · ${index < progress.completed ? 'تم جمعه' : 'لم يُجمع بعد'}`;
    seal.setAttribute('role', 'img');
    seal.setAttribute('aria-label', seal.title);
    $('seals').append(seal);
  });
  renderVitals();
  $('questTitle').textContent =
    progress.completed === 3
      ? 'اكتملت المهمات الثلاث'
      : `مهمتك: ${lesson.missions[progress.completed].title}`;
  $('victoryWorld').hidden = progress.completed !== 3;
  $('interact').textContent =
    progress.completed === 3 ? 'شاهد إنجازك' : 'افتح المهمة';
}
function resetVirtualControls() {
  stickResetters.forEach((reset) => reset());
  stickInput.move.x =
    stickInput.move.y =
    stickInput.camera.x =
    stickInput.camera.y =
      0;
  document.querySelectorAll('.thumbstick').forEach((stick) => {
    stick.classList.remove('active');
    const knob = stick.querySelector('.thumbstick-knob');
    if (knob) knob.style.transform = '';
  });
}
function startGame() {
  $('intro').hidden = true;
  $('finish').hidden = true;
  $('game').hidden = false;
  document.body.classList.add('immersive');
  playing = true;
  paused = progress.hearts === 0;
  $('pauseLayer').hidden = true;
  $('recoverLayer').hidden = !paused;
  closeMission();
  renderMap();
  $('world').focus({ preventScroll: true });
}
function travelToStation(index) {
  if (paused || index > progress.completed) return;
  pendingStation = index;
  destination = { x: 20 + index * 30, y: 58 };
  showDestination();
}
function showDestination() {
  $('destination').hidden = false;
  $('destination').style.left = `${destination.x}%`;
  $('destination').style.top = `${destination.y}%`;
}
function showCurrentTask() {
  const mission = lesson.missions[missionIndex];
  selectedTask = mission.tasks.findIndex(
    (_, index) => !progress.solved[missionIndex].includes(index)
  );
  if (selectedTask === -1) {
    world3d.showPuzzle({
      label: mission.reward,
      choices: mission.choices,
      choice: 0,
    });
    completeMission();
    return;
  }
  selectedChoice = 0;
  awaitingNext = false;
  const task = mission.tasks[selectedTask];
  world3d.showPuzzle({
    label: task.label,
    choices: mission.choices,
    choice: selectedChoice,
  });
  $('wheelTask').textContent = task.label;
  $('wheelFeedback').textContent = mission.instruction;
  $('wheelFeedback').className = '';
  $('wheelConfirm').textContent = 'تأكيد الإجابة';
  $('wheelPrev').disabled = false;
  $('wheelNext').disabled = false;
  updateWheelLabel();
}
function updateWheelLabel() {
  $('wheelChoice').textContent =
    lesson.missions[missionIndex].choices[selectedChoice];
  $('wheelCount').textContent =
    `${progress.solved[missionIndex].length} / ${lesson.missions[missionIndex].tasks.length}`;
}
function rotateWheel(direction) {
  if (!puzzleActive || awaitingNext || paused) return;
  const count = lesson.missions[missionIndex].choices.length;
  selectedChoice = (selectedChoice + direction + count) % count;
  world3d.rotatePuzzle(selectedChoice);
  updateWheelLabel();
}
function openMission(index) {
  if (paused || index > progress.completed) return;
  missionIndex = index;
  puzzleActive = true;
  pressedKeys.clear();
  resetVirtualControls();
  destination = null;
  $('wheelHud').hidden = false;
  $('stations').hidden = true;
  $('world').classList.add('mechanism-mode');
  $('questTitle').textContent = lesson.missions[index].title;
  $('questText').textContent = lesson.missions[index].instruction;
  showCurrentTask();
}
function closeMission() {
  puzzleActive = false;
  awaitingNext = false;
  world3d?.hidePuzzle();
  $('wheelHud').hidden = true;
  $('stations').hidden = false;
  $('world').classList.remove('mechanism-mode');
  $('questText').textContent = 'تحرّك نحو علامة المهمة، ثم افتحها.';
  renderMap();
  $('world').focus({ preventScroll: true });
}
function loseHeart() {
  progress.mistakes++;
  progress.hearts--;
  saveProgress();
  renderVitals();
  if (progress.hearts > 0) return;
  paused = true;
  pressedKeys.clear();
  resetVirtualControls();
  destination = null;
  pendingStation = null;
  $('recoverLayer').hidden = false;
  $('recover').focus();
}
function confirmWheel() {
  if (!puzzleActive || paused) return;
  if (awaitingNext) {
    if (
      progress.solved[missionIndex].length ===
      lesson.missions[missionIndex].tasks.length
    ) {
      closeMission();
      return;
    }
    showCurrentTask();
    return;
  }
  const task = lesson.missions[missionIndex].tasks[selectedTask];
  if (selectedChoice !== task.correctChoiceIndex) {
    loseHeart();
    $('wheelFeedback').textContent = `راجع التلميح. ${task.explanation}`;
    $('wheelFeedback').className = 'wrong';
    return;
  }
  progress.solved[missionIndex].push(selectedTask);
  awaitingNext = true;
  world3d.celebrate();
  playChime();
  $('wheelFeedback').textContent = `إجابة صحيحة. ${task.explanation}`;
  $('wheelFeedback').className = 'correct';
  $('wheelConfirm').textContent = 'السؤال التالي ←';
  $('wheelPrev').disabled = true;
  $('wheelNext').disabled = true;
  updateWheelLabel();
  renderVitals();
  if (
    progress.solved[missionIndex].length ===
    lesson.missions[missionIndex].tasks.length
  )
    completeMission();
  saveProgress();
}
function completeMission() {
  progress.completed = Math.max(progress.completed, missionIndex + 1);
  awaitingNext = true;
  $('wheelFeedback').textContent =
    `حصلت على ${lesson.missions[missionIndex].reward}. ${missionIndex === 2 ? 'اكتملت رحلة المراجعة.' : 'المهمة التالية أصبحت جاهزة.'}`;
  $('wheelConfirm').textContent = 'ارجع للعالم ←';
  $('wheelPrev').disabled = true;
  $('wheelNext').disabled = true;
  renderMap();
  saveProgress();
}
function recoverHearts() {
  progress.hearts = 3;
  saveProgress();
  renderVitals();
  paused = false;
  resetVirtualControls();
  $('recoverLayer').hidden = true;
  $('world').focus({ preventScroll: true });
}
function showFinish() {
  $('game').hidden = true;
  $('finish').hidden = false;
  playing = false;
  resetVirtualControls();
  document.body.classList.remove('immersive');
  const total = lesson.missions.reduce(
    (sum, mission) => sum + mission.tasks.length,
    0
  );
  $('finishCopy').textContent =
    `أكملت المهمات المبنية على ${lesson.sourceLabel}.`;
  $('finishStats').textContent =
    `✦ ٣ مهمات مكتملة · ${total} تحديات محلولة · ${progress.mistakes} محاولات تعلّم إضافية`;
}
function setPause() {
  if (progress.hearts === 0) return;
  paused = !paused;
  pressedKeys.clear();
  resetVirtualControls();
  destination = null;
  pendingStation = null;
  $('pauseLayer').hidden = !paused;
  if (paused) $('resume').focus();
  else $('world').focus({ preventScroll: true });
}
function resetGame() {
  closeMission();
  progress = freshProgress();
  saveProgress();
  pendingStation = null;
  destination = null;
  playerPosition = { x: 50, y: 80 };
  startGame();
  notify('بدأت رحلة مراجعة جديدة مع ميم.');
}
function advanceToDestination(seconds) {
  const dx = destination.x - playerPosition.x,
    dy = destination.y - playerPosition.y;
  const distance = Math.hypot(dx, dy);
  if (distance < 0.6) {
    destination = null;
    if (pendingStation !== null) {
      const index = pendingStation;
      pendingStation = null;
      openMission(index);
    }
    return;
  }
  const step = Math.min(distance, seconds * 30);
  playerPosition.x += (dx / distance) * step;
  playerPosition.y += (dy / distance) * step;
}
function updateMovement(seconds) {
  const keyX =
    Number(pressedKeys.has('arrowright') || pressedKeys.has('d')) -
    Number(pressedKeys.has('arrowleft') || pressedKeys.has('a'));
  const keyY =
    Number(pressedKeys.has('arrowdown') || pressedKeys.has('s')) -
    Number(pressedKeys.has('arrowup') || pressedKeys.has('w'));
  const horizontal = keyX || stickInput.move.x,
    vertical = keyY || stickInput.move.y;
  if (horizontal || vertical) {
    destination = null;
    pendingStation = null;
    const length = Math.hypot(horizontal, vertical),
      intensity = Math.min(1, length),
      movement = world3d.cameraRelativeMovement(
        horizontal / length,
        vertical / length
      );
    playerPosition.x += movement.x * intensity * seconds * 24;
    playerPosition.y += movement.y * intensity * seconds * 30;
  } else if (destination) advanceToDestination(seconds);
  playerPosition.x = Math.max(7, Math.min(93, playerPosition.x));
  playerPosition.y = Math.max(58, Math.min(88, playerPosition.y));
  $('player').classList.toggle(
    'walking',
    Boolean(horizontal || vertical || destination)
  );
  $('destination').hidden = !destination;
}
function bindThumbstick(element, state) {
  const knob = element.querySelector('.thumbstick-knob');
  const deadZone = 0.14;
  let pointerId = null;
  const release = (event) => {
    if (event && event.pointerId !== pointerId) return;
    const captured = pointerId;
    pointerId = null;
    if (captured !== null && element.hasPointerCapture(captured))
      element.releasePointerCapture(captured);
    state.x = state.y = 0;
    element.classList.remove('active');
    knob.style.transform = '';
  };
  const update = (event) => {
    if (event.pointerId !== pointerId) return;
    const bounds = element.getBoundingClientRect(),
      radius = Math.max(1, Math.min(bounds.width, bounds.height) * 0.34);
    let x = (event.clientX - (bounds.left + bounds.width / 2)) / radius,
      y = (event.clientY - (bounds.top + bounds.height / 2)) / radius;
    const raw = Math.hypot(x, y);
    if (raw > 1) {
      x /= raw;
      y /= raw;
    }
    const magnitude =
        Math.min(1, raw) <= deadZone
          ? 0
          : (Math.min(1, raw) - deadZone) / (1 - deadZone),
      length = Math.hypot(x, y) || 1;
    state.x = (x / length) * magnitude;
    state.y = (y / length) * magnitude;
    knob.style.transform = `translate(${x * radius}px, ${y * radius}px)`;
  };
  stickResetters.push(() => release());
  element.addEventListener(
    'pointerdown',
    (event) => {
      if (pointerId !== null || !playing || paused || puzzleActive) return;
      event.preventDefault();
      event.stopPropagation();
      pointerId = event.pointerId;
      element.setPointerCapture(pointerId);
      element.classList.add('active');
      update(event);
    },
    { signal: controls.signal }
  );
  element.addEventListener(
    'pointermove',
    (event) => {
      if (event.pointerId !== pointerId) return;
      event.preventDefault();
      event.stopPropagation();
      update(event);
    },
    { signal: controls.signal }
  );
  for (const name of ['pointerup', 'pointercancel', 'lostpointercapture'])
    element.addEventListener(name, release, { signal: controls.signal });
}
function interact() {
  if (progress.completed === 3) return showFinish();
  travelToStation(progress.completed);
}
function bindControls() {
  $('start').onclick = startGame;
  $('interact').onclick = interact;
  $('wheelExit').onclick = closeMission;
  $('wheelPrev').onclick = () => rotateWheel(-1);
  $('wheelNext').onclick = () => rotateWheel(1);
  $('wheelConfirm').onclick = confirmWheel;
  $('hint').onclick = () =>
    notify(lesson.missions[Math.min(progress.completed, 2)].hint);
  $('recover').onclick = recoverHearts;
  $('pause').onclick = setPause;
  $('resume').onclick = setPause;
  $('replay').onclick = resetGame;
  $('returnMap').onclick = startGame;
  $('closeGame').onclick = () => post('close');
  bindThumbstick($('moveStick'), stickInput.move);
  bindThumbstick($('cameraStick'), stickInput.camera);
  $('sound').onclick = async () => {
    audioContext ??= new AudioContext();
    await audioContext.resume();
    soundEnabled = !soundEnabled;
    $('sound').setAttribute(
      'aria-label',
      soundEnabled ? 'إغلاق الصوت' : 'تشغيل الصوت'
    );
    $('sound').setAttribute('aria-pressed', String(soundEnabled));
    playChime();
  };
  document.addEventListener(
    'keydown',
    (event) => {
      if (!playing || paused) return;
      if (puzzleActive) {
        if (event.key.toLowerCase() === 'q') rotateWheel(-1);
        if (event.key.toLowerCase() === 'r') rotateWheel(1);
        if (event.key.toLowerCase() === 'e' && !event.repeat) confirmWheel();
        return;
      }
      const key = event.key.toLowerCase();
      if (
        [
          'arrowup',
          'arrowdown',
          'arrowleft',
          'arrowright',
          'w',
          'a',
          's',
          'd',
        ].includes(key)
      ) {
        event.preventDefault();
        pressedKeys.add(key);
      }
      if (key === 'e' && !event.repeat) interact();
    },
    { signal: controls.signal }
  );
  document.addEventListener(
    'keyup',
    (event) => pressedKeys.delete(event.key.toLowerCase()),
    { signal: controls.signal }
  );
  window.addEventListener(
    'blur',
    () => {
      pressedKeys.clear();
      resetVirtualControls();
    },
    { signal: controls.signal }
  );
  document.addEventListener(
    'visibilitychange',
    () => {
      pressedKeys.clear();
      resetVirtualControls();
      if (document.hidden && playing && !paused) setPause();
    },
    { signal: controls.signal }
  );
}
function animate(now) {
  if (disposed) return;
  const elapsed = Math.min((now - lastFrame) / 1000, 0.05);
  lastFrame = now;
  if (playing && !paused && !puzzleActive) updateMovement(elapsed);
  if (playing) {
    const moving =
      !paused &&
      !puzzleActive &&
      Boolean(
        destination ||
        pressedKeys.size ||
        Math.hypot(stickInput.move.x, stickInput.move.y)
      );
    const activeElapsed = paused ? 0 : elapsed;
    world3d.rotateCamera(
      stickInput.camera.x,
      stickInput.camera.y,
      activeElapsed
    );
    world3d.render(playerPosition, moving, progress.completed, activeElapsed);
    document.querySelectorAll('.station').forEach((station, index) => {
      const point = world3d.project(index);
      station.style.left = `${point.x}%`;
      station.style.top = `${point.y}%`;
      station.style.visibility = point.visible ? 'visible' : 'hidden';
    });
  }
  animationFrame = requestAnimationFrame(animate);
}
function dispose() {
  if (disposed) return;
  disposed = true;
  cancelAnimationFrame(animationFrame);
  controls.abort();
  clearTimeout(toastTimer);
  resetVirtualControls();
  pressedKeys.clear();
  world3d?.dispose?.();
  if (audioContext && audioContext.state !== 'closed')
    audioContext.close().catch(() => {});
}
function bootstrap(payload) {
  if (
    lesson ||
    !payload ||
    !validContent(payload.content) ||
    typeof payload.progressKey !== 'string' ||
    !payload.progressKey.startsWith('massar:mim-game:') ||
    payload.progressKey.length > 700 ||
    !['student', 'preview'].includes(payload.mode)
  ) {
    post('error', 'محتوى اللعبة غير صالح أو غير مكتمل.');
    return;
  }
  lesson = payload.content;
  storageKey = payload.progressKey;
  mode = payload.mode;
  lesson.missions.forEach((mission, index) => {
    mission.x = 20 + index * 30;
    mission.y = 45;
  });
  $('gameTitle').textContent = lesson.title;
  $('worldTitle').textContent = lesson.title;
  $('gameIntro').textContent = lesson.intro;
  $('sourceLabel').textContent = lesson.sourceLabel;
  $('modeNote').textContent =
    mode === 'preview'
      ? 'معاينة إدارية فقط، لا تنشر هذه الشاشة أي محتوى.'
      : 'للتدريب فقط، لا تؤثر على الدرجات أو الترتيب.';
  try {
    world3d = createWorld(
      $('world'),
      {
        rotate: rotateWheel,
        move: (point) => {
          if (!playing || paused || puzzleActive) return;
          pendingStation = null;
          destination = point;
        },
      },
      lesson
    );
  } catch {
    $('waiting').hidden = true;
    $('unsupported').hidden = false;
    post('error', 'هذا الجهاز لا يدعم تشغيل العالم ثلاثي الأبعاد حاليًا.');
    return;
  }
  restoreProgress();
  bindControls();
  renderMap();
  if (progress.solved.some((answers) => answers.length))
    $('start').textContent = 'كمّل المغامرة ←';
  $('waiting').hidden = true;
  $('shell').hidden = false;
  lastFrame = performance.now();
  animationFrame = requestAnimationFrame(animate);
}

window.addEventListener(
  'message',
  (event) => {
    if (
      event.origin !== location.origin ||
      event.source !== parent ||
      !event.data ||
      event.data.source !== 'massar-platform'
    )
      return;
    if (event.data.type === 'bootstrap') bootstrap(event.data.payload);
    if (event.data.type === 'dispose') dispose();
  },
  { signal: controls.signal }
);
window.addEventListener('pagehide', dispose, { once: true });
post('ready');
