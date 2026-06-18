import { test } from 'node:test';
import assert from 'node:assert/strict';
import { TemporaryAudioStorage } from './temporaryAudioStorage.js';
import type { AIConfig } from './aiConfig.js';

const config: AIConfig = {
  primaryProvider: 'vertex', project: 'p', location: 'global', temporaryBucket: 'bucket',
  temporaryPrefix: 'ai-analysis', textModel: 'text', imageModel: 'image',
};

function fakeStorage(lifecycle = true, metadataFailure = false) {
  const calls: { upload: unknown[]; delete: unknown[] } = { upload: [], delete: [] };
  const bucket = {
    name: 'bucket',
    getMetadata: async () => [{ lifecycle: { rule: lifecycle ? [{ action: { type: 'Delete' }, condition: { age: 1 } }] : [] } }],
    upload: async (...args: unknown[]) => {
      calls.upload.push(args);
      return [{ getMetadata: async () => {
        if (metadataFailure) throw { status: 503, sensitiveUrl: 'gs://bucket/private' };
        return [{ generation: '7' }];
      } }];
    },
    file: (name: string, options?: unknown) => ({
      delete: async (deleteOptions: unknown) => { calls.delete.push([name, options, deleteOptions]); },
    }),
  };
  return { storage: { bucket: () => bucket } as any, calls };
}

test('temporary audio storage accepts a one-day delete lifecycle', async () => {
  await new TemporaryAudioStorage(config, fakeStorage(true).storage).validateAccess();
});

test('temporary audio storage rejects a missing delete lifecycle', async () => {
  await assert.rejects(new TemporaryAudioStorage(config, fakeStorage(false).storage).validateAccess(), /24-hour delete lifecycle/);
});

test('temporary audio storage creates opaque objects and generation-aware deletion', async () => {
  const fake = fakeStorage();
  const storage = new TemporaryAudioStorage(config, fake.storage);
  const uploaded = await storage.upload('/tmp/audio.mp3', 'student-and-lesson-id');
  assert.match(uploaded.uri, /^gs:\/\/bucket\/ai-analysis\/[a-f0-9]{16}\/[a-f0-9-]+\.mp3$/);
  assert.equal(uploaded.uri.includes('student-and-lesson-id'), false);
  await storage.delete(uploaded);
  assert.deepEqual(fake.calls.delete[0], [uploaded.objectName, { generation: '7' }, { ignoreNotFound: true }]);
});

test('temporary audio storage deletes the object when post-upload metadata retrieval fails', async () => {
  const fake = fakeStorage(true, true);
  const storage = new TemporaryAudioStorage(config, fake.storage);
  await assert.rejects(storage.upload('/tmp/audio.mp3', 'job'), (error: unknown) =>
    error instanceof Error && error.message.includes('provider:503') && !error.message.includes('private'));
  assert.equal(fake.calls.delete.length, 1);
});
