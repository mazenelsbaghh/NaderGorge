import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import test from 'node:test';

const source = readFileSync(
  resolve(
    process.cwd(),
    'src/features/admin-ai-agent/AdminAiStrongConfirmation.tsx'
  ),
  'utf8'
);

test('strong confirmation remains exact, expiring, focused and locally non-authoritative', () => {
  assert.match(source, /typed === phrase/);
  assert.match(source, /remainingSeconds > 0/);
  assert.match(source, /inputRef\.current\?\.focus/);
  assert.match(source, /disabled=\{!matches \|\| busy\}/);
  assert.doesNotMatch(source, /localStorage|sessionStorage|fetch\(|apiClient/);
});
