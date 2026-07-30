import { afterEach, test } from 'node:test';
import assert from 'node:assert/strict';
import { readAIConfig } from './aiConfig.js';

const originalEnv = { ...process.env };
afterEach(() => { process.env = { ...originalEnv }; });

test('AI configuration defaults to Vertex and requires its settings', () => {
  delete process.env.AI_PRIMARY_PROVIDER;
  delete process.env.GOOGLE_CLOUD_PROJECT;
  assert.throws(() => readAIConfig(), /GOOGLE_CLOUD_PROJECT/);
});

test('AI configuration reads Vertex settings and model defaults', () => {
  process.env.AI_PRIMARY_PROVIDER = 'vertex';
  process.env.GOOGLE_CLOUD_PROJECT = 'project';
  process.env.GOOGLE_CLOUD_LOCATION = 'global';
  process.env.AI_TEMP_GCS_BUCKET = 'bucket';
  process.env.GEMINI_API_KEY = 'legacy-key';
  process.env.GEMINI_FALLBACK_API_KEY = 'separate-fallback-key';
  const config = readAIConfig();
  assert.equal(config.textModel, 'gemini-2.5-flash');
  assert.equal(config.imageModel, 'gemini-3-pro-image-preview');
  assert.equal(config.temporaryPrefix, 'ai-analysis');
  assert.equal(config.developerApiKey, 'separate-fallback-key');
});

test('AI configuration rejects an unknown primary provider', () => {
  process.env.AI_PRIMARY_PROVIDER = 'other';
  assert.throws(() => readAIConfig(), /must be vertex or developer/);
});

test('AI configuration rejects an unsafe object prefix', () => {
  process.env.AI_PRIMARY_PROVIDER = 'developer';
  process.env.GEMINI_API_KEY = 'key';
  process.env.AI_TEMP_GCS_PREFIX = '../private';
  assert.throws(() => readAIConfig(), /safe object prefix/);
});

test('developer primary requires a developer credential', () => {
  process.env.AI_PRIMARY_PROVIDER = 'developer';
  delete process.env.GEMINI_API_KEY;
  delete process.env.GEMINI_FALLBACK_API_KEY;
  assert.throws(() => readAIConfig(), /requires GEMINI_API_KEY/);
});
