import assert from 'node:assert/strict';
import test from 'node:test';

import { validateVideoEmbedNavigation } from './video-embed-request-guard.ts';

function requestHeaders(values: Record<string, string>) {
  const normalized = new Map(
    Object.entries(values).map(([key, value]) => [key.toLowerCase(), value]),
  );
  return {
    get(name: string) {
      return normalized.get(name.toLowerCase()) ?? null;
    },
  } as Pick<Headers, 'get'>;
}

test('video embed accepts a same-origin iframe navigation', () => {
  const result = validateVideoEmbedNavigation(
    'https://app.massar-academy.net/api/video/embed?s=session-id',
    requestHeaders({
      referer: 'https://app.massar-academy.net/student/packages/1/lessons/2',
      'sec-fetch-dest': 'iframe',
      'sec-fetch-site': 'same-origin',
    }),
  );

  assert.equal(result, null);
});

test('2026-09-02 Safari same-site metadata accepts the exact application origin', () => {
  assert.equal(
    validateVideoEmbedNavigation(
      'https://app.massar-academy.net/api/video/embed?s=session-id',
      requestHeaders({
        referer: 'https://app.massar-academy.net/student/packages/1/lessons/2',
        'sec-fetch-dest': 'iframe',
        'sec-fetch-site': 'same-site',
      }),
    ),
    null,
  );
});

test('same-site metadata cannot authorize a sibling Massar surface', () => {
  assert.equal(
    validateVideoEmbedNavigation(
      'https://app.massar-academy.net/api/video/embed?s=session-id',
      requestHeaders({
        referer: 'https://admin.massar-academy.net/lessons/2',
        'sec-fetch-dest': 'iframe',
        'sec-fetch-site': 'same-site',
      }),
    ),
    'unauthorized-origin',
  );
});

test('video embed rejects a copied top-level URL even with a same-origin referrer', () => {
  assert.equal(
    validateVideoEmbedNavigation(
      'https://app.massar-academy.net/api/video/embed?s=x',
      requestHeaders({
        referer: 'https://app.massar-academy.net/student/lesson',
        'sec-fetch-dest': 'document',
        'sec-fetch-site': 'same-origin',
      }),
    ),
    'missing-context',
  );
});

test('video embed rejects missing referrer or fetch-site metadata', () => {
  const incompleteHeaders = [
    requestHeaders({ 'sec-fetch-dest': 'iframe', 'sec-fetch-site': 'same-origin' }),
    requestHeaders({
      referer: 'https://app.massar-academy.net/student/lesson',
      'sec-fetch-dest': 'iframe',
    }),
  ];

  for (const headers of incompleteHeaders) {
    assert.equal(
      validateVideoEmbedNavigation('https://app.massar-academy.net/api/video/embed?s=x', headers),
      'missing-context',
    );
  }
});

test('video embed uses exact hosts and rejects lookalike or malformed referrers', () => {
  for (const referer of [
    'https://app.massar-academy.net.evil.example/lesson',
    'https://evil.example/?next=app.massar-academy.net',
    'http://app.massar-academy.net/student/lesson',
    'not-a-url',
  ]) {
    assert.equal(
      validateVideoEmbedNavigation(
        'https://app.massar-academy.net/api/video/embed?s=x',
        requestHeaders({
          referer,
          'sec-fetch-dest': 'iframe',
          'sec-fetch-site': 'same-origin',
        }),
      ),
      'unauthorized-origin',
    );
  }
});

test('video embed rejects cross-site fetch metadata even with a forged same-host referrer', () => {
  assert.equal(
    validateVideoEmbedNavigation(
      'https://app.massar-academy.net/api/video/embed?s=x',
      requestHeaders({
        referer: 'https://app.massar-academy.net/student/lesson',
        'sec-fetch-dest': 'iframe',
        'sec-fetch-site': 'cross-site',
      }),
    ),
    'unauthorized-origin',
  );
});
