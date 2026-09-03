import assert from 'node:assert/strict';
import test from 'node:test';
import {
  acknowledgeSequencedVideoProgressRequests,
  acknowledgeVideoProgressSegment,
  appendVideoProgressSegment,
  materializeVideoProgressRequests,
  peekVideoProgressSegment,
  sumVideoProgressMediaSeconds,
  sumVideoProgressWallSeconds,
  type SequencedVideoProgressSegment,
  type VideoProgressSegment,
} from './video-progress-segments.ts';

test('progress keeps playback-rate segments ordered while a request is in flight', () => {
  const segments: VideoProgressSegment[] = [];
  appendVideoProgressSegment(segments, 5, 1);
  const active = peekVideoProgressSegment(segments, 30);
  appendVideoProgressSegment(segments, 5, 2);

  assert.deepEqual(active, { seconds: 5, playbackRate: 1 });
  assert.deepEqual(segments, [
    { seconds: 5, playbackRate: 1 },
    { seconds: 5, playbackRate: 2 },
  ]);
  assert.equal(sumVideoProgressWallSeconds(segments), 10);
  assert.equal(sumVideoProgressMediaSeconds(segments), 15);

  acknowledgeVideoProgressSegment(segments, active!);
  assert.deepEqual(segments, [{ seconds: 5, playbackRate: 2 }]);
});

test('progress drains long same-rate playback in bounded ordered chunks', () => {
  const segments: VideoProgressSegment[] = [];
  appendVideoProgressSegment(segments, 64, 1.5);

  const first = peekVideoProgressSegment(segments, 30);
  assert.deepEqual(first, { seconds: 30, playbackRate: 1.5 });
  acknowledgeVideoProgressSegment(segments, first!);
  assert.equal(segments[0]?.seconds, 34);

  const second = peekVideoProgressSegment(segments, 30);
  assert.deepEqual(second, { seconds: 30, playbackRate: 1.5 });
  acknowledgeVideoProgressSegment(segments, second!);
  assert.equal(segments[0]?.seconds, 4);
});

test('page-exit materialization freezes an in-flight request and the entire queued tail', () => {
  const rawSegments: VideoProgressSegment[] = [
    { seconds: 34, playbackRate: 1 },
    { seconds: 7, playbackRate: 2 },
  ];
  const fixedRequests: SequencedVideoProgressSegment[] = [
    { sequence: 8, seconds: 30, playbackRate: 1 },
  ];

  const nextSequence = materializeVideoProgressRequests(
    rawSegments,
    fixedRequests,
    9,
    30,
    30,
  );

  assert.equal(nextSequence, 12);
  assert.deepEqual(fixedRequests, [
    { sequence: 8, seconds: 30, playbackRate: 1 },
    { sequence: 9, seconds: 30, playbackRate: 1 },
    { sequence: 10, seconds: 4, playbackRate: 1 },
    { sequence: 11, seconds: 7, playbackRate: 2 },
  ]);
  assert.deepEqual(rawSegments, []);
});

test('normal ACK then batch ACK reconciles without subtracting a request twice', () => {
  const fixedRequests: SequencedVideoProgressSegment[] = [
    { sequence: 1, seconds: 30, playbackRate: 1 },
    { sequence: 2, seconds: 30, playbackRate: 1 },
    { sequence: 3, seconds: 4, playbackRate: 1 },
  ];
  const pageExitSnapshot = fixedRequests.map((request) => ({ ...request }));

  acknowledgeSequencedVideoProgressRequests(fixedRequests, new Set([1]));
  assert.deepEqual(fixedRequests.map((request) => request.sequence), [2, 3]);

  acknowledgeSequencedVideoProgressRequests(
    fixedRequests,
    new Set(pageExitSnapshot.map((request) => request.sequence)),
  );
  assert.deepEqual(fixedRequests, []);
});

test('batch ACK then late normal ACK is idempotent', () => {
  const fixedRequests: SequencedVideoProgressSegment[] = [
    { sequence: 1, seconds: 30, playbackRate: 1 },
    { sequence: 2, seconds: 8, playbackRate: 1.5 },
  ];
  const pageExitSnapshot = fixedRequests.map((request) => ({ ...request }));

  acknowledgeSequencedVideoProgressRequests(
    fixedRequests,
    new Set(pageExitSnapshot.map((request) => request.sequence)),
  );
  acknowledgeSequencedVideoProgressRequests(fixedRequests, new Set([1]));

  assert.deepEqual(fixedRequests, []);
});

test('BFCache restore replays fixed payloads and gives new playback fresh sequences', () => {
  const rawSegments: VideoProgressSegment[] = [{ seconds: 64, playbackRate: 1 }];
  const fixedRequests: SequencedVideoProgressSegment[] = [];
  let nextSequence = materializeVideoProgressRequests(rawSegments, fixedRequests, 1, 30, 30);
  const pageExitSnapshot = fixedRequests.map((request) => ({ ...request }));

  appendVideoProgressSegment(rawSegments, 5, 2);
  assert.deepEqual(fixedRequests, pageExitSnapshot, 'unacknowledged exit payload must remain immutable');
  assert.deepEqual(fixedRequests[0], { sequence: 1, seconds: 30, playbackRate: 1 });

  acknowledgeSequencedVideoProgressRequests(fixedRequests, new Set([1, 2, 3]));
  nextSequence = materializeVideoProgressRequests(rawSegments, fixedRequests, nextSequence, 30, 30);

  assert.equal(nextSequence, 5);
  assert.deepEqual(fixedRequests, [{ sequence: 4, seconds: 5, playbackRate: 2 }]);
});

test('page-exit batch respects its request cap and retains overflow for a later drain', () => {
  const rawSegments: VideoProgressSegment[] = [{ seconds: 935, playbackRate: 1 }];
  const fixedRequests: SequencedVideoProgressSegment[] = [];

  const nextSequence = materializeVideoProgressRequests(rawSegments, fixedRequests, 1, 30, 30);

  assert.equal(fixedRequests.length, 30);
  assert.equal(nextSequence, 31);
  assert.deepEqual(rawSegments, [{ seconds: 35, playbackRate: 1 }]);
});
