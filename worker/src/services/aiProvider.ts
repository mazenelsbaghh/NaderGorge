import { classifyAIError } from './aiErrors.js';

const RETRY_DELAYS_MS = [2_000, 5_000, 10_000];
let waitBeforeRetry = (delayMs: number) => new Promise<void>((resolve) => setTimeout(resolve, delayMs));

type GeminiRequest<T> = (abortSignal: AbortSignal) => Promise<T>;

class GeminiRequestDeadlineError extends Error {
  constructor() {
    super('Gemini request exceeded its deadline.');
    this.name = 'GeminiRequestDeadlineError';
  }
}

export class GeminiDeveloperApiError extends Error {
  constructor(
    public readonly category: string,
    public readonly providerErrorName?: string,
    public readonly providerStatus?: number,
  ) {
    const diagnostic = [providerErrorName, providerStatus === undefined ? undefined : `HTTP ${providerStatus}`]
      .filter((value): value is string => Boolean(value))
      .join(', ');
    super(`Gemini Developer API operation failed (${category}${diagnostic ? `: ${diagnostic}` : ''}).`);
    this.name = 'GeminiDeveloperApiError';
  }
}

export function setGeminiRetryWaitForTests(wait?: (delayMs: number) => Promise<void>) {
  waitBeforeRetry = wait ?? ((delayMs) => new Promise<void>((resolve) => setTimeout(resolve, delayMs)));
}

function providerErrorName(error: unknown) {
  if (!error || typeof error !== 'object') return undefined;
  const name = (error as { name?: unknown }).name;
  return typeof name === 'string' && name.trim() ? name.trim() : undefined;
}

function providerStatus(error: unknown) {
  if (!error || typeof error !== 'object') return undefined;
  const status = (error as { status?: unknown }).status;
  return typeof status === 'number' ? status : undefined;
}

function requestDeadlineMs() {
  const configuredDeadline = Number.parseInt(
    process.env.GEMINI_REQUEST_TIMEOUT_MS || process.env.AI_PROVIDER_TIMEOUT_MS || '600000',
    10,
  );
  return Number.isFinite(configuredDeadline) && configuredDeadline > 0 ? configuredDeadline : 600_000;
}

async function requestBeforeDeadline<T>(request: GeminiRequest<T>): Promise<T> {
  const abortController = new AbortController();
  let deadlineTimer: NodeJS.Timeout | undefined;
  const deadline = new Promise<never>((_resolve, reject) => {
    deadlineTimer = setTimeout(() => {
      reject(new GeminiRequestDeadlineError());
      abortController.abort();
    }, requestDeadlineMs());
  });

  try {
    return await Promise.race([request(abortController.signal), deadline]);
  } finally {
    if (deadlineTimer) clearTimeout(deadlineTimer);
  }
}

function geminiFailure(error: unknown) {
  if (error instanceof GeminiRequestDeadlineError) {
    return new GeminiDeveloperApiError('provider-timeout', error.name);
  }
  const failure = classifyAIError(error);
  return new GeminiDeveloperApiError(failure.category, providerErrorName(error), failure.status ?? providerStatus(error));
}

export async function executeGeminiRequest<T>(request: GeminiRequest<T>): Promise<T> {
  try {
    return await requestBeforeDeadline(request);
  } catch (error) {
    throw geminiFailure(error);
  }
}

export async function executeRetriableGeminiRequest<T>(request: GeminiRequest<T>): Promise<T> {
  for (let attempt = 0; ; attempt += 1) {
    try {
      return await requestBeforeDeadline(request);
    } catch (error) {
      if (error instanceof GeminiRequestDeadlineError) {
        const retryDelay = RETRY_DELAYS_MS[attempt];
        if (retryDelay !== undefined) {
          console.warn('[AI provider] Gemini request deadline exceeded; retrying request.', { attempt: attempt + 1 });
          await waitBeforeRetry(retryDelay);
          continue;
        }
        throw geminiFailure(error);
      }
      const failure = classifyAIError(error);
      const status = failure.status ?? providerStatus(error);
      const retryDelay = RETRY_DELAYS_MS[attempt];
      if (retryDelay !== undefined && (status === 429 || status === 500 || status === 502 || status === 503 || status === 504)) {
        console.warn('[AI provider] Transient Gemini failure; retrying request.', { status, attempt: attempt + 1 });
        await waitBeforeRetry(retryDelay);
        continue;
      }
      throw geminiFailure(error);
    }
  }
}
