import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import vm from 'node:vm';
import ts from 'typescript';

function loadModule(path, dependencies = {}) {
  const sandboxModule = { exports: {} };
  const source = readFileSync(new URL(path, import.meta.url), 'utf8');
  const compiled = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true },
  }).outputText;
  vm.runInNewContext(compiled, {
    module: sandboxModule,
    exports: sandboxModule.exports,
    require(name) {
      if (!(name in dependencies)) throw new Error(`Unexpected dependency: ${name}`);
      return dependencies[name];
    },
  });
  return sandboxModule.exports;
}

const helper = loadModule('./sales-coupon-payload.ts');
const { prepareSalesCouponPayload } = helper;
const teacherId = '2a0e7d2f-1dd7-4af0-9974-c999489899b2';

test('a teacher coupon from the creation form sends the selected teacher as its academic target', () => {
  const payload = { targetType: 'Teacher', teacherId, targetId: null };
  assert.equal(prepareSalesCouponPayload(payload).targetId, teacherId);
  assert.equal(payload.targetId, null);
});

test('changing the teacher replaces a stale target rather than validating a different teacher', () => {
  const result = prepareSalesCouponPayload({ targetType: 'Teacher', teacherId, targetId: 'previous-teacher' });
  assert.equal(result.targetId, teacherId);
});

test('teacher-owned content keeps its specific content target', () => {
  for (const targetType of ['Package', 'Term', 'ContentSection', 'Lesson', 'SpecificVideo', 'VideoType', 'PublicExam']) {
    const payload = { targetType, ownerType: 'Teacher', teacherId, targetId: 'content-id' };
    assert.equal(prepareSalesCouponPayload(payload), payload);
    assert.equal(payload.targetId, 'content-id');
  }
});

test('an explicit platform target is preserved', () => {
  const payload = { targetType: 'Platform', teacherId, targetId: null };
  assert.equal(prepareSalesCouponPayload(payload), payload);
});

test('a teacher target without a selected teacher fails before making a request', () => {
  for (const teacherId of [null, undefined, '', '  ', 123]) {
    assert.throws(() => prepareSalesCouponPayload({ targetType: 'Teacher', teacherId }), /اختيار المدرس مطلوب/);
  }
});

test('discount, dates, usage limits, owner and academic scopes are preserved', () => {
  const payload = {
    targetType: 'Teacher', teacherId, targetId: '', code: 'Example with spaces', discountValue: 25,
    discountType: 'Percentage', ownerType: 'Teacher', startsAt: '2026-10-05T21:00:00Z',
    expiresAt: '2026-10-06T21:00:00Z', globalUsageLimit: 100, perStudentUsageLimit: 1,
    academicScopes: [{ scopeLevel: 'Exact' }],
  };
  const result = prepareSalesCouponPayload(payload);
  for (const key of Object.keys(payload).filter((key) => key !== 'targetId')) assert.equal(result[key], payload[key]);
});

function serviceWithClient(apiClient) {
  return loadModule('../services/admin-sales-service.ts', {
    './api-client': apiClient,
    '@/lib/sales-coupon-payload': helper,
  }).adminSalesService;
}

test('the actual creation service posts a complete teacher target', async () => {
  let posted;
  const service = serviceWithClient({ async post(url, payload) { posted = { url, payload }; return { data: { data: { id: 'new-coupon' } } }; } });
  const result = await service.createCoupon({ targetType: 'Teacher', teacherId, targetId: null, discountValue: 25 });
  assert.equal(posted.url, '/admin/sales/coupons');
  assert.equal(posted.payload.targetId, teacherId);
  assert.equal(posted.payload.discountValue, 25);
  assert.equal(result.id, 'new-coupon');
});

test('the actual update service applies the same teacher target rule', async () => {
  let sent;
  const service = serviceWithClient({ async put(url, payload) { sent = { url, payload }; return { data: { data: { id: 'coupon' } } }; } });
  await service.updateCoupon('coupon', { targetType: 'Teacher', teacherId, targetId: 'stale-id' });
  assert.equal(sent.url, '/admin/sales/coupons/coupon');
  assert.equal(sent.payload.targetId, teacherId);
});

test('the creation service does not send an incomplete teacher selection', async () => {
  let called = false;
  const service = serviceWithClient({ async post() { called = true; } });
  await assert.rejects(service.createCoupon({ targetType: 'Teacher', teacherId: null }), /اختيار المدرس مطلوب/);
  assert.equal(called, false);
});
