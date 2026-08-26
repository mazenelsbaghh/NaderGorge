import assert from 'node:assert/strict';
import { decideLiveSupportSequence, parseLiveSupportEnvelope } from './live-support-client-contract.ts';
import { removeConversationDraft, updateConversationDraft } from './conversation-drafts.ts';
import { acquireMutationLock, releaseMutationLock } from './conversation-mutation-lock.ts';
import {
  advanceLiveSupportThreadHistory,
  createLiveSupportThreadPagination,
  mergeOrderedLiveSupportMessages,
  reconcileLiveSupportThreadHead,
} from './live-support-message-pages.ts';
import type { LiveSupportMessage } from '../services/live-support-service.ts';

assert.deepEqual(parseLiveSupportEnvelope('{"eventId":"e1","type":"MessageAdded","payload":{}}')?.eventId, 'e1');
assert.equal(parseLiveSupportEnvelope('{"type":"MessageAdded"}'), undefined, 'missing event id must force reconciliation');
assert.equal(parseLiveSupportEnvelope('{"eventId":"e1","type":"MessageAdded","sequence":0}'), undefined, 'invalid sequence must be rejected');
assert.equal(parseLiveSupportEnvelope('{malformed'), undefined, 'malformed JSON must be rejected');
assert.equal(decideLiveSupportSequence(4, 4), 'duplicate');
assert.equal(decideLiveSupportSequence(4, 3), 'duplicate');
assert.equal(decideLiveSupportSequence(4, 6), 'reconcile');
assert.equal(decideLiveSupportSequence(4, 5), 'accept');

// Regression 2026-08-26: a head refresh must preserve older WhatsApp pages.
const historicalMessage: LiveSupportMessage = {
  id: '00000000-0000-0000-0000-000000000001',
  conversationId: '00000000-0000-0000-0000-000000000010',
  senderType: 'Guest',
  clientMessageId: 'historical',
  type: 'Text',
  content: 'رسالة قديمة',
  sentAt: '2026-08-26T12:00:00.000Z',
};
const refreshedMessage: LiveSupportMessage = {
  ...historicalMessage,
  id: '00000000-0000-0000-0000-000000000002',
  conversationId: '00000000-0000-0000-0000-000000000020',
  clientMessageId: 'current',
  content: 'الحالة القديمة',
};
const mergedMessages = mergeOrderedLiveSupportMessages(
  [historicalMessage, refreshedMessage],
  [{ ...refreshedMessage, content: 'الحالة المحدثة', deliveredAt: '2026-08-26T12:01:00.000Z' }],
);
assert.deepEqual(mergedMessages.map((message) => message.id), [historicalMessage.id, refreshedMessage.id]);
assert.equal(mergedMessages[1]?.content, 'الحالة المحدثة');
assert.equal(mergedMessages[1]?.deliveredAt, '2026-08-26T12:01:00.000Z');

// Regression 2026-08-26: a refreshed 50-message head can become disjoint
// from the loaded head. Walk each new frontier back to its stable predecessor
// before resuming the employee's original older-history cursor.
const initialThreadHead = {
  items: [{ id: 'old-head-1' }, { id: 'old-head-2' }],
  nextCursor: 'historical-frontier',
};
let threadPagination = reconcileLiveSupportThreadHead(
  createLiveSupportThreadPagination(),
  initialThreadHead,
);
threadPagination = reconcileLiveSupportThreadHead(threadPagination, {
  items: [{ id: 'new-head-a-1' }, { id: 'new-head-a-2' }],
  nextCursor: 'bridge-a',
});
threadPagination = reconcileLiveSupportThreadHead(threadPagination, {
  items: [{ id: 'new-head-b-1' }, { id: 'new-head-b-2' }],
  nextCursor: 'bridge-b',
});
assert.equal(threadPagination.cursor, 'bridge-b');
assert.equal(threadPagination.resumePoints.length, 2);

const repeatedBridge = advanceLiveSupportThreadHistory(
  threadPagination,
  'bridge-b',
  {
    items: [{ id: 'new-head-b-1' }],
    nextCursor: 'bridge-b',
  },
);
assert.equal(repeatedBridge.historyGapUnresolved, true);
assert.equal(repeatedBridge.pagination.cursor, 'bridge-b');
assert.equal(repeatedBridge.pagination.resumePoints.length, 2);

const bridgeToHeadA = advanceLiveSupportThreadHistory(
  threadPagination,
  'bridge-b',
  {
    items: [{ id: 'new-head-a-2' }, { id: 'between-heads' }],
    nextCursor: 'unused-after-overlap',
  },
);
assert.equal(bridgeToHeadA.historyGapUnresolved, false);
assert.equal(bridgeToHeadA.pagination.cursor, 'bridge-a');
assert.equal(bridgeToHeadA.pagination.resumePoints.length, 1);

const localSendDoesNotCloseBridge = advanceLiveSupportThreadHistory(
  bridgeToHeadA.pagination,
  'bridge-a',
  {
    items: [{ id: 'locally-appended-message' }],
    nextCursor: 'bridge-a-continued',
  },
);
assert.equal(localSendDoesNotCloseBridge.pagination.cursor, 'bridge-a-continued');
assert.equal(localSendDoesNotCloseBridge.pagination.resumePoints.length, 1);

const bridgeToOriginalHead = advanceLiveSupportThreadHistory(
  localSendDoesNotCloseBridge.pagination,
  'bridge-a-continued',
  {
    items: [{ id: 'old-head-2' }, { id: 'middle-message' }],
    nextCursor: 'unused-original-overlap',
  },
);
assert.equal(bridgeToOriginalHead.pagination.cursor, 'historical-frontier');
assert.equal(bridgeToOriginalHead.pagination.resumePoints.length, 0);

const historicalPage = advanceLiveSupportThreadHistory(
  bridgeToOriginalHead.pagination,
  'historical-frontier',
  { items: [{ id: 'older-episode' }], nextCursor: null },
);
assert.equal(historicalPage.pagination.cursor, undefined);
assert.equal(historicalPage.historyGapUnresolved, false);

const exhaustedBridge = advanceLiveSupportThreadHistory(
  threadPagination,
  'bridge-b',
  { items: [{ id: 'unconnected-message' }], nextCursor: null },
);
assert.equal(exhaustedBridge.historyGapUnresolved, true);
assert.equal(exhaustedBridge.pagination.cursor, 'bridge-b');
assert.equal(exhaustedBridge.pagination.resumePoints.length, 2);

const staleBridgePage = advanceLiveSupportThreadHistory(
  threadPagination,
  'bridge-a',
  { items: [{ id: 'old-head-1' }], nextCursor: null },
);
assert.equal(staleBridgePage.stale, true);
assert.equal(staleBridgePage.pagination, threadPagination);

const draftA = updateConversationDraft({}, 'conversation-a', 'مسودة أ');
const draftAB = updateConversationDraft(draftA, 'conversation-b', 'مسودة ب');
assert.equal(draftAB['conversation-a'], 'مسودة أ');
assert.equal(draftAB['conversation-b'], 'مسودة ب');
assert.equal(removeConversationDraft(draftAB, 'conversation-a')['conversation-b'], 'مسودة ب');

const mutationLock = { current: false };
assert.equal(acquireMutationLock(mutationLock), true);
assert.equal(acquireMutationLock(mutationLock), false, 'duplicate close/transfer must not issue a second mutation');
releaseMutationLock(mutationLock);
assert.equal(acquireMutationLock(mutationLock), true, 'a failed/cancelled mutation must release the lock');

console.log('Live-support client contracts passed: malformed, missing-id, duplicate, out-of-order, and gap reconciliation.');
