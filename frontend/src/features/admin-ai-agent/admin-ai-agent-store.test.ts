import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { useAdminAiAgentStore } from './admin-ai-agent-store.ts';
import {
  isAdminAiTurnInProgress,
  type AdminAiTurnStatus,
} from '../../services/admin-ai-agent-contract.ts';

const controllerSource = readFileSync(
  new URL('./useAdminAiAgentController.ts', import.meta.url),
  'utf8'
);

const event = (sequence: number, eventId = crypto.randomUUID()) => ({
  schemaVersion: '1' as const,
  eventId,
  sequence,
  type: 'snapshot_changed' as const,
  conversationId: crypto.randomUUID(),
  occurredAt: new Date().toISOString(),
});

test('duplicate and gapped events converge through the store contract', () => {
  useAdminAiAgentStore.getState().clearSecurityBoundary();
  const first = event(1);
  assert.equal(useAdminAiAgentStore.getState().acceptEvent(first), 'accept');
  assert.equal(useAdminAiAgentStore.getState().acceptEvent(first), 'duplicate');
  assert.equal(
    useAdminAiAgentStore
      .getState()
      .acceptEvent({ ...first, eventId: crypto.randomUUID(), sequence: 3 }),
    'reconcile'
  );
});

test('security-boundary cleanup removes drafts, selection, sequences and intents', () => {
  const state = useAdminAiAgentStore.getState();
  state.selectConversation(crypto.randomUUID());
  state.setDraft('private admin prompt');
  state.beginIntent('send', crypto.randomUUID());
  state.acceptEvent(event(1));
  state.clearSecurityBoundary();
  const cleared = useAdminAiAgentStore.getState();
  assert.equal(cleared.draft, '');
  assert.equal(cleared.selectedConversationId, undefined);
  assert.deepEqual(cleared.inFlightIntents, {});
  assert.deepEqual(cleared.lastSequenceByConversation, {});
});

test('drafts stay bounded in memory without browser persistence', () => {
  useAdminAiAgentStore.getState().setDraft('س'.repeat(9000));
  assert.equal(useAdminAiAgentStore.getState().draft.length, 8000);
  assert.equal('persist' in useAdminAiAgentStore, false);
});

test('turn progress classification stops snapshot polling after terminal states', () => {
  const expected: Record<AdminAiTurnStatus, boolean> = {
    Queued: true,
    Planning: true,
    Retrieving: true,
    Answering: true,
    WaitingClarification: false,
    ProposalReady: false,
    Completed: false,
    CancelRequested: true,
    Cancelled: false,
    Failed: false,
    AccessRevoked: false,
  };
  for (const [status, inProgress] of Object.entries(expected))
    assert.equal(isAdminAiTurnInProgress(status as AdminAiTurnStatus), inProgress);
});

test('snapshot generation guards reject late responses across conversation boundaries', () => {
  assert.match(controllerSource, /generation !== snapshotGeneration\.current/);
  assert.match(
    controllerSource,
    /selectedConversationId !==\s*selectedConversationId/
  );
  assert.match(
    controllerSource,
    /controllers\.current\.forEach\(\(c\) => c\.abort\(\)\)/
  );
});
