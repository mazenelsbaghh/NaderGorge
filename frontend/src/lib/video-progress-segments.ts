export type VideoProgressSegment = {
  seconds: number;
  playbackRate: number;
};

export type SequencedVideoProgressSegment = VideoProgressSegment & {
  sequence: number;
};

const SEGMENT_EPSILON_SECONDS = 0.01;

export function appendVideoProgressSegment(
  segments: VideoProgressSegment[],
  seconds: number,
  playbackRate: number,
): void {
  if (!Number.isFinite(seconds) || seconds <= 0) return;

  const lastSegment = segments[segments.length - 1];
  if (lastSegment && lastSegment.playbackRate === playbackRate) {
    lastSegment.seconds += seconds;
    return;
  }

  segments.push({ seconds, playbackRate });
}

export function peekVideoProgressSegment(
  segments: VideoProgressSegment[],
  maxSeconds: number,
): VideoProgressSegment | null {
  const firstSegment = segments[0];
  if (!firstSegment || firstSegment.seconds <= SEGMENT_EPSILON_SECONDS) return null;
  return {
    seconds: Math.min(maxSeconds, firstSegment.seconds),
    playbackRate: firstSegment.playbackRate,
  };
}

export function acknowledgeVideoProgressSegment(
  segments: VideoProgressSegment[],
  completed: VideoProgressSegment,
): void {
  const firstSegment = segments[0];
  if (!firstSegment || firstSegment.playbackRate !== completed.playbackRate) return;

  firstSegment.seconds -= completed.seconds;
  if (firstSegment.seconds <= SEGMENT_EPSILON_SECONDS) segments.shift();
}

export function sumVideoProgressWallSeconds(segments: VideoProgressSegment[]): number {
  return segments.reduce((total, segment) => total + segment.seconds, 0);
}

export function sumVideoProgressMediaSeconds(segments: VideoProgressSegment[]): number {
  return segments.reduce(
    (total, segment) => total + (segment.seconds * segment.playbackRate),
    0,
  );
}

/**
 * Freezes accumulated wall-clock time into immutable API requests. Assigning a
 * sequence also freezes the payload: a BFCache restore must replay the exact
 * same seconds for that sequence instead of reusing it for a larger segment.
 */
export function materializeVideoProgressRequests(
  segments: VideoProgressSegment[],
  requests: SequencedVideoProgressSegment[],
  nextSequence: number,
  maxSecondsPerRequest: number,
  maxRequestCount: number,
): number {
  let sequence = nextSequence;
  while (requests.length < maxRequestCount) {
    const segment = peekVideoProgressSegment(segments, maxSecondsPerRequest);
    if (!segment) break;

    requests.push({ ...segment, sequence });
    acknowledgeVideoProgressSegment(segments, segment);
    sequence += 1;
  }

  return sequence;
}

/**
 * Removes only requests explicitly confirmed by the server. This remains safe
 * when a normal request and a page-exit batch race: whichever response wins can
 * acknowledge its own sequence set, while the other becomes a harmless no-op.
 */
export function acknowledgeSequencedVideoProgressRequests(
  requests: SequencedVideoProgressSegment[],
  acknowledgedSequences: ReadonlySet<number>,
): void {
  let writeIndex = 0;
  for (const request of requests) {
    if (acknowledgedSequences.has(request.sequence)) continue;
    requests[writeIndex] = request;
    writeIndex += 1;
  }
  requests.length = writeIndex;
}
