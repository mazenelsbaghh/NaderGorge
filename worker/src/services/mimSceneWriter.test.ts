import assert from 'node:assert/strict';
import test from 'node:test';
import { parseWritingContext, parseWrittenScene, type WritingContext, type WritingDocument } from './mimSceneWriter.js';

const source: WritingContext = { lessonTitle: 'دورة الماء', source: { id: null, title: 'نص الشرح', revision: 0,
  chapters: [], text: 'يسخن الماء فيتبخر ثم يبرد بخار الماء ويتكثف لتتكون السحب ثم يسقط المطر ويعود الماء إلى الأنهار والبحار. تتكرر هذه المراحل في دورة الماء الطبيعية.' }, previousScenes: null, previousLessonOpening: null };
const draft = (): WritingDocument => ({ schemaVersion: 1, title: 'ميم ودورة الماء', premise: 'يتتبع ميم قطرة الماء', style: 'كرتون', continuity: 'نفس الشخصيات', scenes: [{
  title: 'رحلة القطرة', educationalPoint: 'التبخر', sourceChapterIds: [], prompt: 'Use both character references.',
  shots: Array.from({ length: 6 }, (_, i) => ({ start: i * 5, end: (i + 1) * 5, title: 'قطرة', action: 'تتحول القطرة إلى بخار', camera: 'لقطة قريبة', dialogue: '', sound: '' })),
}] });
test('a lesson without videos accepts written explanation and exactly one timed scene', () => {
  const context = parseWritingContext(source);
  assert.equal(parseWrittenScene(draft(), context).scenes.length, 1);
});
for (const [name, change] of [
  ['batch output', (d: WritingDocument) => { d.scenes.push(structuredClone(d.scenes[0]!)); }],
  ['invented chapter', (d: WritingDocument) => { d.scenes[0]!.sourceChapterIds = ['not-in-source']; }],
  ['timing gap', (d: WritingDocument) => { d.scenes[0]!.shots[1]!.start = 6; }],
  ['short ending', (d: WritingDocument) => { d.scenes[0]!.shots[5]!.end = 29; }],
] as const) test(`writer rejects ${name} without accepting a partial result`, () => {
  const response = draft(); change(response);
  assert.throws(() => parseWrittenScene(response, source));
});
test('title-only request cannot become a fabricated lesson scene', () => {
  assert.throws(() => parseWritingContext({ ...source, source: { ...source.source, text: '' } }));
});

test('model output passes through the real scene generator with no batch acceptance', async () => {
  const { generateMimStudioScene, setAIServiceRuntimeFactoryForTests } = await import('./geminiService.js');
  const response = draft();
  try {
    setAIServiceRuntimeFactoryForTests(() => ({ config: { textModel: 'test-model' } as never, developer: { models: { generateContent: async () => ({ text: JSON.stringify(response) }) }, files: {} } as never }));
    assert.equal((await generateMimStudioScene(source)).scenes.length, 1);
    response.scenes.push(structuredClone(response.scenes[0]!));
    await assert.rejects(generateMimStudioScene(source), /INVALID_MIM_SCENE/);
  } finally { setAIServiceRuntimeFactoryForTests(); }
});
