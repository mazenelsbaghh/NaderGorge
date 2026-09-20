import { UnrecoverableError, type Job } from 'bullmq';
import { generateLessonMimGame } from '../services/geminiService.js';
import { lessonMimGameCallbacks, type LessonMimGameCallbackClient } from '../services/lessonMimGameCallbackClient.js';
import { parseMimGameContent, parseMimSourcePack, type MimGameContent, type MimSourcePack } from '../services/lessonMimGameContract.js';

interface Payload { gameId: string; lessonId: string; runId: string; sourceFingerprint: string; schemaVersion: number; sourcePack: unknown; generatedContentJson?: string }
interface Dependencies { generate(source: MimSourcePack): Promise<MimGameContent>; callbacks: LessonMimGameCallbackClient }
const uuid = (value: unknown): value is string => typeof value === 'string'
  && /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);

function payloadOf(value: unknown): { payload: Payload; source: MimSourcePack } {
  if (!value || typeof value !== 'object') throw new UnrecoverableError('MIM_MODEL_INVALID');
  const payload = value as Payload;
  if (!uuid(payload.gameId) || !uuid(payload.lessonId) || !uuid(payload.runId) || payload.schemaVersion !== 1 || !/^[0-9a-f]{64}$/.test(payload.sourceFingerprint))
    throw new UnrecoverableError('MIM_MODEL_INVALID');
  try { return { payload, source: parseMimSourcePack(payload.sourcePack) }; }
  catch { throw new UnrecoverableError('MIM_MODEL_INVALID'); }
}

export function createLessonMimGameProcessor(deps: Dependencies) {
  return async (job: Job) => {
    const { payload, source } = payloadOf(job.data);
    let contentJson = payload.generatedContentJson;
    if (contentJson) {
      try { contentJson = JSON.stringify(parseMimGameContent(contentJson, source)); }
      catch { throw new UnrecoverableError('MIM_MODEL_INVALID'); }
    } else {
      let content: MimGameContent;
      try { content = await deps.generate(source); }
      catch (error) {
        if (error instanceof Error && /^MIM_/.test(error.message)) throw new UnrecoverableError('MIM_MODEL_INVALID');
        throw error;
      }
      contentJson = JSON.stringify(content);
      try { parseMimGameContent(contentJson, source); }
      catch { throw new UnrecoverableError('MIM_MODEL_INVALID'); }
      await job.updateData({ ...job.data, generatedContentJson: contentJson });
    }
    await deps.callbacks.complete({ gameId: payload.gameId, runId: payload.runId, sourceFingerprint: payload.sourceFingerprint, contentJson });
    return { gameId: payload.gameId, runId: payload.runId };
  };
}

export default createLessonMimGameProcessor({ generate: generateLessonMimGame, callbacks: lessonMimGameCallbacks });
