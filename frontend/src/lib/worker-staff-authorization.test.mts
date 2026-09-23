import assert from 'node:assert/strict';
import test from 'node:test';
import { staffSessionApiUrl, validateStaffAuthorization } from './worker-staff-authorization.ts';

test('2026-09-23 staff validation uses the public HTTPS API in production', () => {
  assert.equal(
    staffSessionApiUrl('production', 'https://api.massar-academy.net/api/', 'http://backend:5245/api'),
    'https://api.massar-academy.net/api',
  );
  assert.equal(staffSessionApiUrl('production', undefined, 'http://backend:5245/api'), undefined);
  assert.equal(staffSessionApiUrl('production', 'http://api.massar-academy.net/api', undefined), undefined);
  assert.equal(staffSessionApiUrl('development', undefined, 'http://backend:5245/api'), 'http://backend:5245/api');
});

test('staff session validation denies missing and insufficient authorization', async () => {
  let requests = 0;
  const fetchSession: typeof fetch = async () => {
    requests += 1;
    return Response.json({ data: { user: { roles: ['Student'], permissions: [] } } });
  };
  const apiUrl = 'https://api.massar-academy.net/api';

  assert.equal((await validateStaffAuthorization(null, apiUrl, fetchSession)).status, 401);
  assert.equal(requests, 0);
  assert.equal((await validateStaffAuthorization('Bearer token', apiUrl, fetchSession)).status, 403);
  assert.equal(requests, 1);
});

test('staff session validation forwards bearer over HTTPS and fails closed on an outage', async () => {
  let requestedUrl = '';
  let forwardedAuthorization = '';
  let requestOptions: RequestInit | undefined;
  const fetchSession: typeof fetch = async (input, init) => {
    requestedUrl = String(input);
    forwardedAuthorization = new Headers(init?.headers).get('authorization') ?? '';
    requestOptions = init;
    return Response.json({ data: { user: { roles: ['Teacher'] } } });
  };
  const apiUrl = staffSessionApiUrl('production', 'https://api.massar-academy.net/api', 'http://backend:5245/api');
  assert.deepEqual(await validateStaffAuthorization('Bearer token', apiUrl, fetchSession), { ok: true });
  assert.equal(requestedUrl, 'https://api.massar-academy.net/api/auth/session');
  assert.equal(forwardedAuthorization, 'Bearer token');
  assert.equal(requestOptions?.redirect, 'error');
  assert.equal(requestOptions?.cache, 'no-store');
  assert.ok(requestOptions?.signal);

  const previousError = console.error;
  console.error = () => {};
  try {
    const unavailable: typeof fetch = async () => { throw new Error('connection refused'); };
    assert.equal((await validateStaffAuthorization('Bearer token', apiUrl, unavailable)).status, 503);
  } finally {
    console.error = previousError;
  }
});
