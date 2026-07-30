import { test } from 'node:test';
import assert from 'node:assert/strict';
import { classifyExternalFailure, fetchWithTimeout, redactExternalText } from './workerFetch.js';

test('redactExternalText hides URLs and token query values', () => {
  const redacted = redactExternalText('visit https://example.test/path?token=secret');
  assert.equal(redacted.includes('https://example.test'), false);
  assert.equal(redacted.includes('secret'), false);
});

test('classifyExternalFailure marks timeout retryable', () => {
  const failure = classifyExternalFailure(new Error('operation timeout'));
  assert.equal(failure.category, 'timeout');
  assert.equal(failure.retryable, true);
});

test('fetchWithTimeout rejects a hung fetch with timeout category', async () => {
  const oldFetch = globalThis.fetch;
  try {
    globalThis.fetch = async (_url: RequestInfo | URL, init?: RequestInit) => new Promise<Response>((_resolve, reject) => {
      init?.signal?.addEventListener('abort', () => reject(new DOMException('aborted', 'AbortError')));
    });
    await assert.rejects(
      fetchWithTimeout('https://example.test?token=secret', { timeoutMs: 5 }),
      (error: any) => error.category === 'timeout',
    );
  } finally {
    globalThis.fetch = oldFetch;
  }
});
