export const BUNNY_BRIDGE_INITIAL_DEADLINE_MS = 30_000;
export const BUNNY_BRIDGE_AFTER_SURFACE_DEADLINE_MS = 8_000;
export const BUNNY_BRIDGE_RETRY_DEADLINE_MS = 15_000;

export interface BunnyBridgeReadinessWatchdog {
  start: () => void;
  markSurfaceLoaded: () => void;
  markReady: () => void;
  cancel: () => void;
}

interface BunnyBridgeReadinessWatchdogOptions<THandle> {
  schedule: (callback: () => void, delayMs: number) => THandle;
  cancelScheduled: (handle: THandle) => void;
  retryBridgeInPlace: () => boolean;
  recoverEmbed: () => void;
  initialDeadlineMs?: number;
  surfaceDeadlineMs?: number;
  retryDeadlineMs?: number;
}

/**
 * Keeps the provider surface and the Player.js tracking bridge as two separate
 * readiness signals. A loaded Bunny iframe gets one in-place bridge retry before
 * the caller is allowed to replace the embed.
 */
export function createBunnyBridgeReadinessWatchdog<THandle>(
  options: BunnyBridgeReadinessWatchdogOptions<THandle>,
): BunnyBridgeReadinessWatchdog {
  const initialDeadlineMs = options.initialDeadlineMs ?? BUNNY_BRIDGE_INITIAL_DEADLINE_MS;
  const surfaceDeadlineMs = options.surfaceDeadlineMs ?? BUNNY_BRIDGE_AFTER_SURFACE_DEADLINE_MS;
  const retryDeadlineMs = options.retryDeadlineMs ?? BUNNY_BRIDGE_RETRY_DEADLINE_MS;
  let scheduledHandle: THandle | null = null;
  let running = false;
  let bunnySurfaceLoaded = false;
  let retriedInPlace = false;

  const clearScheduled = () => {
    if (scheduledHandle === null) return;
    options.cancelScheduled(scheduledHandle);
    scheduledHandle = null;
  };

  const requireRecovery = () => {
    scheduledHandle = null;
    if (!running) return;
    running = false;
    options.recoverEmbed();
  };

  const handleInitialDeadline = () => {
    scheduledHandle = null;
    if (!running) return;

    // A browser/DNS failure can prevent the iframe load event altogether.
    // Still give Bunny's alternate trusted hostname one chance before
    // replacing the platform embed/session.
    if (!retriedInPlace) {
      retriedInPlace = true;
      if (options.retryBridgeInPlace()) {
        scheduledHandle = options.schedule(requireRecovery, retryDeadlineMs);
        return;
      }
    }

    requireRecovery();
  };

  return {
    start() {
      clearScheduled();
      running = true;
      bunnySurfaceLoaded = false;
      retriedInPlace = false;
      scheduledHandle = options.schedule(handleInitialDeadline, initialDeadlineMs);
    },
    markSurfaceLoaded() {
      if (!running || bunnySurfaceLoaded) return;
      bunnySurfaceLoaded = true;
      // Once the iframe document has loaded, a missing bridge no longer needs
      // the full network allowance. Fail over promptly if the loaded document
      // is a browser error page or a stalled Bunny host.
      clearScheduled();
      scheduledHandle = options.schedule(handleInitialDeadline, surfaceDeadlineMs);
    },
    markReady() {
      running = false;
      clearScheduled();
    },
    cancel() {
      running = false;
      clearScheduled();
    },
  };
}
