type FullscreenDocument = Document & {
  webkitFullscreenElement?: Element | null;
  webkitExitFullscreen?: () => Promise<void> | void;
};

type FullscreenElement = HTMLElement & {
  webkitRequestFullscreen?: () => Promise<void> | void;
};

type LockableScreenOrientation = ScreenOrientation & {
  lock?: (orientation: 'landscape') => Promise<void>;
  unlock?: () => void;
};

export function getFullscreenElement(documentLike: Document): Element | null {
  const vendorDocument = documentLike as FullscreenDocument;
  return documentLike.fullscreenElement ?? vendorDocument.webkitFullscreenElement ?? null;
}

export async function requestVideoFullscreen(element: HTMLElement): Promise<boolean> {
  const vendorElement = element as FullscreenElement;
  try {
    if (element.requestFullscreen) {
      await element.requestFullscreen();
      return true;
    }
    if (vendorElement.webkitRequestFullscreen) {
      await vendorElement.webkitRequestFullscreen();
      return true;
    }
  } catch {
    return false;
  }
  return false;
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
