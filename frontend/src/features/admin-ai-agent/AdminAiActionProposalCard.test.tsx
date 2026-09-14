import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import test from 'node:test';

const proposalCard = readFileSync(
  resolve(
    process.cwd(),
    'src/features/admin-ai-agent/AdminAiActionProposalCard.tsx'
  ),
  'utf8'
);
const strongConfirmation = readFileSync(
  resolve(
    process.cwd(),
    'src/features/admin-ai-agent/AdminAiStrongConfirmation.tsx'
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

test('proposal card exposes typed review fields and never renders raw JSON', () => {
  assert.match(proposalCard, /proposal\.changes\.map/);
  assert.match(proposalCard, /currentValue/);
  assert.match(proposalCard, /requestedValue/);
  assert.match(proposalCard, /confirmationType === 'Explicit'/);
  assert.match(proposalCard, /confirmationType === 'TypedStrong'/);
  assert.match(proposalCard, /proposal\.status/);
  assert.match(proposalCard, /proposal\.execution/);
  assert.doesNotMatch(
    `${proposalCard}\n${executionResult}`,
    /JSON\.stringify|<pre|dangerouslySetInnerHTML/
  );
});

test('strong confirmation is focused, exact and expires locally', () => {
  assert.match(strongConfirmation, /inputRef\.current\?\.focus/);
  assert.match(strongConfirmation, /typed === phrase/);
  assert.match(strongConfirmation, /remainingSeconds > 0/);
  assert.match(strongConfirmation, /انتهت صلاحية عبارة التأكيد/);
  assert.match(strongConfirmation, /disabled=\{!matches \|\| busy\}/);
});
