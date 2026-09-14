import { expect, test } from '@playwright/test';

import {
  parseSafeReturnUrl,
  resolveReturnNavigation,
} from '../../src/lib/safe-return-url';


test.describe('safe authentication return navigation', () => {
  test('accepts a normalized deep link only inside the active surface', () => {
    expect(
      parseSafeReturnUrl(
        '/student/packages/abc?tab=lessons#current',
        'student'
      )
    ).toBe('/student/packages/abc?tab=lessons#current');
    expect(parseSafeReturnUrl('/admin/students', 'student')).toBeNull();
    expect(parseSafeReturnUrl('/student-impersonation', 'student')).toBeNull();
  });

  test('rejects open redirects, encoded separators, controls, and auth loops', () => {
    for (const candidate of [
      '//evil.example/path',
      'https://evil.example/path',
      '/%2f%2fevil.example',
      '/student\\evil',
      '/student/%5cevil',
      '/login?returnUrl=/login',
      '/student\nnext',
    ]) {
      expect(parseSafeReturnUrl(candidate, 'student')).toBeNull();
    }
  });

  test('marks same-origin returns for client routing and external defaults for document navigation', () => {
    expect(
      resolveReturnNavigation({
        returnUrl: '/student/packages',
        defaultDestination: 'https://app.massar-academy.net/student',
        surface: 'student',
        currentOrigin: 'https://app.massar-academy.net',
      })
    ).toEqual({
      href: '/student/packages',
      sameOrigin: true,
      source: 'return-url',
    });

    expect(
      resolveReturnNavigation({
        returnUrl: '//evil.example',
        defaultDestination: 'https://app.massar-academy.net/student',
        surface: 'landing',
        currentOrigin: 'https://massar-academy.net',
      })
    ).toEqual({
      href: 'https://app.massar-academy.net/student',
      sameOrigin: false,
      source: 'default',
    });
  });
});
