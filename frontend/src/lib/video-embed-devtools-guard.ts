type InspectionKeyboardShortcut = Pick<KeyboardEvent, 'key' | 'ctrlKey' | 'shiftKey' | 'metaKey' | 'altKey'>;

// Keep this predicate self-contained: the embed executes its serialized source.
export function isVideoInspectionShortcut(event: InspectionKeyboardShortcut): boolean {
  const key = event.key.toLowerCase();
  if (key === 'f12') return true;
  if ((event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey && key === 'u') return true;
  if (event.ctrlKey && event.shiftKey && ['c', 'i', 'j', 'k'].includes(key)) return true;
  if (event.metaKey && event.altKey && ['c', 'i', 'j', 'u'].includes(key)) return true;
  return Boolean(event.metaKey && event.shiftKey && key === 'c');
}

export function installVideoInspectionGuard(target: Window, suspendPlayback: () => void): () => void {
  const onInspectionShortcut = (event: KeyboardEvent) => {
    if (!isVideoInspectionShortcut(event)) return;
    event.preventDefault();
    event.stopPropagation();
    suspendPlayback();
  };
  const listenerOptions: AddEventListenerOptions = { capture: true };
  target.addEventListener('keydown', onInspectionShortcut, listenerOptions);
  return () => target.removeEventListener('keydown', onInspectionShortcut, listenerOptions);
}

/**
 * Blocks common inspection keyboard shortcuts inside the isolated embed document.
 *
 * Viewport dimensions are deliberately ignored because browser zoom, sidebars,
 * accessibility tools, and desktop window chrome can all look like docked
 * DevTools. Provider authorization and signed playback sessions remain the
 * actual content-security boundary.
 */
export function createDevToolsSuspensionScript(suspendFunctionName: string): string {
  return `
var __videoEmbedSuspended = false;
var __isVideoEmbedDevToolsShortcut = ${isVideoInspectionShortcut.toString()};
function __suspendVideoEmbed() {
  if (__videoEmbedSuspended) return;
  __videoEmbedSuspended = true;
  window.setTimeout(function () { window.location.replace('about:blank'); }, 0);
  try { ${suspendFunctionName}(); } catch (error) {}
  try {
    window.parent.postMessage(
      { source: 'video-embed', type: 'securityViolation', data: { reason: 'devtools-shortcut' } },
      window.location.origin
    );
  } catch (error) {}
}
window.addEventListener('keydown', function (event) {
  if (!__isVideoEmbedDevToolsShortcut(event)) return;
  if (typeof event.preventDefault === 'function') event.preventDefault();
  if (typeof event.stopImmediatePropagation === 'function') event.stopImmediatePropagation();
  __suspendVideoEmbed();
}, true);
`;
}
