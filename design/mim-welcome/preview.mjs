import { welcomeScenes } from "./welcome-scenes.mjs";
const byId = (id) => document.getElementById(id);
const voice = byId("voice");
const scene = byId("scene");
const reducedMotion = matchMedia("(prefers-reduced-motion: reduce)");
const visitStorageKey = "massar.mim.preview.firstWelcomeCompleted.v1";
let completedFirstWelcome = false;
try {
  completedFirstWelcome = localStorage.getItem(visitStorageKey) === "yes";
} catch (error) {
  if (error.name !== "SecurityError") throw error;
}
const automaticScene = completedFirstWelcome ? "returning" : "first";
const requestedMode = new URLSearchParams(location.search).get("welcome");
let selectedMode = ["first", "returning"].includes(requestedMode)
  ? requestedMode
  : "auto";
let selectedScene =
  welcomeScenes[selectedMode === "auto" ? automaticScene : selectedMode];
let cueIndex = -1;
function exitTime() {
  return Number.isFinite(voice.duration) ? voice.duration - 1.55 : Infinity;
}
let audioContext;
let analyser;
let samples;
let frame;
function connectVoice() {
  audioContext = new AudioContext();
  analyser = audioContext.createAnalyser();
  analyser.fftSize = 256;
  audioContext.createMediaElementSource(voice).connect(analyser);
  analyser.connect(audioContext.destination);
  samples = new Uint8Array(analyser.fftSize);
}
function showCue(seconds) {
  const cues = selectedScene.cues;
  const index = cues.findLastIndex((cue) => seconds >= cue.at);
  if (index === cueIndex) return;
  cueIndex = index;
  byId("eyebrow").textContent = cues[index].eyebrow;
  byId("headline").innerHTML = cues[index].title;
  byId("caption").textContent = cues[index].caption;
}
function positionCharacter(seconds) {
  const arrival = Math.min(seconds / 1.05, 1);
  const exit = Math.max(0, Math.min((seconds - exitTime()) / 0.85, 1));
  const walk = arrival < 1 || exit > 0;
  const horizontal = 155 * (1 - arrival) ** 3 - 165 * exit ** 2;
  const tilt = Math.sin(seconds * (walk ? 18 : 2.4)) * (walk ? 5 : 0.6);
  const lift = walk
    ? -Math.abs(Math.sin(seconds * 12)) * 9
    : Math.sin(seconds * 2.4) * 1.2;
  byId("character").style.opacity = String(
    seconds >= exitTime() + 0.85 ? 0 : 1,
  );
  byId("character").style.transform = reducedMotion.matches
    ? "none"
    : `translate(${horizontal}%, ${lift}px) rotate(${tilt}deg)`;
  byId("shadow").style.opacity = String((1 - exit) * arrival * 0.8);
  byId("shadow").style.transform = `scale(${0.7 + arrival * 0.3 - exit * 0.4})`;
}
function updateScene() {
  const seconds = voice.currentTime;
  const phase =
    seconds >= exitTime() + 0.85
      ? "ended"
      : seconds >= exitTime()
        ? "exit"
        : "playing";
  scene.dataset.phase = phase;
  byId("ending").setAttribute("aria-hidden", String(phase !== "ended"));
  showCue(seconds);
  positionCharacter(seconds);
  ["learn", "try", "solve"].forEach((id, index) =>
    byId(id).classList.toggle(
      "visible",
      selectedScene.learning !== null &&
        seconds >= selectedScene.learning[0] + index * 0.75 &&
        seconds < selectedScene.learning[1],
    ),
  );
  byId("progress").style.transform =
    `scaleX(${voice.duration ? seconds / voice.duration : 0})`;
  byId("time").textContent =
    `00:${String(Math.floor(seconds)).padStart(2, "0")}`;
}
function animateSpeech() {
  analyser.getByteTimeDomainData(samples);
  const amplitude = Math.sqrt(
    samples.reduce((sum, sample) => sum + ((sample - 128) / 128) ** 2, 0) /
      samples.length,
  );
  scene.classList.toggle(
    "speaking",
    amplitude > 0.015 &&
      !reducedMotion.matches &&
      voice.currentTime < exitTime(),
  );
  byId("lips").style.transform =
    `scaleY(${0.22 + Math.min(amplitude * 6, 0.8)})`;
  updateScene();
  frame = requestAnimationFrame(animateSpeech);
}
async function startPlayback() {
  try {
    if (!audioContext) connectVoice();
    await audioContext.resume();
    if (voice.ended) voice.currentTime = 0;
    await voice.play();
    byId("restart").hidden = false;
    byId("error").textContent = "";
  } catch (error) {
    byId("error").textContent =
      `تعذّر تشغيل الصوت، اضغط اسمع ميم للمحاولة مرة تانية. (${error.name})`;
  }
}
function stopAnimation() {
  cancelAnimationFrame(frame);
  scene.classList.remove("speaking");
  if (scene.dataset.phase === "ready") return;
  byId("playLabel").textContent = voice.ended
    ? "شغّل الترحيب تاني"
    : "كمّل مع ميم";
  byId("playIcon").textContent = "▶";
  if (scene.dataset.phase !== "ready") updateScene();
}
voice.addEventListener("play", () => {
  byId("playLabel").textContent = "إيقاف مؤقت";
  byId("playIcon").textContent = "Ⅱ";
  cancelAnimationFrame(frame);
  animateSpeech();
});
voice.addEventListener("pause", stopAnimation);
voice.addEventListener("ended", () => {
  stopAnimation();
  if (selectedMode !== "auto" || selectedScene !== welcomeScenes.first) return;
  try {
    localStorage.setItem(visitStorageKey, "yes");
  } catch (error) {
    if (!["SecurityError", "QuotaExceededError"].includes(error.name))
      throw error;
    byId("visitNote").textContent =
      "الحفظ غير متاح في المتصفح ده. تقدر تجرّب الوضعين يدويًا.";
    return;
  }
  byId("visitNote").textContent =
    "الترحيب الأول اتسجّل في المعاينة. افتح الرابط تاني لتجربة ترحيب الرجوع.";
});
voice.addEventListener("error", () => {
  byId("error").textContent = "الصوت لم يتحمّل. أعد تحميل الصفحة وحاول تاني.";
});
byId("play").addEventListener("click", () =>
  voice.paused ? startPlayback() : voice.pause(),
);
byId("restart").addEventListener("click", () => {
  voice.currentTime = 0;
  startPlayback();
});
byId("mute").addEventListener("click", () => {
  voice.muted = !voice.muted;
  byId("mute").setAttribute(
    "aria-label",
    voice.muted ? "تشغيل الصوت" : "كتم الصوت",
  );
  byId("mute").setAttribute("aria-pressed", String(voice.muted));
});
document.addEventListener("visibilitychange", () => {
  if (document.hidden) voice.pause();
});

function resetPreview() {
  cueIndex = -1;
  scene.dataset.phase = "ready";
  scene.classList.remove("speaking");
  byId("ending").setAttribute("aria-hidden", "true");
  byId("eyebrow").textContent = selectedScene.readyEyebrow;
  byId("headline").innerHTML = selectedScene.readyTitle;
  byId("endTitle").innerHTML = selectedScene.endTitle;
  byId("endMessage").textContent = selectedScene.endMessage;
  byId("playLabel").textContent = "اسمع ميم";
  byId("playIcon").textContent = "▶";
  byId("progress").style.transform = "scaleX(0)";
  byId("character").style.opacity = "";
  byId("character").style.transform = "";
  byId("shadow").style.opacity = "0";
  byId("restart").hidden = true;
  byId("time").textContent = "00:00";
  ["learn", "try", "solve"].forEach((id) =>
    byId(id).classList.remove("visible"),
  );
}
function selectScene() {
  voice.pause();
  cancelAnimationFrame(frame);
  selectedMode = byId("visitChoice").value;
  selectedScene =
    welcomeScenes[selectedMode === "auto" ? automaticScene : selectedMode];
  voice.src = selectedScene.audio;
  voice.load();
  resetPreview();
  byId("visitNote").textContent =
    selectedMode === "auto"
      ? `الوضع التلقائي: ${selectedScene.label}. بعد مشاهدة الترحيب الأول كاملًا، الزيارات التالية تعرض ترحيب الرجوع.`
      : `تجربة ${selectedScene.label} بصوت Charon. الاختيار اليدوي لا يغيّر سجل المعاينة.`;
}
byId("visitChoice").value = selectedMode;
byId("visitChoice").addEventListener("change", selectScene);
selectScene();

byId("play").disabled = false;

if (document.body.classList.contains("embedded"))
  document.body.dataset.welcome = selectedMode;
