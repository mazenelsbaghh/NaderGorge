const DEVTOOLS_DIMENSION_THRESHOLD_PX = 160;
const DEVTOOLS_POLL_INTERVAL_MS = 250;

/**
 * Builds a best-effort inspection guard for the isolated embed document.
 *
 * It fails closed: the document unloads instead of recreating a provider iframe
 * after a likely inspection event. This is a deterrent only; browser DevTools
 * and resources already recorded by the browser cannot be made confidential by
 * page JavaScript.
 */
export function createDevToolsSuspensionScript(suspendFunctionName: string): string {
  return `
var __videoEmbedSuspended = false;
function __isVideoEmbedInspectionLikely() {
  var topWindow;
  try {
    var userAgent = typeof navigator === 'undefined' ? '' : String(navigator.userAgent || '');
    var platform = typeof navigator === 'undefined' ? '' : String(navigator.platform || '');
    var touchPoints = typeof navigator === 'undefined' ? 0 : Number(navigator.maxTouchPoints || 0);
    var reportsMobileViewport = typeof navigator !== 'undefined'
      && Boolean(navigator.userAgentData && navigator.userAgentData.mobile);
    var usesMobileViewport = reportsMobileViewport
      || /Android|Mobile|iPad|iPhone|iPod/i.test(userAgent)
      || (platform === 'MacIntel' && touchPoints > 1);
    if (usesMobileViewport) return false;
    topWindow = window.top || window;
    var topDocument = topWindow.document;
    if (topDocument && (topDocument.fullscreenElement || topDocument.webkitFullscreenElement)) {
      return false;
    }
    var widthDifference = Number(topWindow.outerWidth) - Number(topWindow.innerWidth);
    var heightDifference = Number(topWindow.outerHeight) - Number(topWindow.innerHeight);
    return isFinite(widthDifference) && isFinite(heightDifference)
      && (widthDifference >= ${DEVTOOLS_DIMENSION_THRESHOLD_PX} || heightDifference >= ${DEVTOOLS_DIMENSION_THRESHOLD_PX});
  } catch (error) {
    return false;
  }
}
function __suspendVideoEmbed() {
  if (__videoEmbedSuspended) return;
  __videoEmbedSuspended = true;
  window.setTimeout(function () { window.location.replace('about:blank'); }, 0);
  try { ${suspendFunctionName}(); } catch (error) {}
  try {
    window.parent.postMessage(
      { source: 'video-embed', type: 'securityViolation', data: { reason: 'devtools-detected' } },
      window.location.origin
    );
  } catch (error) {}
}
function __enforceVideoEmbedProtection() {
  if (__isVideoEmbedInspectionLikely()) __suspendVideoEmbed();
}
__enforceVideoEmbedProtection();
window.addEventListener('resize', __enforceVideoEmbedProtection);
window.setInterval(__enforceVideoEmbedProtection, ${DEVTOOLS_POLL_INTERVAL_MS});
`;
}
