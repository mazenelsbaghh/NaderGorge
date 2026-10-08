export interface MimShot {
  start: number; end: number; title: string; action: string; camera: string; dialogue: string; sound: string;
}
export interface MimScene {
  title: string; educationalPoint: string; sourceChapterIds: string[]; shots: MimShot[]; prompt: string;
}
export interface MimDocument {
  schemaVersion: number; title: string; premise: string; style: string; continuity: string; scenes: MimScene[]; sourceText?: string | null; targetSceneCount?: number; episodeContext?: string | null;
}
export interface MimSource {
  id: string; title: string; sourceRevision: number;
  chapters: { id: string; title: string; summary: string; startTime: number; endTime: number }[];
}
export interface MimSnapshot {
  version: string; sourceVideoId: string | null; sourceRevision: number; stale: boolean; document: MimDocument; updatedAt: string | null; generating?: boolean;
}
export interface McpConnection { connected: boolean; configured: boolean; endpoint: string }
export interface McpTool { name: string; description: string; inputSchema: Record<string, unknown> }

export const characterReferences = [
  { name: 'ميم', label: 'مرجع ١', path: '/mim-studio/meem-character-sheet.png', fileName: '01-meem-character-sheet.png' },
  { name: 'بابا نادر', label: 'مرجع ٢', path: '/mim-studio/papa-nader-character-sheet.png', fileName: '02-papa-nader-character-sheet.png' },
] as const;

export const maximumSceneCount = 20;
export const targetSceneCount = (document: MimDocument) => document.targetSceneCount ?? 4;
export const lessonOpeningDirection = 'كل حصة حلقة لها موقف واحد مستمر، مثل طبخة اتلخبطت أو مركب بتغرق أو محاولة إصلاح حاجة. اختر الموقف بما يناسب محتوى الحصة، والمعلومة تدخل من الحوار والحركة ومحاولات حل المشكلة. أول مشهد يبدأ بموقف مختلف عن الحصة السابقة إن كانت متاحة؛ كل مشهد بعده يكمل من نهاية السابق بنفس المكان والأدوات والحركة. المشهد الأخير يحل المشكلة. لا تعِد استخدام المصباح ودوامة التاريخ تلقائيًا، ولا تحوّل نادر لمدرس واقف يلقي الشرح.';
export const noVisibleWriting = 'ABSOLUTELY NO VISIBLE WRITING: no subtitles, captions, titles, names, numbers, dates, labels, signs, logos, watermarks or readable text on books, scrolls, maps, clothing or scenery. Use blank surfaces, non-letter pictograms and visual action. Communicate facts only through spoken dialogue and images.';

const arabic = new Intl.NumberFormat('ar-EG', { minimumIntegerDigits: 2, useGrouping: false });
export const shotTime = (seconds: number) => arabic.format(seconds);

export function sceneScript(scene: MimScene, index: number): string {
  return [`المشهد ${index + 1}: ${scene.title}`, `٣٠ ثانية · ${scene.shots.length} كادرات`,
    `الفكرة التعليمية: ${scene.educationalPoint}`, '',
    ...scene.shots.flatMap(shot => [
      `${shotTime(shot.start)}–${shotTime(shot.end)} | ${shot.title}`, shot.action,
      `الكاميرا: ${shot.camera}`, ...(shot.dialogue ? [shot.dialogue] : []),
      ...(shot.sound ? [`الصوت: ${shot.sound}`] : []), '',
    ]),
  ].join('\n');
}

export function episodeScript(document: MimDocument): string {
  return [document.title, document.premise, `الشكل: ${document.style}`, `ثبات التنفيذ: ${document.continuity}`,
    ...document.scenes.map(sceneScript)].join('\n\n');
}

export function generationPrompt(document: MimDocument, index: number): string {
  const scene = document.scenes[index];
  const identityDirection = scene.prompt.split(/\nSCENE \d+:|\n\nABSOLUTELY NO VISIBLE WRITING:/)[0];
  const timedShot = (shot: MimShot) => `${String(shot.start).padStart(2, '0')}-${String(shot.end).padStart(2, '0')}: ${shot.title}\nAction: ${shot.action}\nCamera: ${shot.camera}\nEXACT Egyptian Arabic dialogue (speaker labels are not spoken): ${shot.dialogue}\nSound: ${shot.sound}`;
  return [identityDirection, noVisibleWriting,
    ...characterReferences.map((reference, i) => `REFERENCE IMAGE ${i + 1}: attach the actual file ${reference.fileName} (${reference.name}) to this generation request.`),
    'Use both attached character sheets as visual identity references, never as frames or collages in the final video.',
    `Episode situation: ${document.episodeContext ?? document.premise}\nCreative direction: ${document.style}\nContinuity: ${document.continuity}`,
    `SCENE ${index + 1}: ${scene.title}\nScene ${index + 1} of ${targetSceneCount(document)}. Educational point: ${scene.educationalPoint}`,
    index ? `Begin precisely where the previous scene ended; no reset or new introduction. Preserve positions, movement, props, emotional state, lighting and the unfinished goal. Previous scene's final two shots:\n${document.scenes[index - 1].shots.slice(-2).map(timedShot).join('\n')}` : 'Open directly in this episode\'s situation. Establish the goal and immediate comic problem.',
    scene.shots.map(timedShot).join('\n'), noVisibleWriting,
    'End at exactly 30 seconds. Reserve the first and final half-second for silent visual action. Do not generate fades inside this clip; the editor applies them later. Keep total dialogue within the timings; lower the score under voices. No overlapping dialogue or added narrator.'].join('\n\n');
}

export function mcpBrief(document: MimDocument): string {
  return [
    'استخدم Higgsfield MCP الرسمي لإعداد فيديو ميم وبابا نادر من السيناريو التالي.',
    `قاعدة كتابة افتتاحيات الحصص:\n${lessonOpeningDirection}`,
    ...characterReferences.map(reference => `${reference.label}: ${reference.name} — الملف ${reference.fileName}`),
    'ارفع الصورتين الفعليتين من الحزمة وأرفقهما بكل طلب توليد، بما فيه اللقطات المقسمة؛ ذكر أسماء الملفات في النص وحده لا يكفي. ثبّت الشكل والملابس والصوت عبر كل اللقطات.',
    'الشيتان مرجع للشخصيات فقط؛ لا تعرض لوحة الزوايا والتعبيرات نفسها داخل الفيديو. إذا تعذر إرفاق المرجعين، وضّح العائق قبل التوليد.',
    'اكتشف أدوات التوليد والموديلات المتاحة ودعم أكثر من مرجع قبل اختيار الموديل.',
    `المتاح حاليًا ${document.scenes.length} من ${targetSceneCount(document)} مشاهد. نفّذ المشهد المحدد فقط بعد مراجعته. كل مشهد ٣٠ ثانية؛ إذا لم يدعم الموديل المدة، اقسمه عند حدود الكادرات بدون قطع الحوار.`,
    'بعد اكتمال فيديوهات الحلقة، اجمعها بالترتيب مع Fade خروج ودخول عند حدود المشاهد. حافظ على ٣٠ ثانية لكل مشهد وعلى الحوار، ولا تضف أي كتابة للفيديو.',
    'اعرض خطة اللقطات والتكلفة قبل بدء التوليد. لا تكرر طلب توليد حالته غير مؤكدة.',
    episodeScript(document),
    ...document.scenes.map((_, index) => `GENERATION PROMPT ${index + 1}\n${generationPrompt(document, index)}`),
  ].join('\n\n');
}

export function preparedSourceMatches(document: MimDocument, source: MimSource): boolean {
  const chapters = new Set(source.chapters.map(chapter => chapter.id));
  return document.scenes.every(scene => scene.sourceChapterIds.every(id => chapters.has(id)));
}

export interface MimVideoModel { id: string; name: string }
export interface MimVideo { error?: string | null; reviewAvailableAt?: string | null; model: string; version: string; state: string; quote: string; expiresAt: string; jobId: string | null; urls: string[] }
export interface MimEpisodeVideo { state: string; progress: number; sceneCount: number; duration: number; error?: string | null }
