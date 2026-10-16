// Allow the material request (15s) plus both embed initialization phases (20s each).
export const YOUTUBE_BOOTSTRAP_DEADLINE_MS = 60_000;

export type YouTubeStartupFailurePhase = 'sdk_load' | 'sdk_timeout' | 'iframe_timeout' | 'provider_error' | 'bootstrap_timeout';

export interface YouTubeStartupRecovery {
  startAttempt: () => boolean;
  failAttempt: () => 'retry' | 'exhausted' | 'ignored';
  markReady: () => void;
  suspend: () => void;
  resume: () => void;
  cancel: () => void;
  isRecoveryAttempt: () => boolean;
}

interface StartupRecoveryOptions<THandle> {
  schedule: (callback: () => void, delayMs: number) => THandle;
  cancelScheduled: (handle: THandle) => void;
  onTimeout: () => void;
}

export function createYouTubeStartupRecovery<THandle>(options: StartupRecoveryOptions<THandle>): YouTubeStartupRecovery {
  let phase: 'idle' | 'loading' | 'retrying' | 'ready' | 'ended' = 'idle';
  let retried = false;
  let suspended = false;
  let deadline: THandle | null = null;
  let generation = 0;

  const clearDeadline = () => {
    generation += 1;
    if (deadline !== null) options.cancelScheduled(deadline);
    deadline = null;
  };

  const armDeadline = () => {
    clearDeadline();
    const attemptGeneration = generation;
    deadline = options.schedule(() => {
      if (phase === 'loading' && attemptGeneration === generation) options.onTimeout();
    }, YOUTUBE_BOOTSTRAP_DEADLINE_MS);
  };

  return {
    startAttempt() {
      if (phase !== 'idle' && phase !== 'retrying') return false;
      phase = 'loading';
      suspended = false;
      armDeadline();
      return true;
    },
    failAttempt() {
      if (phase !== 'loading') return 'ignored';
      clearDeadline();
      phase = retried ? 'ended' : 'retrying';
      if (retried) return 'exhausted';
      retried = true;
      return 'retry';
    },
    markReady() {
      if (phase !== 'loading') return;
      phase = 'ready';
      clearDeadline();
    },
    suspend() {
      if (phase !== 'loading') return;
      suspended = true;
      clearDeadline();
    },
    resume() {
      if (phase !== 'loading' || !suspended) return;
      suspended = false;
      armDeadline();
    },
    cancel() {
      phase = 'ended';
      clearDeadline();
    },
    isRecoveryAttempt: () => retried,
  };
}
