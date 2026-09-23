import {
  lessons,
  newLessonProgress,
  readProfile,
  earnedItems,
} from './preview-data.mjs';
import { destinations } from './preview-environments.mjs';
import { createPreviewWorld } from './preview-world.mjs';
import { bindPreviewControls } from './preview-controls.mjs';

const $ = (id) => document.getElementById(id);
const number = (value) => value.toLocaleString('ar-EG');
const preferencesKey = 'massar:mim-standalone-preview:preferences';
let profileId = 'explorer',
  lessonIndex = 0,
  profile,
  world,
  inputs,
  attempt = null,
  currentMission = 0,
  selectedDestination = -1;
let paused = false,
  destination = null,
  pendingMission = null,
  frame,
  last = performance.now(),
  toastTimer;
let player = { x: 50, y: 80 };
const lesson = () => lessons[lessonIndex];
const progress = () => profile.lessons[lesson().id];
const storageKey = () => `massar:mim-standalone-preview:v1:${profileId}`;
const canMove = () =>
  !paused &&
  !attempt &&
  !$('journal').open &&
  $('conversation').hidden &&
  !document.hidden;
const element = (tag, text, className) => {
  const node = document.createElement(tag);
  if (text !== undefined) node.textContent = text;
  if (className) node.className = className;
  return node;
};
function button(text, action, className = 'quiet') {
  const node = element('button', text, className);
  node.type = 'button';
  node.onclick = action;
  return node;
}
function tell(text) {
  clearTimeout(toastTimer);
  $('toast').textContent = text;
  $('toast').hidden = false;
  toastTimer = setTimeout(() => {
    $('toast').hidden = true;
  }, 6500);
}
function loadProfile() {
  try {
    profile = readProfile(localStorage, storageKey());
  } catch (error) {
    console.warn('Preview save recovery:', error.message);
    profile = { version: 1, lessons: {}, badge: '' };
    tell('تعذّر استعادة الحفظ. تقدر تبدأ تجربة جديدة.');
  }
  profile.lessons[lesson().id] ??= newLessonProgress();
}
function save() {
  try {
    localStorage.setItem(storageKey(), JSON.stringify(profile));
    localStorage.setItem(
      preferencesKey,
      JSON.stringify({ profileId, lessonIndex })
    );
    $('saveStatus').textContent =
      'معاينة محلية · التقدم محفوظ لهذا الملف على الجهاز';
  } catch (error) {
    console.warn('Preview save unavailable:', error.message);
    $('saveStatus').textContent = 'الحفظ غير متاح · الجلسة الحالية فقط';
  }
}
function updateHud() {
  const completed = progress().completed;
  world?.setLessonTitles(lesson().missions.map((mission) => mission.title));
  $('journeyProgress').value = completed;
  $('journeyCaption').textContent =
    `${lesson().title} · ${number(completed)} / ٤`;
  $('questTitle').textContent =
    completed === 4
      ? 'فتحت قاعة الاكتشاف!'
      : completed === 3
        ? 'القفل الأخير'
        : lesson().missions[completed].title;
  $('questText').textContent = [
    'استعد لتركيب جسر المعرفة.',
    'الجسر اكتمل. شغّل آلة الأسباب.',
    'الآلة اشتغلت. رجّع قطع المعرض.',
    '٣ مفاتيح أخيرة لفتح القاعة.',
    'مقتنياتك محفوظة للرحلة الجاية.',
  ][completed];
  const items = earnedItems(profile),
    badge = items.find((item) => item.id === profile.badge);
  $('equippedBadge').textContent = badge
    ? `${badge.icon} ${badge.title}`
    : 'مستكشف المعرفة';
  world?.setBadge(badge);
  $('collectionCount').textContent =
    `${number(items.length)} مقتنيات · افتح الحقيبة`;
  $('seals').replaceChildren(
    ...lesson().missions.map((mission, index) => {
      const seal = button(
        mission.icon,
        showCollection,
        `seal ${index < completed ? 'earned' : ''}`
      );
      seal.setAttribute('aria-label', mission.reward);
      return seal;
    })
  );
  $('interact').textContent =
    completed === 4
      ? 'شوف إنجازك'
      : completed === 3
        ? 'افتح القفل الأخير'
        : 'اذهب للمهمة ←';
  renderStations();
}
function renderStations() {
  $('stations').replaceChildren(
    ...lesson().missions.map((mission, index) => {
      const station = button(
        '',
        () => (index < progress().completed ? showRecap(index) : travel(index)),
        `station ${index < progress().completed ? 'complete' : ''}`
      );
      const marker = element('span', undefined, 'marker');
      marker.append(
        element('i', index < progress().completed ? '✓' : mission.icon)
      );
      station.append(marker, element('span', mission.title, 'station-label'));
      station.disabled = index > progress().completed;
      return station;
    })
  );
}
function talk(title, text, actionText, action) {
  inputs?.reset();
  destination = null;
  pendingMission = null;
  $('conversationTitle').textContent = title;
  $('conversationText').textContent = text;
  $('conversationContinue').textContent = actionText;
  $('conversationContinue').onclick = () => {
    $('conversation').hidden = true;
    $('world').classList.remove('has-conversation');
    action();
  };
  $('conversation').hidden = false;
  $('world').classList.add('has-conversation');
}
function beginLesson() {
  closePuzzle();
  visitDestination(-1);
  player = { x: 50, y: 80 };
  updateHud();
  save();
  if (progress().completed === 4)
    return talk(
      'أهلًا برجوعك!',
      'قاعة الاكتشاف مفتوحة. تقدر تستكشف المتحف أو تبدأ الحصة التالية.',
      'شوف إنجازك',
      showSummary
    );
  talk(
    'ميم محتاج مساعدتك',
    progress().completed
      ? 'رجعت في وقتك! تقدمك محفوظ، هنكمّل الرحلة من آخر مهمة.'
      : lesson().story,
    'يلا نكمّل الرحلة ←',
    () => travel(progress().completed)
  );
}
function visitDestination(index) {
  selectedDestination = index;
  world.showDestination(index);
  const theme = destinations[index];
  document.body.dataset.destination = theme?.id ?? 'hub';
  $('worldName').textContent = theme?.title ?? 'متحف المعرفة';
  $('world').setAttribute('aria-label', `${theme?.title ?? 'متحف المعرفة'}. الأسهم للحركة والسحب للكاميرا.`);
  $('interact').disabled = index > progress().completed;
  $('interact').textContent = index > progress().completed ? 'أكمل العالم السابق لفتح اللغز' : index >= 0 ? 'ابدأ لغز العالم' : 'كمّل الرحلة ←';
  player = { x: 50, y: 80 };
}
function showWorldMap() {
  const routes = destinations.map((place, index) => {
    const route = button('', () => {
      if (place.id === 'factory') {
        location.assign('factory.html');
        return;
      }
      $('journal').close();
      closePuzzle();
      $('conversation').hidden = true;
      $('world').classList.remove('has-conversation');
      pendingMission = destination = null;
      visitDestination(index);
    }, `world-route world-route-${place.id}`);
    route.append(element('strong', place.title), element('span', place.description),
      element('small', index < progress().completed ? 'اكتملت المغامرة ✓' : index === progress().completed ? 'لغزك التالي · ادخل العالم ←' : 'استكشف العالم · اللغز يفتح بعد السابق'));
    return route;
  });
  journal('خريطة المغامرات', ...routes, button('ارجع إلى المتحف', () => {
    $('journal').close();
    closePuzzle();
    $('conversation').hidden = true;
    $('world').classList.remove('has-conversation');
    pendingMission = destination = null;
    visitDestination(-1);
  }));
}
function travel(index) {
  if (index === 4) return showSummary();
  if (index < progress().completed) return showRecap(index);
  visitDestination(index < 3 ? index : -1);
  destination = { x: index < 3 ? 20 + index * 30 : 50, y: 58 };
  pendingMission = index;
}
function openMission(index) {
  if (index !== progress().completed || index > 3) return;
  currentMission = index;
  inputs.reset();
  destination = null;
  pendingMission = null;
  if (index === 3) {
    talk(
      'سرّ قاعة الاكتشاف',
      `قفل القاعة فيه ٣ مفاتيح. فتحت منهم ${number(progress().finalStep)}. هنستخدم الترتيب والتوصيل والتصنيف.`,
      'ابدأ المفتاح التالي ←',
      () => startPuzzle(lesson().final[progress().finalStep])
    );
  } else startPuzzle(lesson().missions[index]);
}
function startPuzzle(puzzle) {
  visitDestination({ order: 0, match: 1, sort: 2 }[puzzle.type]);
  attempt = {
    puzzle,
    order: [],
    pairs: [],
    selected: null,
    categories: Array(puzzle.items?.length ?? 0).fill(-1),
  };
  $('world').classList.add('mechanism-mode');
  $('puzzleHud').hidden = false;
  $('puzzleTitle').textContent =
    currentMission === 3
      ? `القفل الأخير · المفتاح ${number(progress().finalStep + 1)}`
      : puzzle.title;
  $('puzzleInstruction').textContent = {
    order: 'اضغط الألواح الحجرية بالترتيب الصحيح لبناء الجسر.',
    match: 'اضغط السبب على اليمين، ثم نتيجته على اليسار.',
    sort: 'اضغط العيّنة، ثم الصندوق المناسب لوضعها بداخله.',
  }[puzzle.type];
  feedback('اختار القطع قدامك بالضغط عليها.', '');
  syncPuzzle();
}
function feedback(text, kind) {
  $('feedback').textContent = text;
  $('feedback').className = kind;
}
function syncPuzzle() {
  world.showPuzzle(attempt);
  buildTargets();
  const { puzzle, order, pairs, categories } = attempt;
  const count =
    puzzle.type === 'order'
      ? order.length
      : puzzle.type === 'match'
        ? pairs.length
        : categories.filter((value) => value >= 0).length;
  const total =
    puzzle.type === 'match' ? puzzle.pairs.length : puzzle.items.length;
  $('puzzleCount').textContent = `${number(count)} / ${number(total)}`;
  $('confirm').disabled = count !== total;
  $('confirm').textContent = {
    order: 'ثبّت الترتيب',
    match: 'شغّل الآلة',
    sort: 'ثبّت التصنيف',
  }[puzzle.type];
  $('undo').hidden = puzzle.type !== 'order';
  $('undo').disabled = !order.length;
}
function buildTargets() {
  const targets = world.puzzleTargets();
  $('worldTargets').replaceChildren(
    ...targets.map((target) => {
      const control = button('', () => choose(target.action), 'world-target');
      control.setAttribute('aria-label', target.label);
      control.setAttribute('aria-pressed', String(Boolean(target.selected)));
      control.append(element('span', target.label, 'sr-only'));
      return control;
    })
  );
  positionTargets(targets);
}
function positionTargets(targets = world.puzzleTargets()) {
  [...$('worldTargets').children].forEach((node, index) => {
    const target = targets[index];
    if (!target) return;
    Object.assign(node.style, {
      left: `${target.x}%`,
      top: `${target.y}%`,
      width: `${target.width}%`,
      height: `${target.height}%`,
      visibility: target.visible ? 'visible' : 'hidden',
    });
  });
}
const selections = {
  order(index) {
    if (!attempt.order.includes(index)) attempt.order.push(index);
  },
  cause(index) {
    attempt.selected = index;
  },
  result(index) {
    if (attempt.selected === null) return;
    if (attempt.selected !== index) {
      wrong('الوصلة مش مظبوطة. جرّب نتيجة تانية، أو اسأل ميم.');
      return;
    }
    if (!attempt.pairs.includes(index)) attempt.pairs.push(index);
    attempt.selected = null;
    feedback('وصلة صحيحة! شوف الأنبوب نور إزاي.', 'success');
  },
  sample(index) {
    attempt.selected = index;
  },
  category(index) {
    if (attempt.selected !== null) {
      attempt.categories[attempt.selected] = index;
      attempt.selected = null;
      feedback('اتنقلت العيّنة. كمّل التصنيف وبعدين ثبّت اختياراتك.', '');
    }
  },
};
function choose(action) {
  if (!attempt || paused || $('journal').open) return;
  selections[action.kind](action.index);
  syncPuzzle();
}
function helpIndex() {
  return currentMission === 3
    ? Math.min(progress().finalStep, 2)
    : Math.min(currentMission, 2);
}
function wrong(text) {
  progress().mistakes[helpIndex()]++;
  if (currentMission === 3) progress().finalMistakes++;
  feedback(text, 'error');
  save();
  if (progress().mistakes[helpIndex()] >= 2)
    tell('ميم: نراجع الفكرة؟ اضغط علامة الكتاب فوق، وهنرجع لنفس اللغز.');
}
function confirmPuzzle() {
  if (!attempt || $('confirm').disabled) return;
  const { puzzle, order, categories } = attempt;
  if (
    puzzle.type === 'order' &&
    !order.every((value, index) => value === index)
  )
    return wrong('في لوح مش في مكانه. استخدم تراجع، وركّب الترتيب من جديد.');
  if (
    puzzle.type === 'sort' &&
    !categories.every((value, index) => value === puzzle.answers[index])
  )
    return wrong('في عيّنة محتاجة صندوق تاني. اضغط عليها وانقلها.');
  world.celebrate();
  if (currentMission === 3) {
    progress().finalStep++;
    if (progress().finalStep === 3) progress().completed = 4;
    save();
    closePuzzle();
    updateHud();
    if (progress().completed === 4)
      return talk(
        'قاعة الاكتشاف اتفتحت!',
        'كل القطع رجعت لمكانها. بص وراك: باب المتحف اتفتح بفهمك!',
        'شوف اللي اتعلمته',
        showSummary
      );
    return talk(
      'مفتاح جديد اتفتح!',
      `معاك ${number(progress().finalStep)} من ٣ مفاتيح. نكمّل؟`,
      'المفتاح التالي ←',
      () => startPuzzle(lesson().final[progress().finalStep])
    );
  }
  const mission = lesson().missions[currentMission];
  progress().completed = currentMission + 1;
  if (!profile.badge) profile.badge = `${lesson().id}:${currentMission}`;
  save();
  closePuzzle();
  updateHud();
  talk(`جمعت ${mission.reward}!`, mission.success, 'كمّل الاستكشاف ←', () =>
    travel(progress().completed)
  );
}
function closePuzzle() {
  attempt = null;
  world?.hidePuzzle();
  $('worldTargets').replaceChildren();
  $('puzzleHud').hidden = true;
  $('world').classList.remove('mechanism-mode');
  inputs?.reset();
}
function revealHint() {
  const index = helpIndex(),
    hints = lesson().missions[index].hints,
    used = progress().hints[index];
  progress().hints[index] = Math.min(used + 1, hints.length);
  save();
  tell(`ميم: ${hints[Math.min(used, hints.length - 1)]}`);
}
function journal(title, ...children) {
  inputs?.reset();
  $('journalTitle').textContent = title;
  $('journalBody').replaceChildren(...children);
  if (!$('journal').open) $('journal').showModal();
}
function showRecap(index = helpIndex()) {
  const mission = lesson().missions[index];
  journal(
    `دفتر ميم · ${mission.concept}`,
    element('p', `شرح تجريبي · الموضع ${mission.time}`),
    ...mission.recap.map((text, index) =>
      element('div', `${number(index + 1)}. ${text}`, 'journal-recap')
    ),
    element(
      'p',
      'المعاينة محلية: هنا شرح مكتوب بدل مقطع الفيديو. اضغط «ارجع للعبة» لتكمل نفس اللغز.'
    )
  );
}
function showCollection() {
  const items = earnedItems(profile);
  const rows = items.map((item) => {
    const row = element('div', undefined, 'journal-row'),
      detail = element('div');
    detail.append(element('strong', item.title), element('small', item.lesson));
    row.append(
      element('span', item.icon, 'glyph'),
      detail,
      button(profile.badge === item.id ? 'شارتك ✓' : 'البس الشارة', () => {
        profile.badge = item.id;
        save();
        updateHud();
        showCollection();
      })
    );
    return row;
  });
  journal(
    'حقيبة ميم',
    element(
      'p',
      items.length
        ? `${number(items.length)} قطع من رحلاتك. اختار قطعة لتظهر كشارتك.`
        : 'لسه أول قطعة مستنياك عند جسر المعرفة.'
    ),
    ...rows
  );
}
function showSummary() {
  const rows = lesson().missions.map((mission, index) => {
    const row = element('div', undefined, 'journal-row'),
      detail = element('div');
    detail.append(
      element('strong', mission.concept),
      element(
        'small',
        progress().mistakes[index] || progress().hints[index]
          ? 'محتاجة مراجعة بسيطة'
          : 'حلّيتها من أول مرة'
      )
    );
    row.append(
      detail,
      button('راجع', () => showRecap(index))
    );
    return row;
  });
  const actions = element('div', undefined, 'summary-actions');
  actions.append(
    button('افتح حقيبة المقتنيات', showCollection),
    button(
      'الرحلة التالية ←',
      () => {
        $('journal').close();
        switchLesson((lessonIndex + 1) % lessons.length);
      },
      'primary'
    )
  );
  journal('ميم: المتحف نور بفهمك!', ...rows, actions);
}
function showSettings() {
  const profileLabel = element('label', 'ملف تجريبي'),
    select = element('select');
  select.id = 'profileSelect';
  for (const [id, title] of [
    ['explorer', 'المستكشف الأول'],
    ['visitor', 'المستكشف الثاني'],
  ]) {
    const option = element('option', title);
    option.value = id;
    select.append(option);
  }
  select.value = profileId;
  select.onchange = () => {
    profileId = select.value;
    loadProfile();
    $('journal').close();
    beginLesson();
  };
  profileLabel.append(select);
  const lessonLabel = element('label', 'الرحلة'),
    lessonSelect = element('select');
  lessonSelect.id = 'lessonSelect';
  lessons.forEach((item, index) => {
    const option = element('option', item.title);
    option.value = index;
    lessonSelect.append(option);
  });
  lessonSelect.value = lessonIndex;
  lessonSelect.onchange = () => {
    $('journal').close();
    switchLesson(Number(lessonSelect.value));
  };
  lessonLabel.append(lessonSelect);
  journal(
    'مساحة التجربة',
    element(
      'p',
      'معاينة محلية منفصلة. تقدمك ومقتنياتك محفوظين في المتصفح، ومفيش اتصال بحسابات الطلاب.'
    ),
    profileLabel,
    lessonLabel,
    button('ابدأ الحصة من جديد', () => {
      profile.lessons[lesson().id] = newLessonProgress();
      if (profile.badge.startsWith(`${lesson().id}:`)) profile.badge = '';
      $('journal').close();
      beginLesson();
    })
  );
}
function switchLesson(index) {
  lessonIndex = index;
  profile.lessons[lesson().id] ??= newLessonProgress();
  currentMission = Math.min(progress().completed, 3);
  beginLesson();
}
function movePlayer(dt) {
  const movement = inputs.movement();
  if (movement.x || movement.y) {
    destination = null;
    pendingMission = null;
    const relative = world.cameraRelativeMovement(movement.x, movement.y),
      scale = Math.max(1, Math.hypot(relative.x, relative.y));
    player.x += (relative.x / scale) * dt * 24;
    player.y += (relative.y / scale) * dt * 30;
  } else if (destination) {
    const dx = destination.x - player.x,
      dy = destination.y - player.y,
      distance = Math.hypot(dx, dy);
    if (distance < 0.6) {
      const mission = pendingMission;
      destination = null;
      pendingMission = null;
      if (mission !== null) openMission(mission);
    } else {
      const step = Math.min(distance, dt * 30);
      player.x += (dx / distance) * step;
      player.y += (dy / distance) * step;
    }
  }
  player.x = Math.max(7, Math.min(93, player.x));
  player.y = Math.max(58, Math.min(88, player.y));
  return Boolean(movement.x || movement.y || destination);
}
function animate(now) {
  frame = requestAnimationFrame(animate);
  const dt = Math.min((now - last) / 1000, 0.05);
  last = now;
  if (document.hidden) return;
  const moving = canMove() ? movePlayer(dt) : false,
    active = paused || $('journal').open ? 0 : dt;
  if (canMove()) world.rotateCamera(inputs.camera.x, inputs.camera.y, active);
  world.render(player, moving, progress().completed, active);
  [...$('stations').children].forEach((node, index) => {
    const point = world.project(index);
    Object.assign(node.style, {
      left: `${point.x}%`,
      top: `${point.y}%`,
      visibility: point.visible ? 'visible' : 'hidden',
    });
  });
  if (attempt) positionTargets();
}
function boot() {
  try {
    const saved = JSON.parse(localStorage.getItem(preferencesKey) ?? 'null');
    if (['explorer', 'visitor'].includes(saved?.profileId))
      profileId = saved.profileId;
    if (Number.isInteger(saved?.lessonIndex) && lessons[saved.lessonIndex])
      lessonIndex = saved.lessonIndex;
  } catch (error) {
    console.warn('Preview preferences unavailable:', error.message);
  }
  loadProfile();
  try {
    world = createPreviewWorld(
      $('world'),
      {
        select: choose,
        move: (point) => {
          if (canMove()) {
            destination = point;
            pendingMission = null;
          }
        },
      },
      { title: 'متحف المعرفة', missions: lesson().missions }
    );
  } catch (error) {
    console.error('Museum unavailable:', error);
    $('waiting').hidden = true;
    $('unsupported').hidden = false;
    return;
  }
  inputs = bindPreviewControls(canMove);
  $('waiting').hidden = true;
  $('shell').hidden = false;
  currentMission = Math.min(progress().completed, 3);
  beginLesson();
  frame = requestAnimationFrame(animate);
}
$('confirm').onclick = confirmPuzzle;
$('undo').onclick = () => {
  if (attempt?.puzzle.type === 'order') {
    attempt.order.pop();
    syncPuzzle();
  }
};
$('exitPuzzle').onclick = closePuzzle;
$('hint').onclick = revealHint;
$('recapButton').onclick = () => showRecap();
$('collectionButton').onclick = showCollection;
$('settingsButton').onclick = showSettings;
$('closeJournal').onclick = () => {
  $('journal').close();
  $('world').focus({ preventScroll: true });
};
$('interact').onclick = () => travel(selectedDestination < 0 ? progress().completed : selectedDestination);
$('mapButton').onclick = showWorldMap;
$('pause').onclick = () => {
  paused = true;
  inputs.reset();
  journal(
    'ناخد نفس؟',
    element('p', 'تقدمك محفوظ. نكمّل المغامرة لما تكون جاهز.'),
    button('كمّل المغامرة', () => {
      $('journal').close();
    })
  );
};
$('journal').addEventListener('close', () => {
  paused = false;
  inputs?.reset();
});
$('portraitContinue').onclick = () => {
  $('rotatePrompt').hidden = true;
};
const portrait = matchMedia(
  '(orientation: portrait) and (max-width: 700px) and (pointer: coarse)'
);
$('rotatePrompt').hidden = !portrait.matches;
portrait.addEventListener('change', () => {
  $('rotatePrompt').hidden = !portrait.matches;
});
window.addEventListener(
  'pagehide',
  () => {
    cancelAnimationFrame(frame);
    clearTimeout(toastTimer);
    inputs?.dispose();
    world?.dispose();
  },
  { once: true }
);
boot();
