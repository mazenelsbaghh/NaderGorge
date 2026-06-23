import type { AIConfig } from './aiConfig.js';
import { classifyAIError, isQuotaExhausted } from './aiErrors.js';

export type AIOperation = 'transcription' | 'chapters' | 'essay' | 'mindmap' | 'live-support';

export class AIProviderExecutionError extends Error {
  constructor(
    message: string,
    public readonly primaryCategory: string,
    public readonly fallbackCategory?: string,
  ) {
    super(message);
    this.name = 'AIProviderExecutionError';
  }
}

export class AIProviderGateway {
  constructor(private readonly config: AIConfig) {}

  async execute<T>(input: {
    operation: AIOperation;
    vertex: () => Promise<T>;
    developer: () => Promise<T>;
  }): Promise<T> {
    if (this.config.primaryProvider === 'developer') {
      try {
        return await input.developer();
      } catch (error) {
        const failure = classifyAIError(error);
        throw new AIProviderExecutionError(
          `Developer AI operation failed (${failure.category}).`,
          failure.category,
        );
      }
    }

    try {
      return await input.vertex();
    } catch (primaryError) {
      const primary = classifyAIError(primaryError);
      if (!isQuotaExhausted(primaryError)) {
        throw new AIProviderExecutionError(
          `Vertex AI operation failed (${primary.category}).`,
          primary.category,
        );
      }

      console.warn('[AI provider] Primary quota exhausted; attempting configured fallback.', {
        operation: input.operation,
        provider: 'vertex',
        status: primary.status,
        category: primary.category,
      });

      if (!this.config.fallbackApiKey) {
        throw new AIProviderExecutionError(
          'Vertex quota exhausted and Developer API fallback is not configured.',
          primary.category,
          'unavailable',
        );
      }

      try {
        return await input.developer();
      } catch (fallbackError) {
        const fallback = classifyAIError(fallbackError);
        throw new AIProviderExecutionError(
          `Vertex quota exhausted and Developer API fallback failed (${fallback.category}).`,
          primary.category,
          fallback.category,
        );
      }
    }
  }
}
