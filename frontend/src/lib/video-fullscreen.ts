type FullscreenDocument = Document & {
  webkitFullscreenElement?: Element | null;
  webkitExitFullscreen?: () => Promise<void> | void;
};

type FullscreenElement = HTMLElement & {
  webkitRequestFullscreen?: () => Promise<void> | void;
};

export type NativeFullscreenVideo = HTMLVideoElement & {
  webkitEnterFullscreen?: () => void;
  webkitExitFullscreen?: () => void;
  webkitSupportsFullscreen?: boolean;
};

export function findNativeFullscreenVideo(root: HTMLElement): NativeFullscreenVideo | null {
  const direct = root.querySelector('video') as NativeFullscreenVideo | null;
  if (direct?.webkitEnterFullscreen) return direct;
  for (const iframe of root.querySelectorAll('iframe')) {
    try {
      const video = iframe.contentDocument?.querySelector('video') as NativeFullscreenVideo | null;
      if (video?.webkitEnterFullscreen) return video;
    } catch {
      // Cross-origin provider frames remain controlled by their own player.
    }
  }
  return null;
}

export function enterNativeVideoFullscreen(video: NativeFullscreenVideo, onExit: () => void): (() => void) | null {
  if (!video.webkitEnterFullscreen || video.webkitSupportsFullscreen === false || video.readyState < 1) return null;
  const cleanup = () => video.removeEventListener('webkitendfullscreen', onExit);
  video.addEventListener('webkitendfullscreen', onExit, { once: true });
  try {
    // Must run synchronously in the click gesture, before any awaited container request.
    video.webkitEnterFullscreen();
    return cleanup;
  } catch {
    cleanup();
    return null;
  }
}

type LockableScreenOrientation = ScreenOrientation & {
  lock?: (orientation: 'landscape') => Promise<void>;
  unlock?: () => void;
};

export function getFullscreenElement(documentLike: Document): Element | null {
  const vendorDocument = documentLike as FullscreenDocument;
  return documentLike.fullscreenElement ?? vendorDocument.webkitFullscreenElement ?? null;
}

export async function requestVideoFullscreen(element: HTMLElement, timeoutMs = 700): Promise<boolean> {
  const vendorElement = element as FullscreenElement;
  let timeout: ReturnType<typeof setTimeout> | undefined;
  try {
    const request = element.requestFullscreen ?? vendorElement.webkitRequestFullscreen;
    if (!request) return false;
    // Some iOS WebViews expose this API but never settle its promise.
    return await Promise.race([
      Promise.resolve(request.call(element)).then(() => true),
      new Promise<boolean>((resolve) => { timeout = setTimeout(() => resolve(false), timeoutMs); }),
    ]);
  } catch {
    return false;
  } finally {
    if (timeout) clearTimeout(timeout);
  }
}

export async function waitForVideoFullscreen(
  documentLike: Document,
  timeoutMs = 300,
): Promise<boolean> {
  if (getFullscreenElement(documentLike)) return true;

  return new Promise<boolean>((resolve) => {
    let timeout: ReturnType<typeof setTimeout> | null = null;
    const finish = (entered: boolean) => {
      documentLike.removeEventListener('fullscreenchange', handleChange);
      documentLike.removeEventListener('webkitfullscreenchange', handleChange);
      if (timeout) clearTimeout(timeout);
      resolve(entered);
    };
    const handleChange = () => {
      if (getFullscreenElement(documentLike)) finish(true);
    };

    documentLike.addEventListener('fullscreenchange', handleChange);
    documentLike.addEventListener('webkitfullscreenchange', handleChange);
    timeout = setTimeout(() => finish(Boolean(getFullscreenElement(documentLike))), timeoutMs);
  });
}

export async function exitVideoFullscreen(documentLike: Document): Promise<boolean> {
  const vendorDocument = documentLike as FullscreenDocument;
  try {
    if (documentLike.exitFullscreen) {
      await documentLike.exitFullscreen();
      return true;
    }
    if (vendorDocument.webkitExitFullscreen) {
      await vendorDocument.webkitExitFullscreen();
      return true;
    }
  } catch {
    return false;
  }
  return false;
}

export async function lockVideoToLandscape(screenLike: Screen): Promise<boolean> {
  const orientation = screenLike.orientation as LockableScreenOrientation | undefined;
  if (!orientation?.lock) return false;
  try {
    await orientation.lock('landscape');
    return true;
  } catch {
    return false;
  }
}

export function unlockVideoOrientation(screenLike: Screen): void {
  const orientation = screenLike.orientation as LockableScreenOrientation | undefined;
  try {
    orientation?.unlock?.();
  } catch {
    // Some embedded browsers expose unlock but reject the call.
  }
}
