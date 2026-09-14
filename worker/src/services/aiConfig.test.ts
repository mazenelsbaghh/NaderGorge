import { afterEach, test } from 'node:test';
import assert from 'node:assert/strict';
import { readAIConfig } from './aiConfig.js';

const originalEnv = { ...process.env };
afterEach(() => { process.env = { ...originalEnv }; });

test('AI configuration requires a Gemini Developer API key', () => {
  delete process.env.GEMINI_API_KEY;
  assert.throws(() => readAIConfig(), /GEMINI_API_KEY is required/);
});

test('AI configuration always uses the Gemini Developer API defaults', () => {
  process.env.GEMINI_API_KEY = 'key';
  const config = readAIConfig();
  assert.equal(config.primaryProvider, 'developer');
  assert.equal(config.textModel, 'gemini-3.6-flash');
  assert.equal(config.imageModel, 'gemini-3-pro-image');
});
