import { createHash } from 'node:crypto';

export const AI_OUTPUT_LANGUAGES = ['auto', 'ar', 'en'] as const;

export type AiOutputLanguage = (typeof AI_OUTPUT_LANGUAGES)[number];

export interface GenerationRunContext {
  artifactRunId: string;
  callbackRunId?: string;
}

const GENERATION_RUN_ID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const ARTIFACT_RUN_ID_PATTERN = /^(?:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}|legacy-[0-9a-f]{32})$/i;

export function parseAiOutputLanguage(rawLanguage: unknown): AiOutputLanguage {
  if (rawLanguage === undefined || rawLanguage === null || rawLanguage === '') return 'auto';
  if (typeof rawLanguage !== 'string' || !AI_OUTPUT_LANGUAGES.includes(rawLanguage as AiOutputLanguage)) {
    throw new Error('AI generation job has an invalid outputLanguage.');
  }
  return rawLanguage as AiOutputLanguage;
}

export function parseGenerationRunId(rawRunId: unknown): string {
  if (typeof rawRunId !== 'string' || !GENERATION_RUN_ID_PATTERN.test(rawRunId)) {
    throw new Error('AI generation job has an invalid generationRunId.');
  }
  return rawRunId.toLowerCase();
}

export function parseArtifactRunId(rawRunId: unknown): string {
  if (typeof rawRunId !== 'string' || !ARTIFACT_RUN_ID_PATTERN.test(rawRunId)) {
    throw new Error('AI generation has an invalid artifact run id.');
  }
  return rawRunId.toLowerCase();
}

// Roll out workers first: legacy backend jobs omit the fence, while a legacy worker
// cannot preserve a run fence emitted by a newer backend.
export function resolveGenerationRun(
  rawRunId: unknown,
  physicalJobId: string | number | undefined,
  jobTimestamp: number | undefined,
): GenerationRunContext {
  if (rawRunId !== undefined && rawRunId !== null && rawRunId !== '') {
    const callbackRunId = parseGenerationRunId(rawRunId);
    return { artifactRunId: callbackRunId, callbackRunId };
  }
  if (physicalJobId === undefined || typeof jobTimestamp !== 'number' || !Number.isFinite(jobTimestamp)) {
    throw new Error('Legacy AI generation job is missing its id or timestamp.');
  }
  const digest = createHash('sha256')
    .update(`${String(physicalJobId)}\0${jobTimestamp}`)
    .digest('hex')
    .slice(0, 32);
  return { artifactRunId: `legacy-${digest}` };
}
