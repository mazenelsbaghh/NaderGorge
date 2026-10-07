export interface MimShot {
  start: number; end: number; title: string; action: string; camera: string; dialogue: string; sound: string;
}
export interface MimScene {
  title: string; educationalPoint: string; sourceChapterIds: string[]; shots: MimShot[]; prompt: string;
}
export interface MimDocument {
  schemaVersion: number; title: string; premise: string; style: string; continuity: string; scenes: MimScene[];
}
export interface MimSource {
  id: string; title: string; sourceRevision: number;
  chapters: { id: string; title: string; summary: string; startTime: number; endTime: number }[];
}
export interface MimSnapshot {
  version: string; sourceVideoId: string; sourceRevision: number; stale: boolean; document: MimDocument; updatedAt: string | null;
}
export interface McpConnection { connected: boolean; configured: boolean; endpoint: string }
export interface McpTool { name: string; description: string; inputSchema: Record<string, unknown> }

export const characterReferences = [
  { name: 'ميم', label: 'مرجع ١', path: '/mim-studio/meem-character-sheet.png', fileName: '01-meem-character-sheet.png' },
  { name: 'بابا نادر', label: 'مرجع ٢', path: '/mim-studio/papa-nader-character-sheet.png', fileName: '02-papa-nader-character-sheet.png' },
] as const;

export const lessonOpeningDirection = 'عند كتابة اسكربت حصة جديدة، ابدأ الفيديو الأول فقط بافتتاحية جديدة نابعة من موضوع الحصة وتختلف في الموقف والمكان والفعل الكوميدي عن افتتاحية الحصة السابقة، وليس مجرد تغيير الحوار. راجع اسكربت الحصة السابقة إن كان متاحًا، ولا تدّعِ المقارنة إن لم يكن متاحًا. الفيديوهات الثاني والثالث والرابع تكمل قصة نفس الحصة وتحافظ على استمراريتها. لا تعِد استخدام دوامة التاريخ والمصباح تلقائيًا لكل حصة. هذه قاعدة لكتابة الحصص القادمة، ولا تغيّر الاسكربت المعتمد أدناه أثناء توليده.';

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
  const identityDirection = scene.prompt.split(/\nSCENE \d+:/)[0];
  return [identityDirection,
    ...characterReferences.map((reference, i) => `REFERENCE IMAGE ${i + 1}: attach the actual file ${reference.fileName} (${reference.name}) to this generation request.`),
    'Use both attached character sheets as visual identity references, never as frames or collages in the final video.',
    `Creative direction: ${document.style}`, `Continuity: ${document.continuity}`,
    'Use the following current storyboard and EXACT Egyptian Arabic dialogue. Speaker names are direction only; do not speak them.',
    sceneScript(scene, index), 'Complete this 30-second scene with motivated shot changes and clear lip sync.'].join('\n\n');
}

export function mcpBrief(document: MimDocument): string {
  return [
    'استخدم Higgsfield MCP الرسمي لإعداد فيديو ميم وبابا نادر من السيناريو التالي.',
    `قاعدة كتابة افتتاحيات الحصص:\n${lessonOpeningDirection}`,
    ...characterReferences.map(reference => `${reference.label}: ${reference.name} — الملف ${reference.fileName}`),
    'ارفع الصورتين الفعليتين من الحزمة وأرفقهما بكل طلب توليد، بما فيه اللقطات المقسمة؛ ذكر أسماء الملفات في النص وحده لا يكفي. ثبّت الشكل والملابس والصوت عبر كل اللقطات.',
    'الشيتان مرجع للشخصيات فقط؛ لا تعرض لوحة الزوايا والتعبيرات نفسها داخل الفيديو. إذا تعذر إرفاق المرجعين، وضّح العائق قبل التوليد.',
    'اكتشف أدوات التوليد والموديلات المتاحة ودعم أكثر من مرجع قبل اختيار الموديل.',
    'مدة الحلقة ١٢٠ ثانية: أربعة مشاهد، كل مشهد ٣٠ ثانية. إذا لم يدعم الموديل ٣٠ ثانية، اقسم المشهد عند حدود الكادرات إلى لقطات مدعومة بدون قطع الحوار.',
    'اعرض خطة اللقطات والتكلفة قبل بدء التوليد. لا تكرر طلب توليد حالته غير مؤكدة.',
    episodeScript(document),
    ...document.scenes.map((_, index) => `GENERATION PROMPT ${index + 1}\n${generationPrompt(document, index)}`),
  ].join('\n\n');
}

export function preparedSourceMatches(document: MimDocument, source: MimSource): boolean {
  const chapters = new Set(source.chapters.map(chapter => chapter.id));
  return document.scenes.every(scene => scene.sourceChapterIds.every(id => chapters.has(id)));
}
