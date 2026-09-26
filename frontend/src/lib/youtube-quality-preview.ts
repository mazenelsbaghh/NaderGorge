// Experimental geometry, measured against the native embed in Chrome.
export const YOUTUBE_QUALITY_COMPACT_HEIGHT = 250;
export const YOUTUBE_QUALITY_GEAR_CLIP = 'polygon(evenodd,0 0,100% 0,100% 100%,0 100%,0 0,var(--quality-gear-left,26px) var(--quality-gear-top,2px),calc(var(--quality-gear-left,26px) + var(--quality-gear-size,44px)) var(--quality-gear-top,2px),calc(var(--quality-gear-left,26px) + var(--quality-gear-size,44px)) calc(var(--quality-gear-top,2px) + var(--quality-gear-size,44px)),var(--quality-gear-left,26px) calc(var(--quality-gear-top,2px) + var(--quality-gear-size,44px)),var(--quality-gear-left,26px) var(--quality-gear-top,2px))';

export function youtubeQualityPreviewGeometry(width: number, height: number) {
  // Keep a stable native layout; 318px Android embeds showed an account sheet.
  const canvasWidth = Math.max(360, width);
  const scale = width / canvasWidth;
  const canvasHeight = height / scale;
  return {
    canvasWidth, canvasHeight, scale,
    left: (canvasHeight <= YOUTUBE_QUALITY_COMPACT_HEIGHT ? 2 : 26) * scale,
    top: 2 * scale,
    size: 44 * scale,
  };
}

export function youtubeQualityCoverPercent(value: number): number {
  return Number.isFinite(value) ? Math.min(40, Math.max(0, value)) : 0;
}

export function youtubeQualityPreviewStyles({ bottomCoverPercent = 0, mobileBottomCoverPercent = 0 }: { bottomCoverPercent?: number; mobileBottomCoverPercent?: number } = {}): string {
  return `
    body { --menu-width:min(400px,calc(100% - 24px)); --menu-left:calc((100% - var(--menu-width)) / 2); }
    @media (max-height:${YOUTUBE_QUALITY_COMPACT_HEIGHT}px) { body { --quality-gear-left:2px; } }
    body.quality-started #click-overlay { clip-path:${YOUTUBE_QUALITY_GEAR_CLIP}; }
    .quality-mask { position:absolute; z-index:11; left:0; right:0; background:#000; pointer-events:none; }
    #quality-top-mask { top:0; height:48px; }
    body:not(.quality-started) #quality-top-mask { pointer-events:auto; }
    #quality-bottom-mask { bottom:0; height:calc(76px + ${youtubeQualityCoverPercent(bottomCoverPercent)}%); max-height:calc(100% - 48px); pointer-events:auto; }
    @media (any-pointer:coarse) { #quality-bottom-mask { height:calc(76px + ${youtubeQualityCoverPercent(mobileBottomCoverPercent)}%); } }
    #quality-start-mask { top:48px; bottom:76px; }
    body.quality-started #quality-start-mask { display:none; }
    body.quality-open #click-overlay { clip-path:polygon(evenodd,0 0,100% 0,100% 100%,0 100%,0 0,var(--quality-gear-left,26px) 2px,calc(var(--quality-gear-left,26px) + 44px) 2px,calc(var(--quality-gear-left,26px) + 44px) 46px,var(--quality-gear-left,26px) 46px,var(--quality-gear-left,26px) 2px,0 0,var(--menu-left) 48px,calc(var(--menu-left) + var(--menu-width)) 48px,calc(var(--menu-left) + var(--menu-width)) calc(100% - 24px),var(--menu-left) calc(100% - 24px),var(--menu-left) 48px,0 0); }
    body.quality-open #quality-bottom-mask { clip-path:polygon(evenodd,0 0,100% 0,100% 100%,0 100%,0 0,var(--menu-left) 0,calc(var(--menu-left) + var(--menu-width)) 0,calc(var(--menu-left) + var(--menu-width)) calc(100% - 24px),var(--menu-left) calc(100% - 24px),var(--menu-left) 0); }
  `;
}

export function youtubeQualityPreviewScript(): string {
  return `
var qualityOpenedWhilePaused = false;
function syncNativeQualityPlaybackState(state) {
  if (state === YT.PlayerState.PAUSED || state === YT.PlayerState.ENDED || (state === YT.PlayerState.PLAYING && qualityOpenedWhilePaused)) closeNativeQualityArea();
}
function closeNativeQualityArea() {
  if (!document.body.classList.contains('quality-open')) return;
  document.body.classList.remove('quality-open');
  postToParent('nativeQualityMenu', { open: false });
}
['quality-top-mask', 'quality-bottom-mask', 'quality-start-mask'].forEach(function(id) {
  var mask = document.createElement('div');
  mask.id = id;
  mask.className = 'quality-mask';
  document.body.appendChild(mask);
});
window.addEventListener('message', function(event) {
  if (event.origin !== window.location.origin || event.source !== window.parent) return;
  if (event.data && event.data.type === 'openNativeQualityMenu') {
    if (!document.body.classList.contains('quality-started') || document.body.classList.contains('quality-open')) return;
    qualityOpenedWhilePaused = player.getPlayerState() === YT.PlayerState.PAUSED;
    document.body.classList.add('quality-open');
    postToParent('nativeQualityMenu', { open: true });
  }
  if (event.data && event.data.type === 'closeNativeQualityMenu') {
    closeNativeQualityArea();
  }
});
`;
}
