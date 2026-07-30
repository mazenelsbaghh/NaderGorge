import { test } from 'node:test';
import assert from 'node:assert';
import { isUnsafeSecret, requireStrongSecret } from './security.js';

test('isUnsafeSecret checks unsafe secrets correctly', () => {
  assert.strictEqual(isUnsafeSecret(undefined), true);
  assert.strictEqual(isUnsafeSecret(''), true);
  assert.strictEqual(isUnsafeSecret('short'), true);
  assert.strictEqual(isUnsafeSecret('12345678901234567890123'), true); // 23 chars (less than minLength 24)
  assert.strictEqual(isUnsafeSecret('123456789012345678901234'), false); // 24 chars
  assert.strictEqual(isUnsafeSecret('CHANGE_ME_IN_LOCAL_ENV', 10), true); // in UNSAFE_SECRETS
});

test('requireStrongSecret throws or returns value', () => {
  process.env.TEST_STRONG_SECRET = 'a'.repeat(32);
  assert.strictEqual(requireStrongSecret('TEST_STRONG_SECRET'), 'a'.repeat(32));

  process.env.TEST_WEAK_SECRET = 'short';
  assert.throws(() => {
    requireStrongSecret('TEST_WEAK_SECRET');
  }, /is missing, weak, or uses an unsafe placeholder/);
});
