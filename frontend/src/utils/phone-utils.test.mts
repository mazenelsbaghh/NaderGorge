import { test } from 'node:test';
import assert from 'node:assert/strict';
import { normalizeEgyptianMobileInput } from './phone-utils.ts';

test('registration phone cleanup never turns invalid content into a different valid recipient', () => {
  for (const phone of ['010108340840', '0101083408', '010abc10834084', '+4401010834084', '01310834084']) {
    assert.equal(normalizeEgyptianMobileInput(phone), phone);
    assert.doesNotMatch(normalizeEgyptianMobileInput(phone), /^01[0125]\d{8}$/);
  }
});

test('Persian digits and explicit Egyptian country codes keep the same local number', () => {
  for (const phone of ['۰۱۰۱۰۸۳۴۰۸۴', '00201010834084', '201010834084', '\u200e010 1083-4084\u200f'])
    assert.equal(normalizeEgyptianMobileInput(phone), '01010834084');
});
