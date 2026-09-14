import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { atomicWriteFileSync, resolveWithin } from './storage.js';

test('atomicWriteFileSync publishes complete content without temporary files', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'massar-worker-storage-'));
  try {
    const destination = atomicWriteFileSync(root, 'subtitles/example.srt', 'complete');

    assert.equal(fs.readFileSync(destination, 'utf8'), 'complete');
    assert.deepEqual(
      fs.readdirSync(path.dirname(destination)).filter((name) => name.endsWith('.tmp')),
      [],
    );
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test('resolveWithin rejects traversal and absolute paths', () => {
  const root = path.join(os.tmpdir(), 'massar-worker-storage-root');
  assert.throws(() => resolveWithin(root, '../secret'), /escapes/);
  assert.throws(() => resolveWithin(root, '/absolute'), /relative/);
});
