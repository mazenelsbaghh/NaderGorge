import { welcomeScenes, lipEnvelopes } from "./welcome-scenes.mjs";
const mode = new URLSearchParams(location.search).get("welcome") === "first" ? "first" : "returning";
const welcome = welcomeScenes[mode];
const scene = document.getElementById("scene");
const character = document.getElementById("character");
const skip = document.getElementById("skipWelcome");
const reducedMotion = matchMedia("(prefers-reduced-motion: reduce)");
const duration = mode === "first" ? 15900 : 12650;
let frame;
let dismissed = false;
let cueIndex = -1;
const voice = document.getElementById("voice");
const autoplayNote = document.getElementById("autoplayNote");
voice.src = welcome.audio;
let startedAt;
let starting = false;
document.body.dataset.welcome = mode;
const pose = new URLSearchParams(location.search).get("pose") || "bottom";
document.body.dataset.pose = pose;
const sprites = {
  bottom: { x: 565, y: 623, angle: 7, scale: 1.12 },
  right: { x: 582, y: 625, angle: -15, scale: 1.12 },
  left: { x: 606, y: 651, angle: 15, scale: 1.12 },
};
const sprite = sprites[pose] || sprites.bottom;
character.querySelector("img").src = `mim-${pose in sprites ? pose : "bottom"}.png`;
const mouth = character.querySelector(".mouth");
const mouthArtwork = document.createElementNS("http://www.w3.org/2000/svg", "g");
for (const child of [...mouth.children]) if (child.tagName !== "defs") mouthArtwork.append(child);
mouthArtwork.setAttribute("transform", `translate(${sprite.x} ${sprite.y}) rotate(${sprite.angle}) scale(${sprite.scale}) translate(-623 -675)`);
mouth.append(mouthArtwork);
let mouthOpen = 0;
scene.dataset.phase = "playing";
document.getElementById("endTitle").innerHTML = welcome.endTitle;
document.getElementById("endMessage").textContent = welcome.endMessage;
let skipTimer;
function stop() {
  dismissed = true;
  voice.pause();
  cancelAnimationFrame(frame);
  clearTimeout(skipTimer);
}
function dismiss() {
  if (dismissed) return;
  stop();
  parent.postMessage({ type: "mim-dismiss" }, location.origin);
}
function tick(now) {
  if (dismissed) return;
  const elapsed = voice.ended ? duration : voice.currentTime * 1000;
  const seconds = elapsed / 1000;
  const amplitude = voice.paused || voice.ended ? 0 : (lipEnvelopes[mode][Math.floor(elapsed / 40)] || 0);
  mouthOpen += (amplitude - mouthOpen) * .55;
  scene.classList.toggle("speaking", !reducedMotion.matches && mouthOpen > .025);
  document.getElementById("lips").style.transform = `scale(${1 - mouthOpen * .12}, ${.18 + mouthOpen * 1.1})`;
  const index = welcome.cues.findLastIndex(cue => seconds >= cue.at);
  if (index !== cueIndex && index >= 0) {
    cueIndex = index;
    const cue = welcome.cues[index];
    document.getElementById("eyebrow").textContent = cue.eyebrow;
    document.getElementById("headline").innerHTML = cue.title;
    document.getElementById("caption").textContent = cue.caption;
  }
  if (!reducedMotion.matches) {
    character.style.transform = `translateY(${Math.sin(seconds * 2.4) * 3}px) rotate(${(pose === "right" ? -3 : pose === "left" ? 3 : 0) + Math.sin(seconds * 2.4) * .8}deg)`;
  }
  ["learn", "try", "solve"].forEach((id, index) => {
    document.getElementById(id).classList.toggle("visible", welcome.learning !== null && seconds >= welcome.learning[0] + index * .75 && seconds < welcome.learning[1]);
  });
  if (elapsed >= duration - 1700) {
    scene.dataset.phase = "ended";
    document.getElementById("ending").setAttribute("aria-hidden", "false");
  }
  if (elapsed >= duration) return dismiss();
  frame = requestAnimationFrame(tick);
}
skip.addEventListener("click", dismiss);
window.addEventListener("pagehide", stop);
window.addEventListener("message", event => {
  if (event.origin === location.origin && event.source === parent && event.data?.type === "mim-stop") stop();
});
document.addEventListener("keydown", event => {
  if (event.key === "Escape") { event.preventDefault(); dismiss(); }
});
async function start() {
  if (dismissed || starting || startedAt !== undefined) return;
  starting = true;
  try {
    await voice.play();
    startedAt = performance.now();
    autoplayNote.hidden = true;
    skipTimer = setTimeout(() => { skip.hidden = false; }, 60000);
    frame = requestAnimationFrame(tick);
  } catch (error) {
    autoplayNote.textContent = error.name === "NotAllowedError"
      ? "اضغط في أي مكان لبدء الترحيب بالصوت"
      : "تعذّر تحميل الصوت. اضغط للمحاولة تاني.";
    autoplayNote.hidden = false;
  } finally {
    starting = false;
  }
}
document.addEventListener("pointerdown", event => {
  if (!event.target.closest("#skipWelcome")) void start();
});
void start();
