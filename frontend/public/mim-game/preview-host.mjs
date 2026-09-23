// Preview-only host; original source and gameplay remain unchanged.
const original = document.getElementById('original');
const switchWing = document.getElementById('switchWing');
const previewNote = document.getElementById('previewNote');
let expansion = null;
let resumeOriginal = false;
const examples = [
  ['رحلة الفتات', ['التجوية', 'التعرية', 'الترسيب'], ['تفتت الصخور في مكانها', 'نقل الفتات بالماء أو الرياح', 'استقرار الفتات في مكان جديد']],
  ['تكوّن الصخور', ['ناري', 'رسوبي', 'متحوّل'], ['صخر يتكوّن من تبريد الصهارة', 'صخر يتكوّن من تلاحم الرواسب', 'صخر تغيّر بالحرارة والضغط دون انصهار']],
  ['عينات المتحف', ['ناري', 'رسوبي', 'متحوّل'], ['الجرانيت', 'الحجر الرملي', 'الرخام']],
];
const content = {
  schemaVersion: 1, title: 'متحف الصخور', intro: 'استكشف المتحف وحل ألغاز ميم.',
  sourceLabel: 'أسئلة تجريبية للمعاينة المحلية',
  missions: examples.map(([title, choices, labels], index) => ({
    title, instruction: 'لف عجلة الاختيارات حتى تختار الإجابة المناسبة، ثم ثبّتها.',
    hint: index === 0 ? 'التجوية تفتّت، والتعرية تنقل، والترسيب يراكم الفتات.' : 'الناري من الصهارة، والرسوبي من الرواسب، والمتحوّل بالحرارة والضغط.',
    reward: `ختم المتحف ${index + 1}`, icon: 'globe',
    sourceRefs: [{ videoId: 'local-preview', chapterId: `sample-${index}`, startTime: 0, endTime: 60 }], choices,
    tasks: labels.map((label, correctChoiceIndex) => ({label, icon: 'globe', correctChoiceIndex, explanation: `${label}: الإجابة هي ${choices[correctChoiceIndex]}.`})),
  })),
};
window.addEventListener('message', (event) => {
  if (event.origin !== location.origin || event.source !== original.contentWindow || event.data?.source !== 'massar-mim-game') return;
  if (event.data.type === 'ready') {
    original.contentWindow.postMessage({source: 'massar-platform', type: 'bootstrap', payload: {content, mode: 'preview', progressKey: 'massar:mim-game:local-additive-preview:v1'}}, location.origin);
    switchWing.textContent = '✦ ادخل المغامرات الإضافية';
    switchWing.disabled = false;
  } else if (event.data.type === 'error') {
    const error = document.getElementById('previewError');
    error.textContent = event.data.message;
    error.hidden = false;
  } else if (event.data.type === 'close') {
    original.contentWindow.location.reload();
  }
});
switchWing.addEventListener('click', () => {
  if (expansion) {
    expansion.remove();
    expansion = null;
    original.hidden = false;
    if (resumeOriginal) original.contentDocument.getElementById('resume')?.click();
    switchWing.textContent = '✦ ادخل المغامرات الإضافية';
    previewNote.textContent = 'معاينة محلية · أسئلة تجريبية';
    original.contentWindow.focus();
    return;
  }
  const pauseLayer = original.contentDocument.getElementById('pauseLayer');
  resumeOriginal = Boolean(pauseLayer?.hidden);
  if (resumeOriginal) original.contentDocument.getElementById('pause')?.click();
  original.hidden = true;
  expansion = document.createElement('iframe');
  expansion.title = 'جناح المغامرات الإضافية';
  expansion.src = 'preview-expansion.html';
  document.body.prepend(expansion);
  switchWing.textContent = '↩ ارجع للعبة الأصلية';
  previewNote.textContent = 'جناح إضافي · تقدّم الأصل محفوظ';
});
original.src = 'index.html';
