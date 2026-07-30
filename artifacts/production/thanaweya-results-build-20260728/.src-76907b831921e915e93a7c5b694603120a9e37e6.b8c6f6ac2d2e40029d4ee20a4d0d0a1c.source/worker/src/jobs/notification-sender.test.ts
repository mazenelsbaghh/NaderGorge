import { test } from 'node:test';
import assert from 'node:assert';
import { Pool } from 'pg';

// Preserve original methods
const originalQuery = Pool.prototype.query;
const originalFetch = globalThis.fetch;

// Setup mock state
let poolQueries: any[] = [];
let multicastSentPayloads: any[] = [];
let fetchCalls: { url: string; body: any }[] = [];
let mockDeviceTokens: string[] = ['token-123', 'token-456'];

// Mock Pool.prototype.query
Pool.prototype.query = (async (text: any, params?: any[]) => {
  const sql = typeof text === 'string' ? text : text.text;
  poolQueries.push({ text: sql, params });

  if (sql.includes('ParentDeviceTokens')) {
    return {
      rows: mockDeviceTokens.map(t => ({ DeviceToken: t }))
    };
  }
  if (sql.includes('users')) {
    return {
      rows: [
        { FullName: 'Mocked Student', PhoneNumber: '01012345678' }
      ]
    };
  }
  return { rows: [] };
}) as any;

// Mock fetch for Evolution API
globalThis.fetch = (async (url: any, init?: any) => {
  fetchCalls.push({
    url: String(url),
    body: init?.body ? JSON.parse(init.body) : null
  });
  return {
    ok: true,
    status: 200,
    text: async () => 'OK'
  } as any;
}) as any;

import { processParentPushNotification, processNotificationJob, firebaseMessaging } from './notification-sender.js';

// Setup Mock for firebaseMessaging
firebaseMessaging.sendEachForMulticast = async (payload: any) => {
  multicastSentPayloads.push(payload);
  return {
    successCount: payload.tokens.length,
    failureCount: 0,
    responses: payload.tokens.map(() => ({
      success: true,
      messageId: 'mock-msg-id'
    }))
  };
};

test('processParentPushNotification fetches tokens and sends multicast messages', async () => {
  poolQueries = [];
  multicastSentPayloads = [];

  const studentId = 'test-student-guid';
  const title = 'اختبار الكيمياء العضوية الشامل';
  const body = 'تم حل اختبار الكيمياء العضوية الشامل بنجاح';
  const category = 'Exam';

  const res = await processParentPushNotification(studentId, title, body, category);

  assert.strictEqual(res.success, true);
  assert.strictEqual(res.tokensCount, 2);
  assert.strictEqual(res.successCount, 2);
  assert.strictEqual(res.failureCount, 0);

  // Check SQL Query
  assert.ok(poolQueries.length > 0);
  assert.ok(poolQueries[0]?.text.includes('ParentDeviceTokens'));
  assert.deepStrictEqual(poolQueries[0]?.params, [studentId]);

  // Check Multicast Message Payload
  assert.strictEqual(multicastSentPayloads.length, 1);
  assert.deepStrictEqual(multicastSentPayloads[0]?.tokens, ['token-123', 'token-456']);
  assert.strictEqual(multicastSentPayloads[0]?.notification?.title, title);
  assert.strictEqual(multicastSentPayloads[0]?.notification?.body, body);
  assert.strictEqual(multicastSentPayloads[0]?.data?.studentId, studentId);
  assert.strictEqual(multicastSentPayloads[0]?.data?.category, category);
});

test('processParentPushNotification handles case when no tokens are found', async () => {
  const savedTokens = mockDeviceTokens;
  mockDeviceTokens = [];
  poolQueries = [];
  multicastSentPayloads = [];

  const res = await processParentPushNotification('student-no-tokens', 'Title', 'Body', 'Warning');

  assert.strictEqual(res.success, true);
  assert.strictEqual(res.reason, 'no_tokens');
  assert.strictEqual(res.tokensCount, 0);
  assert.strictEqual(multicastSentPayloads.length, 0);

  mockDeviceTokens = savedTokens;
});

test('processNotificationJob handles parent-push job', async () => {
  poolQueries = [];
  multicastSentPayloads = [];

  const job: any = {
    id: 'job-parent-push-1',
    name: 'parent-push',
    data: {
      studentId: 'student-id-push',
      title: 'واجب جديد',
      body: 'تم تقديم الواجب بنجاح',
      category: 'Homework'
    }
  };

  const res = (await processNotificationJob(job)) as any;

  assert.strictEqual(res.type, 'ParentPush');
  assert.strictEqual(res.tokensCount, 2);
  assert.strictEqual(multicastSentPayloads.length, 1);
  assert.strictEqual(multicastSentPayloads[0]?.notification?.title, 'واجب جديد');
});

test('processNotificationJob handles send-warning job by sending WhatsApp to student and Push to parent', async () => {
  process.env.EVOLUTION_API_BASE_URL = 'http://evolution-api';
  process.env.EVOLUTION_API_KEY = 'mock-key';

  poolQueries = [];
  multicastSentPayloads = [];
  fetchCalls = [];

  const job: any = {
    id: 'job-warning-1',
    name: 'send-warning',
    data: {
      StudentId: 'student-id-warning',
      Severity: 'Critical',
      WarningId: 'warning-id-123',
      Message: 'غياب متكرر عن الحضور',
      Category: 'Warning'
    }
  };

  const res = (await processNotificationJob(job)) as any;

  assert.strictEqual(res.success, true);
  assert.strictEqual(res.smsSent, true);
  assert.strictEqual(res.parentPushSent, true);

  // Verify WhatsApp API fetch call
  assert.strictEqual(fetchCalls.length, 1);
  assert.ok(fetchCalls[0]?.url.includes('message/sendText'));
  assert.ok(fetchCalls[0]?.body?.textMessage?.text.includes('غياب متكرر عن الحضور'));

  // Verify Firebase parent push multicast call
  assert.strictEqual(multicastSentPayloads.length, 1);
  assert.strictEqual(multicastSentPayloads[0]?.notification?.title, 'تنبيه أكاديمي جديد');
  assert.ok(multicastSentPayloads[0]?.notification?.body.includes('غياب متكرر عن الحضور'));
});
