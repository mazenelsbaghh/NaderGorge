import { classifyAIError } from './aiErrors.js';

const RETRY_DELAYS_MS = [2_000, 5_000, 10_000];
let waitBeforeRetry = (delayMs: number) => new Promise<void>((resolve) => setTimeout(resolve, delayMs));

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

export async function executeGeminiRequest<T>(request: () => Promise<T>): Promise<T> {
  try {
    return await request();
  } catch (error) {
    const failure = classifyAIError(error);
    throw new GeminiDeveloperApiError(failure.category, providerErrorName(error), failure.status ?? providerStatus(error));
  }
}

export async function executeRetriableGeminiRequest<T>(request: () => Promise<T>): Promise<T> {
  for (let attempt = 0; ; attempt += 1) {
    try {
      return await request();
    } catch (error) {
      const failure = classifyAIError(error);
      const status = failure.status ?? providerStatus(error);
      const retryDelay = RETRY_DELAYS_MS[attempt];
      if (retryDelay !== undefined && (status === 429 || status === 500 || status === 502 || status === 503 || status === 504)) {
        console.warn('[AI provider] Transient Gemini failure; retrying request.', { status, attempt: attempt + 1 });
        await waitBeforeRetry(retryDelay);
        continue;
      }
      throw new GeminiDeveloperApiError(failure.category, providerErrorName(error), status);
    }
  }
}
