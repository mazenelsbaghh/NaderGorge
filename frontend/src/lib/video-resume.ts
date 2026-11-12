const SAVE_INTERVAL_MS = 4_000;

type Checkpoint = { seconds: number; observedAt: number };

export function openVideoResume(studentId: string, videoId: string, duration: number | null) {
  const key = `massar:video-resume:${studentId}:${videoId}`;
  const stored = readCheckpoint(key);
  let start = stored && (duration === null || stored.seconds < duration) ? stored.seconds : 0;
  let started = false;
  let pending: Checkpoint | undefined;
  let savedAt = 0;

  function save() {
    if (!pending) return;
    const latest = readCheckpoint(key);
    try {
      if (!latest || pending.observedAt >= latest.observedAt) {
        window.localStorage.setItem(key, JSON.stringify(pending));
      }
      pending = undefined;
      savedAt = Date.now();
    } catch {
      // Playback remains available when browser storage is disabled or full.
    }
  }

  return {
    get startAt() { return started ? 0 : start; },
    beginPlayback() { started = true; },
    recordPosition(seconds: number) {
      if (!started || !Number.isFinite(seconds) || seconds < 0) return;
      pending = { seconds: Math.floor(seconds), observedAt: Date.now() };
      if (pending.observedAt - savedAt >= SAVE_INTERVAL_MS) save();
    },
    save,
    finish() {
      if (!started) return;
      // A zero checkpoint prevents another tab from restoring an older sample.
      pending = { seconds: 0, observedAt: Date.now() };
      started = false;
      start = 0;
      save();
    },
  };
}

function readCheckpoint(key: string): Checkpoint | undefined {
  try {
    const checkpoint = JSON.parse(window.localStorage.getItem(key) ?? 'null') as Checkpoint | null;
    if (checkpoint && Number.isSafeInteger(checkpoint.seconds) && checkpoint.seconds >= 0
      && Number.isFinite(checkpoint.observedAt)) return checkpoint;
  } catch {
    // An unavailable or invalid checkpoint is equivalent to a first visit.
  }
}
