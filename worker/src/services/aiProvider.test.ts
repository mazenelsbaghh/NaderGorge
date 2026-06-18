import { test } from 'node:test';
import assert from 'node:assert/strict';
import { AIProviderExecutionError, AIProviderGateway } from './aiProvider.js';
import type { AIConfig } from './aiConfig.js';

function config(overrides: Partial<AIConfig> = {}): AIConfig {
  return {
    primaryProvider: 'vertex',
    project: 'p',
    location: 'global',
    temporaryBucket: 'b',
    temporaryPrefix: 'ai-analysis',
    textModel: 'text',
    imageModel: 'image',
    fallbackApiKey: 'fallback',
    ...overrides,
  };
}

test('provider gateway uses primary and skips fallback on success', async () => {
  let fallbackCalls = 0;
  const result = await new AIProviderGateway(config()).execute({
    operation: 'essay',
    vertex: async () => 'vertex',
    developer: async () => { fallbackCalls++; return 'developer'; },
  });
  assert.equal(result, 'vertex');
  assert.equal(fallbackCalls, 0);
});

test('provider gateway falls back exactly once for structured quota exhaustion', async () => {
  let fallbackCalls = 0;
  const result = await new AIProviderGateway(config()).execute({
    operation: 'chapters',
    vertex: async () => { throw { status: 429 }; },
    developer: async () => { fallbackCalls++; return 'developer'; },
  });
  assert.equal(result, 'developer');
  assert.equal(fallbackCalls, 1);
});

test('provider gateway never falls back for non-quota failures', async () => {
  let fallbackCalls = 0;
  await assert.rejects(new AIProviderGateway(config()).execute({
    operation: 'mindmap',
    vertex: async () => { throw { status: 403 }; },
    developer: async () => { fallbackCalls++; return 'developer'; },
  }));
  assert.equal(fallbackCalls, 0);
});

test('provider gateway reports unavailable fallback without exposing the primary error', async () => {
  await assert.rejects(new AIProviderGateway(config({ fallbackApiKey: undefined })).execute({
    operation: 'essay',
    vertex: async () => { throw { status: 429, secret: 'primary-secret' }; },
    developer: async () => 'unused',
  }), (error: unknown) => error instanceof AIProviderExecutionError && !error.message.includes('primary-secret'));
});

test('provider gateway reports failed fallback without exposing the fallback error', async () => {
  await assert.rejects(new AIProviderGateway(config()).execute({
    operation: 'essay',
    vertex: async () => { throw { status: 429 }; },
    developer: async () => { throw { status: 401, secret: 'fallback-secret' }; },
  }), (error: unknown) => error instanceof AIProviderExecutionError && error.fallbackCategory === 'authentication' && !error.message.includes('fallback-secret'));
});
