/**
 * Blocks common DevTools keyboard shortcuts inside the isolated embed document.
 *
 * Viewport dimensions are deliberately ignored because browser zoom, sidebars,
 * accessibility tools, and desktop window chrome can all look like docked
 * DevTools. Provider authorization and signed playback sessions remain the
 * actual content-security boundary.
 */
export function createDevToolsSuspensionScript(suspendFunctionName: string): string {
  return `
var __videoEmbedSuspended = false;
function __isVideoEmbedDevToolsShortcut(event) {
  if (!event) return false;
  if (String(event.key || '') === 'F12') return true;
  var key = String(event.key || '').toLowerCase();
  if (event.ctrlKey && event.shiftKey
    && (key === 'c' || key === 'i' || key === 'j' || key === 'k')) return true;
  if (event.metaKey && event.altKey
    && (key === 'c' || key === 'i' || key === 'j' || key === 'u')) return true;
  return Boolean(event.metaKey && event.shiftKey && key === 'c');
}
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
