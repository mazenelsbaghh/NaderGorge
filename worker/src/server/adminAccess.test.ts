import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createWorkerAdminGuard, isWorkerAdminEnabled } from './adminAccess.js';

function mockResponse() {
  return {
    statusCode: 0,
    body: undefined as unknown,
    status(code: number) { this.statusCode = code; return this; },
    json(body: unknown) { this.body = body; return this; },
  };
}

test('worker admin is disabled by default in production', () => {
  const oldNodeEnv = process.env.NODE_ENV;
  const oldEnabled = process.env.WORKER_ADMIN_ENABLED;
  try {
    process.env.NODE_ENV = 'production';
    delete process.env.WORKER_ADMIN_ENABLED;
    assert.equal(isWorkerAdminEnabled(), false);
  } finally {
    process.env.NODE_ENV = oldNodeEnv;
    if (oldEnabled === undefined) delete process.env.WORKER_ADMIN_ENABLED; else process.env.WORKER_ADMIN_ENABLED = oldEnabled;
  }
});

test('worker admin guard denies disabled admin surface without token details', () => {
  const oldNodeEnv = process.env.NODE_ENV;
  const oldEnabled = process.env.WORKER_ADMIN_ENABLED;
  try {
    process.env.NODE_ENV = 'production';
    process.env.WORKER_ADMIN_ENABLED = 'false';
    const res = mockResponse();
    createWorkerAdminGuard()({ path: '/ui', method: 'GET', ip: '127.0.0.1', socket: {} } as any, res as any, () => assert.fail('next should not run'));
    assert.equal(res.statusCode, 404);
    assert.deepEqual(res.body, { error: 'Not found' });
  } finally {
    process.env.NODE_ENV = oldNodeEnv;
    if (oldEnabled === undefined) delete process.env.WORKER_ADMIN_ENABLED; else process.env.WORKER_ADMIN_ENABLED = oldEnabled;
  }
});

test('worker admin guard allows valid bearer token when explicitly enabled', () => {
  const oldEnabled = process.env.WORKER_ADMIN_ENABLED;
  const oldToken = process.env.WORKER_ADMIN_TOKEN;
  try {
    process.env.WORKER_ADMIN_ENABLED = 'true';
    process.env.WORKER_ADMIN_TOKEN = 'a'.repeat(40);
    let called = false;
    const res = mockResponse();
    createWorkerAdminGuard()({
      path: '/api/status/1',
      method: 'GET',
      ip: '127.0.0.2',
      socket: {},
      header: (name: string) => name.toLowerCase() === 'authorization' ? `Bearer ${'a'.repeat(40)}` : '',
    } as any, res as any, () => { called = true; });
    assert.equal(called, true);
  } finally {
    if (oldEnabled === undefined) delete process.env.WORKER_ADMIN_ENABLED; else process.env.WORKER_ADMIN_ENABLED = oldEnabled;
    if (oldToken === undefined) delete process.env.WORKER_ADMIN_TOKEN; else process.env.WORKER_ADMIN_TOKEN = oldToken;
  }
});

test('worker admin guard denies invalid bearer token when enabled', () => {
  const oldEnabled = process.env.WORKER_ADMIN_ENABLED;
  const oldToken = process.env.WORKER_ADMIN_TOKEN;
  try {
    process.env.WORKER_ADMIN_ENABLED = 'true';
    process.env.WORKER_ADMIN_TOKEN = 'a'.repeat(40);
    const res = mockResponse();
    createWorkerAdminGuard()({
      path: '/api/status/1',
      method: 'GET',
      ip: '127.0.0.3',
      socket: {},
      header: (name: string) => name.toLowerCase() === 'authorization' ? `Bearer ${'b'.repeat(40)}` : '',
    } as any, res as any, () => assert.fail('next should not run'));
    assert.equal(res.statusCode, 401);
  } finally {
    if (oldEnabled === undefined) delete process.env.WORKER_ADMIN_ENABLED; else process.env.WORKER_ADMIN_ENABLED = oldEnabled;
    if (oldToken === undefined) delete process.env.WORKER_ADMIN_TOKEN; else process.env.WORKER_ADMIN_TOKEN = oldToken;
  }
});

test('worker admin guard rate limits repeated denied requests by source', () => {
  const oldEnabled = process.env.WORKER_ADMIN_ENABLED;
  const oldToken = process.env.WORKER_ADMIN_TOKEN;
  const oldLimit = process.env.WORKER_ADMIN_RATE_LIMIT_PER_MINUTE;
  try {
    process.env.WORKER_ADMIN_ENABLED = 'true';
    process.env.WORKER_ADMIN_TOKEN = 'a'.repeat(40);
    process.env.WORKER_ADMIN_RATE_LIMIT_PER_MINUTE = '1';
    const guard = createWorkerAdminGuard();
    const first = mockResponse();
    const second = mockResponse();
    const req = { path: '/api/status/1', method: 'GET', ip: '127.0.0.44', socket: {}, header: () => '' } as any;

    guard(req, first as any, () => assert.fail('next should not run'));
    guard(req, second as any, () => assert.fail('next should not run'));

    assert.equal(first.statusCode, 401);
    assert.equal(second.statusCode, 429);
  } finally {
    if (oldEnabled === undefined) delete process.env.WORKER_ADMIN_ENABLED; else process.env.WORKER_ADMIN_ENABLED = oldEnabled;
    if (oldToken === undefined) delete process.env.WORKER_ADMIN_TOKEN; else process.env.WORKER_ADMIN_TOKEN = oldToken;
    if (oldLimit === undefined) delete process.env.WORKER_ADMIN_RATE_LIMIT_PER_MINUTE; else process.env.WORKER_ADMIN_RATE_LIMIT_PER_MINUTE = oldLimit;
  }
});
