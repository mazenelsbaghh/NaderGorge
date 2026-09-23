const scenario =
  new URLSearchParams(location.search).get("welcome") === "first"
    ? "first"
    : "returning";
const slot = document.getElementById("welcomeSlot");
const frame = document.getElementById("welcomeFrame");
slot.classList.toggle("first", scenario === "first");

const reduceMotion = matchMedia("(prefers-reduced-motion: reduce)");
let closing = false;
const poses = ["bottom", "right", "left"];
const requestedPose = new URLSearchParams(location.search).get("pose");
let pose = poses.includes(requestedPose) ? requestedPose : poses[Math.floor(Math.random() * poses.length)];
if (!poses.includes(requestedPose)) {
  try {
    const previous = sessionStorage.getItem("mim.preview.pose");
    const choices = poses.filter(value => value !== previous);
    pose = choices[Math.floor(Math.random() * choices.length)];
    sessionStorage.setItem("mim.preview.pose", pose);
  } catch { /* Random selection also works without browser storage. */ }
}
slot.dataset.pose = pose;
const entryTransform = { bottom: "translateY(160px) scale(.92)", right: "translateX(180px) rotate(-6deg) scale(.94)", left: "translateX(-180px) rotate(6deg) scale(.94)" }[pose];
const exitTransform = { bottom: "translateY(100px) scale(.96)", right: "translateX(140px) rotate(-4deg)", left: "translateX(-140px) rotate(4deg)" }[pose];
async function dismissWelcome() {
  if (!slot.open || closing) return;
  closing = true;
  frame.contentWindow.postMessage({ type: "mim-stop" }, location.origin);
  slot.classList.add("closing");
  await slot.animate(
    [
      { opacity: 1, filter: "blur(0px)", transform: "translateY(0) scale(1)" },
      { opacity: 0, filter: "blur(14px)", transform: exitTransform },
    ],
    {
      duration: reduceMotion.matches ? 0 : 480,
      easing: "cubic-bezier(.4,0,1,1)",
    },
  ).finished;
  slot.close();
  slot.classList.remove("closing");
  closing = false;
}
function openWelcome() {
  if (slot.open || closing) return;
  frame.src = `embedded.html?welcome=${scenario}&pose=${pose}`;
  slot.showModal();
  slot.animate(
    [
      { opacity: 0, filter: "blur(16px)", transform: entryTransform },
      { opacity: 1, filter: "blur(0px)", transform: "translateY(0) scale(1)" },
    ],
    {
      duration: reduceMotion.matches ? 0 : 700,
      easing: "cubic-bezier(.16,1,.3,1)",
    },
  );
}
window.addEventListener("message", (event) => {
  if (event.origin !== location.origin || event.source !== frame.contentWindow)
    return;
  if (event.data?.type === "mim-dismiss") void dismissWelcome();
});
slot.addEventListener("cancel", (event) => {
  event.preventDefault();
  void dismissWelcome();
});
openWelcome();
if (scenario === "first") {
  document.getElementById("lessonTitle").textContent = "ابدأ أول درس في رحلتك";
  document.getElementById("lessonDescription").textContent =
    "دروسك جاهزة، اختر أول حصة وابدأ على مهلك.";
  document.getElementById("continueButton").textContent =
    "افتح محتواك الدراسي ↖";
}
document.getElementById("continueButton").addEventListener("click", () => {
  document.getElementById("demoStatus").textContent =
    "في المنصة، الزر ده يفتح الحصة الفعلية. هنا بنعرض مكان الترحيب ببيانات تجريبية.";
  document.getElementById("packages").scrollIntoView({ block: "center" });
});
document.getElementById("menuButton").addEventListener("click", () => {
  document.getElementById("demoStatus").textContent =
    "القائمة الفعلية تضم المدرسين، درجاتي، أخطائي، الإشعارات والرصيد.";
  document.getElementById("demoStatus").scrollIntoView({ block: "center" });
});
