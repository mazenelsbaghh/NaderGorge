import assert from 'node:assert/strict';
import { decideLiveSupportSequence, parseLiveSupportEnvelope } from './live-support-client-contract.ts';
import { removeConversationDraft, updateConversationDraft } from './conversation-drafts.ts';
import { acquireMutationLock, releaseMutationLock } from './conversation-mutation-lock.ts';

assert.deepEqual(parseLiveSupportEnvelope('{"eventId":"e1","type":"MessageAdded","payload":{}}')?.eventId, 'e1');
assert.equal(parseLiveSupportEnvelope('{"type":"MessageAdded"}'), undefined, 'missing event id must force reconciliation');
assert.equal(parseLiveSupportEnvelope('{"eventId":"e1","type":"MessageAdded","sequence":0}'), undefined, 'invalid sequence must be rejected');
assert.equal(parseLiveSupportEnvelope('{malformed'), undefined, 'malformed JSON must be rejected');
assert.equal(decideLiveSupportSequence(4, 4), 'duplicate');
assert.equal(decideLiveSupportSequence(4, 3), 'duplicate');
assert.equal(decideLiveSupportSequence(4, 6), 'reconcile');
assert.equal(decideLiveSupportSequence(4, 5), 'accept');

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
