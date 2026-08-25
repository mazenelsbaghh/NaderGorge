import { Job, UnrecoverableError } from 'bullmq';
import { isJobCancellationMarked, throwIfCancellationRequested } from '../cancellation.js';

export async function throwIfGenerationCancellationRequested(
  job: Job,
  logicalAliases: Array<string | undefined>,
) {
  await throwIfCancellationRequested(job);
  const physicalJobId = job.id === undefined ? undefined : String(job.id);
  const aliases = new Set(logicalAliases.filter((alias): alias is string => Boolean(alias)));
  if (physicalJobId) aliases.delete(physicalJobId);

  for (const alias of aliases) {
    if (await isJobCancellationMarked(alias)) {
      throw new UnrecoverableError('تم إلغاء مهمة الذكاء الاصطناعي بواسطة المستخدم.');
    }
  }
}
