import { classifyAIError } from './aiErrors.js';

export class GeminiDeveloperApiError extends Error {
  constructor(public readonly category: string) {
    super(`Gemini Developer API operation failed (${category}).`);
    this.name = 'GeminiDeveloperApiError';
  }
}

export async function executeGeminiRequest<T>(request: () => Promise<T>): Promise<T> {
  try {
    return await request();
  } catch (error) {
    throw new GeminiDeveloperApiError(classifyAIError(error).category);
  }
}
