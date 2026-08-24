import { test } from 'node:test';
import assert from 'node:assert/strict';
import { normalizePublicYouTubeUrl } from './youtubeSource.js';

const CANONICAL_URL = 'https://www.youtube.com/watch?v=AbCdEf12_-3';

test('canonicalizes supported YouTube forms for direct Gemini analysis', () => {
  for (const source of [
    'AbCdEf12_-3',
    CANONICAL_URL,
    'https://youtu.be/AbCdEf12_-3?t=10',
    'https://m.youtube.com/shorts/AbCdEf12_-3?feature=share',
    'https://www.youtube-nocookie.com/embed/AbCdEf12_-3',
  ]) {
    assert.equal(normalizePublicYouTubeUrl(source), CANONICAL_URL);
  }
});

test('rejects lookalike hosts, credentials and malformed video identifiers', () => {
  for (const source of [
    'https://youtube.com.evil.test/watch?v=AbCdEf12_-3',
    'https://user:password@youtube.com/watch?v=AbCdEf12_-3',
    'https://youtube.com/watch?v=too-short',
    'not a video source',
  ]) {
    assert.equal(normalizePublicYouTubeUrl(source), undefined);
  }
});
