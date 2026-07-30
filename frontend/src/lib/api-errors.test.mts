import assert from 'node:assert/strict';
import test from 'node:test';

import {
  getApiErrorSummary,
  getRegistrationApiErrors,
} from './api-errors.ts';

test('production registration date failure from 2026-07-29 points to the father date field in Arabic', () => {
  const errors = getRegistrationApiErrors({
    response: {
      data: {
        message: 'Validation failed',
        errors: ["Father's date of birth must be in the past"],
      },
    },
  });

  assert.deepEqual(errors, [
    {
      field: 'fatherDateOfBirth',
      message: 'تاريخ ميلاد الأب يجب أن يكون تاريخًا سابقًا لليوم.',
    },
  ]);
});

test('multiple validation details stay visible and translated', () => {
  const errors = getRegistrationApiErrors({
    response: {
      data: {
        message: 'Validation failed',
        errors: [
          "Father's date of birth must be in the past",
          "Mother's date of birth must be in the past",
        ],
      },
    },
  });

  assert.equal(errors.length, 2);
  assert.equal(errors[0]?.field, 'fatherDateOfBirth');
  assert.equal(errors[1]?.field, 'motherDateOfBirth');
});

test('generic API validation toast uses the detailed Arabic message', () => {
  const summary = getApiErrorSummary({
    response: {
      data: {
        message: 'Validation failed',
        errors: ['Study track is required for this grade level'],
      },
    },
  });

  assert.equal(summary, 'اختر الشعبة أو التخصص المناسب للصف الدراسي.');
});

test('unknown English server errors never leak into the Arabic interface', () => {
  const summary = getApiErrorSummary({
    response: {
      data: {
        message: 'Unexpected internal failure',
      },
    },
  });

  assert.equal(
    summary,
    'حدثت مشكلة أثناء تنفيذ الطلب. حاول مرة أخرى، وإذا استمرت المشكلة تواصل مع الدعم.',
  );
});
