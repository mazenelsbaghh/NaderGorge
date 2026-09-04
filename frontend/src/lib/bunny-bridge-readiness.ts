export const BUNNY_BRIDGE_INITIAL_DEADLINE_MS = 30_000;
export const BUNNY_BRIDGE_AFTER_SURFACE_DEADLINE_MS = 20_000;
export const BUNNY_BRIDGE_RETRY_DEADLINE_MS = 20_000;

export interface BunnyBridgeReadinessWatchdog {
  start: () => void;
  markSurfaceLoaded: () => void;
  markReady: () => void;
  cancel: () => void;
}

interface BunnyBridgeReadinessWatchdogOptions<THandle> {
  schedule: (callback: () => void, delayMs: number) => THandle;
  cancelScheduled: (handle: THandle) => void;
  retryBridgeInPlace: (options: { source: 'current' | 'alternate' }) => boolean;
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
  let retriedCurrentSource = false;
  let triedAlternateSource = false;

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

  const handleDeadline = () => {
    scheduledHandle = null;
    if (!running) return;

    // A loaded Bunny document can be usable while its Player.js handshake is
    // delayed on a congested mobile connection. Re-probe the same document
    // first so we do not throw away downloaded media or create a reload loop.
    if (bunnySurfaceLoaded && !retriedCurrentSource) {
      retriedCurrentSource = true;
      if (options.retryBridgeInPlace({ source: 'current' })) {
        scheduledHandle = options.schedule(handleDeadline, retryDeadlineMs);
        return;
      }
    }

    // If the document never loaded, or repeated probes could not establish a
    // media-clock bridge, try Bunny's alternate supported hostname once.
    if (!triedAlternateSource) {
      triedAlternateSource = true;
      if (options.retryBridgeInPlace({ source: 'alternate' })) {
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
      retriedCurrentSource = false;
      triedAlternateSource = false;
      scheduledHandle = options.schedule(handleDeadline, initialDeadlineMs);
    },
    markSurfaceLoaded() {
      if (!running || bunnySurfaceLoaded) return;
      bunnySurfaceLoaded = true;
      // Once the iframe document has loaded, a missing bridge no longer needs
      // the full network allowance, but still allow enough time for mobile 4G
      // to finish Bunny's scripts before re-probing the same document.
      clearScheduled();
      scheduledHandle = options.schedule(handleDeadline, surfaceDeadlineMs);
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
