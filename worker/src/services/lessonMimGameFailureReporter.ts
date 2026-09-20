import type { Job } from 'bullmq';
import { isTerminalJobFailure } from '../utils/jobTempFiles.js';
import { lessonMimGameCallbacks, type LessonMimGameCallbackClient } from './lessonMimGameCallbackClient.js';
import { WorkerExternalError } from './workerFetch.js';

function errorCode(error: Error) {
  if (error.name === 'UnrecoverableError' || /MIM_MODEL_INVALID/.test(error.message)) return 'MIM_MODEL_INVALID';
  if ((error instanceof WorkerExternalError && error.category === 'timeout') || /timeout|aborted/i.test(error.message)) return 'MIM_MODEL_TIMEOUT';
  return 'MIM_GENERATION_FAILED';
}

export async function reportTerminalLessonMimGameFailure(
  job: Pick<Job, 'attemptsMade' | 'data' | 'opts'> | undefined,
  error: Error,
  callbacks: LessonMimGameCallbackClient = lessonMimGameCallbacks,
) {
  if (!job || !isTerminalJobFailure(job, error)) return false;
  const data = job.data as Record<string, unknown>;
  if (typeof data.gameId !== 'string' || typeof data.runId !== 'string') return false;
  await callbacks.fail({ gameId: data.gameId, runId: data.runId, errorCode: errorCode(error) });
  return true;
}
