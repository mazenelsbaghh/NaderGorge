import { classifyAIError } from './aiErrors.js';

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
