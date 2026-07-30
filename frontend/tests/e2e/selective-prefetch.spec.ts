import { expect, test } from '@playwright/test';

import {
  isExpensivePrefetchRoute,
  shouldPrefetchDestination,
} from '../../src/components/navigation/IntentLink';
import { platformQueryClient } from '../../src/lib/query-client';
import { queryKeys } from '../../src/lib/query-keys';
import {
  invalidateForStaffDataChanged,
  invalidateStudentQueryKeys,
  resetRealtimeEventDedupe,
} from '../../src/lib/realtime-invalidation-map';
import { isRequestCancellation } from '../../src/services/api-client';


test.describe('selective intent prefetch', () => {
  test('allows an authorized same-origin high-frequency destination', () => {
    expect(
      shouldPrefetchDestination({
        href: '/student/packages',
        currentOrigin: 'https://app.massar-academy.net',
        canPrefetch: true,
        expensive: false,
        saveData: false,
        effectiveType: '4g',
      })
    ).toBe(true);
  });

  test('denies unauthorized, external, rare, data-saving, and constrained requests', () => {
    const base = {
      href: '/student/packages',
      currentOrigin: 'https://app.massar-academy.net',
      canPrefetch: true,
      expensive: false,
      saveData: false,
      effectiveType: '4g',
    };

    expect(shouldPrefetchDestination({ ...base, canPrefetch: false })).toBe(false);
    expect(shouldPrefetchDestination({ ...base, expensive: true })).toBe(false);
    expect(shouldPrefetchDestination({ ...base, saveData: true })).toBe(false);
    expect(
      shouldPrefetchDestination({ ...base, effectiveType: 'slow-2g' })
    ).toBe(false);
    expect(
      shouldPrefetchDestination({
        ...base,
        href: 'https://evil.example/student/packages',
      })
    ).toBe(false);
  });

  test('rejects fragment-only, protocol, and malformed destinations', () => {
    const base = {
      currentOrigin: 'https://app.massar-academy.net',
      canPrefetch: true,
      expensive: false,
      saveData: false,
      effectiveType: '4g',
    };

    expect(shouldPrefetchDestination({ ...base, href: '#details' })).toBe(false);
    expect(shouldPrefetchDestination({ ...base, href: 'mailto:a@example.com' })).toBe(false);
    expect(shouldPrefetchDestination({ ...base, href: '//evil.example' })).toBe(false);
  });

  test('classifies rare data-heavy routes for no intent prefetch', () => {
    expect(isExpensivePrefetchRoute('/admin/live-support')).toBe(true);
    expect(isExpensivePrefetchRoute('/teacher/reports/monthly')).toBe(true);
    expect(isExpensivePrefetchRoute('/student/packages')).toBe(false);
  });

  test('invalidates only student query prefixes mapped to a realtime event', () => {
    const packagesKey = queryKeys.student.packages('student-a');
    const teachersKey = queryKeys.student.teachers('student-a');
    const shellKey = queryKeys.student.shell('student-a');
    platformQueryClient.setQueryData(packagesKey, ['package']);
    platformQueryClient.setQueryData(teachersKey, ['teacher']);
    platformQueryClient.setQueryData(shellKey, { balance: 10 });

    invalidateStudentQueryKeys(['content:packages']);

    expect(platformQueryClient.isStale(packagesKey, 60_000)).toBe(true);
    expect(platformQueryClient.isStale(teachersKey, 60_000)).toBe(true);
    expect(platformQueryClient.isStale(shellKey, 60_000)).toBe(false);
    platformQueryClient.removeQueries(['student']);
  });

  test('recognizes transport cancellation without classifying ordinary failures', () => {
    expect(
      isRequestCancellation(new DOMException('superseded', 'AbortError'))
    ).toBe(true);
    expect(isRequestCancellation({ code: 'ERR_CANCELED' })).toBe(true);
    expect(isRequestCancellation(new Error('offline'))).toBe(false);
  });

  test('uses a stable event id to dedupe targeted realtime invalidation', () => {
    const packagesKey = queryKeys.student.packages('student-a');
    const shellKey = queryKeys.student.shell('student-a');
    platformQueryClient.setQueryData(packagesKey, ['package']);
    platformQueryClient.setQueryData(shellKey, { balance: 10 });
    resetRealtimeEventDedupe();
    const payload = {
      eventId: 'stable-content-event',
      scopes: ['content'],
    };

    expect(invalidateForStaffDataChanged(payload)).toBe(true);
    expect(platformQueryClient.isStale(packagesKey, 60_000)).toBe(true);
    expect(platformQueryClient.isStale(shellKey, 60_000)).toBe(false);
    platformQueryClient.setQueryData(packagesKey, ['fresh-package']);

    expect(invalidateForStaffDataChanged(payload)).toBe(false);
    expect(platformQueryClient.isStale(packagesKey, 60_000)).toBe(false);
    resetRealtimeEventDedupe();
    platformQueryClient.removeQueries(['student']);
  });
});
