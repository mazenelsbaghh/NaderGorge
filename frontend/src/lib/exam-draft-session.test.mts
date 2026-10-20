import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import test from 'node:test';
import { ExamDraftSession, type DraftView, type DraftCache } from './exam-draft-session.ts';

function storage() {
  let server = { version: 0, answers: {} as Record<string, string>, isClosed: false, lastMutationId: '' };
  const connection = {
    mutationId: randomUUID,
    read: async () => structuredClone(server),
    write: async (request: { expectedVersion: number; answers: Record<string, string>; mutationId: string }) => {
      if (request.mutationId === server.lastMutationId) return structuredClone(server);
      if (request.expectedVersion !== server.version)
        throw { response: { status: 409, data: { data: structuredClone(server) } } };
      server = { ...server, version: server.version + 1, answers: structuredClone(request.answers), lastMutationId: request.mutationId };
      return structuredClone(server);
    },
  };
  return { connection, server: () => server };
}

function client(connection: ReturnType<typeof storage>['connection']) {
  let view: DraftView;
  let cache: DraftCache;
  const session = new ExamDraftSession(connection, { changed: next => { view = next; }, cache: next => { cache = next; } });
  return { session, view: () => view!, cache: () => cache! };
}

test('a new device restores acknowledged answers and an explicit deletion remains deleted', async () => {
  const db = storage();
  const first = client(db.connection);
  await first.session.load(null);
  first.session.edit('essay', 'إجابة طويلة '.repeat(700));
  first.session.edit('choice', 'option-a');
  await first.session.flush();
  const second = client(db.connection);
  await second.session.load(null);
  assert.equal(second.view().answers.essay, 'إجابة طويلة '.repeat(700));
  second.session.edit('choice', '');
  await second.session.flush();
  const reopened = client(db.connection);
  await reopened.session.load(second.cache());
  assert.equal(reopened.view().answers.choice, undefined);
  assert.equal(reopened.view().answers.essay, first.view().answers.essay);
});

test('a lost acknowledgement retries the same mutation then saves edits made while offline', async () => {
  const db = storage();
  let loseReply = true;
  const connection = { ...db.connection, write: async (request: Parameters<typeof db.connection.write>[0]) => {
    const accepted = await db.connection.write(request);
    if (loseReply) { loseReply = false; throw new Error('lost response'); }
    return accepted;
  } };
  const page = client(connection);
  await page.session.load(null);
  page.session.edit('essay', 'first answer');
  await assert.rejects(page.session.flush());
  assert.equal(page.view().status, 'offline');
  page.session.edit('essay', 'latest answer');
  await page.session.flush();
  assert.equal(db.server().version, 2);
  assert.deepEqual(db.server().answers, { essay: 'latest answer' });
  assert.equal(page.view().status, 'saved');
});

test('an older tab cannot silently replace a newer server answer', async () => {
  const db = storage();
  const first = client(db.connection), second = client(db.connection);
  await Promise.all([first.session.load(null), second.session.load(null)]);
  first.session.edit('question', 'newer answer');
  await first.session.flush();
  second.session.edit('question', 'older tab answer');
  await assert.rejects(second.session.flush());
  assert.equal(second.view().status, 'conflict');
  assert.equal(db.server().answers.question, 'newer answer');
  const reopened = client(db.connection);
  await reopened.session.load(second.cache());
  assert.equal(reopened.view().status, 'conflict');
  reopened.session.keepPageAnswers();
  await reopened.session.flush();
  assert.equal(db.server().answers.question, 'older tab answer');
});

test('edits during an in-flight save are included before flush resolves', async () => {
  const db = storage();
  let release!: () => void;
  const pause = new Promise<void>(resolve => { release = resolve; });
  let first = true;
  const page = client({ ...db.connection, write: async request => {
    if (first) { first = false; await pause; }
    return db.connection.write(request);
  } });
  await page.session.load(null);
  page.session.edit('question', 'first');
  const flushing = page.session.flush();
  page.session.edit('question', 'last keystroke');
  const overlapping = page.session.flush();
  release();
  assert.deepEqual(await Promise.all([flushing, overlapping]), [2, 2]);
  assert.deepEqual(db.server().answers, { question: 'last keystroke' });
});

test('an unavailable initial server read never declares restoration complete', async () => {
  const db = storage();
  let unavailable = true;
  const page = client({ ...db.connection, read: async () => {
    if (unavailable) throw new Error('offline');
    return db.connection.read();
  } });
  const legacy = { answers: { question: 'local answer' }, confirmedAnswers: {}, version: 0 };
  await assert.rejects(page.session.load(legacy));
  assert.equal(page.view().ready, false);
  await assert.rejects(page.session.flush());
  assert.equal(db.server().version, 0);
  unavailable = false;
  await page.session.load(legacy);
  await page.session.flush();
  assert.deepEqual(db.server().answers, legacy.answers);
});

test('refresh after a stale submit restores newer answers without discarding unsaved edits', async () => {
  const db = storage();
  const first = client(db.connection), second = client(db.connection);
  await Promise.all([first.session.load(null), second.session.load(null)]);
  first.session.edit('question', 'server answer');
  await first.session.flush();
  await second.session.refresh();
  assert.equal(second.view().answers.question, 'server answer');
  assert.equal(await second.session.flush(), 1);
  second.session.edit('question', 'unsaved answer');
  first.session.edit('question', 'new server answer');
  await first.session.flush();
  await second.session.refresh();
  assert.equal(second.view().status, 'conflict');
  assert.equal(second.view().answers.question, 'unsaved answer');
  assert.equal(db.server().answers.question, 'new server answer');
});
