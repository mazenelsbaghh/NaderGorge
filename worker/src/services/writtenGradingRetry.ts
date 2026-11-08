import { UnrecoverableError } from 'bullmq';
import { GeminiDeveloperApiError } from './aiProvider.js';
import { OpenAIWrittenGradingError } from './openAIWrittenGrading.js';

const PERMANENT_PROVIDER_FAILURES = new Set([
  'balance-exhausted', 'authentication', 'permission', 'validation', 'not-found',
]);

export async function withWrittenGradingRetryPolicy<T>(grade: () => Promise<T>): Promise<T> {
  try { return await grade(); }
  catch (error) {
    if ((error instanceof GeminiDeveloperApiError && PERMANENT_PROVIDER_FAILURES.has(error.category))
      || (error instanceof OpenAIWrittenGradingError && !error.retryable)) {
      // Backend recovery can resume later; five immediate attempts cannot restore billing or access.
      const terminal = new UnrecoverableError(error.message);
      terminal.cause = error;
      throw terminal;
    }
    throw error;
  }
}
