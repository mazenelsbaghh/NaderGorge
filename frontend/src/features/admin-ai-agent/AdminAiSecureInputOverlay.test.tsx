import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import test from 'node:test';

const secureOverlay = readFileSync(
  resolve(
    process.cwd(),
    'src/features/admin-ai-agent/AdminAiSecureInputOverlay.tsx'
  ),
  'utf8'
);
const proposalCard = readFileSync(
  resolve(
    process.cwd(),
    'src/features/admin-ai-agent/AdminAiActionProposalCard.tsx'
  ),
  'utf8'
);
const executionResult = readFileSync(
  resolve(
    process.cwd(),
    'src/features/admin-ai-agent/AdminAiExecutionResult.tsx'
  ),
  'utf8'
);

test('secure overlay isolates values and provides modal focus and cleanup behavior', () => {
  assert.match(secureOverlay, /role="dialog"/);
  assert.match(secureOverlay, /aria-modal="true"/);
  assert.match(secureOverlay, /secureInputRef\.current\?\.focus/);
  assert.match(secureOverlay, /event\.key === 'Escape'/);
  assert.match(secureOverlay, /event\.key !== 'Tab'/);
  assert.match(secureOverlay, /container\.querySelectorAll/);
  assert.match(secureOverlay, /document\.addEventListener\('keydown'/);
  assert.match(secureOverlay, /document\.removeEventListener\('keydown'/);
  assert.match(secureOverlay, /trigger\?\.isConnected/);
  assert.match(secureOverlay, /trigger\.focus\(\)/);
  assert.match(secureOverlay, /aria-describedby="secure-description"/);
  assert.match(secureOverlay, /setSecureValue\(''\)/);
  assert.doesNotMatch(
    secureOverlay,
    /localStorage|sessionStorage|console\.|dangerouslySetInnerHTML/
  );
});

test('bulk, partial and recovery states remain structured instead of raw JSON', () => {
  assert.match(proposalCard, /candidateCount/);
  assert.match(proposalCard, /representativeItems/);
  assert.match(proposalCard, /Atomic/);
  assert.match(executionResult, /RecoveryRequired/);
  assert.match(executionResult, /execution\.items\.map/);
  assert.doesNotMatch(
    `${proposalCard}\n${executionResult}`,
    /JSON\.stringify|<pre/
  );
});
