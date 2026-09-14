import assert from 'node:assert/strict';
import test from 'node:test';
import { normalizeAiOutputLanguage } from './ai-output-language.ts';

test('legacy or unknown package language falls back without changing valid choices', () => {
  const cases = [
    { candidate: undefined, expected: 'Auto' },
    { candidate: 'unsupported', expected: 'Auto' },
    { candidate: 'Auto', expected: 'Auto' },
    { candidate: 'Arabic', expected: 'Arabic' },
    { candidate: 'English', expected: 'English' },
  ] as const;

  for (const scenario of cases) {
    assert.equal(normalizeAiOutputLanguage(scenario.candidate), scenario.expected);
  }
});
