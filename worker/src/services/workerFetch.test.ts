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

test('fetchWithTimeout rejects a chunked body whose cumulative size exceeds the limit', async () => {
  const oldFetch = globalThis.fetch;
  try {
    globalThis.fetch = async () => new Response(new ReadableStream({
      start(controller) {
        controller.enqueue(new Uint8Array(6));
        controller.enqueue(new Uint8Array(6));
        controller.close();
      },
    }));

    await assert.rejects(
      fetchWithTimeout('https://example.test', { timeoutMs: 100, maxResponseBytes: 10 }),
      (error: unknown) => error instanceof Error
        && error.name === 'WorkerExternalError'
        && (error as { category?: unknown }).category === 'response-too-large',
    );
  } finally {
    globalThis.fetch = oldFetch;
  }
});

test('fetchWithTimeout keeps the deadline active while a response body is stalled', async () => {
  const oldFetch = globalThis.fetch;
  try {
    globalThis.fetch = async (_url: RequestInfo | URL, init?: RequestInit) => {
      let bodyController!: ReadableStreamDefaultController<Uint8Array>;
      const body = new ReadableStream<Uint8Array>({
        start(controller) {
          bodyController = controller;
          controller.enqueue(new Uint8Array([1]));
        },
      });
      init?.signal?.addEventListener('abort', () => {
        bodyController.error(new DOMException('aborted', 'AbortError'));
      });
      return new Response(body);
    };

    await assert.rejects(
      fetchWithTimeout('https://example.test', { timeoutMs: 5, maxResponseBytes: 10 }),
      (error: unknown) => error instanceof Error
        && error.name === 'WorkerExternalError'
        && (error as { category?: unknown }).category === 'timeout',
    );
  } finally {
    globalThis.fetch = oldFetch;
  }
});
