import { NextRequest, NextResponse } from 'next/server';
import crypto from 'crypto';

/**
 * GET /api/video/embed?t=<encryptedToken>&k=<base64Key>
 * 
 * Decrypts the video session token SERVER-SIDE and returns an HTML page
 * with the YouTube player embedded. This way, the YouTube video ID never
 * appears in the student's browser DOM or JavaScript context.
 * 
 * The URL visible in DevTools is: /api/video/embed?t=<encrypted>&k=<encrypted>
 * The encrypted data is meaningless without the backend's AES key.
 */
export async function GET(request: NextRequest) {
  try {
    const { searchParams } = new URL(request.url);
    const encryptedToken = searchParams.get('t');
    const base64Key = searchParams.get('k');

    if (!encryptedToken || !base64Key) {
      return new NextResponse('Missing parameters', { status: 400 });
    }

    // Decrypt server-side using Node.js crypto (same algorithm as frontend video-crypto.ts)
    const keyBytes = Buffer.from(base64Key, 'base64');
    const tokenBytes = Buffer.from(encryptedToken, 'base64');

    const IV_SIZE = 12;
    const TAG_SIZE = 16;

    if (tokenBytes.length < IV_SIZE + TAG_SIZE) {
      return new NextResponse('Invalid token', { status: 400 });
    }

    const iv = tokenBytes.subarray(0, IV_SIZE);
    const ciphertext = tokenBytes.subarray(IV_SIZE, tokenBytes.length - TAG_SIZE);
    const authTag = tokenBytes.subarray(tokenBytes.length - TAG_SIZE);

    const decipher = crypto.createDecipheriv('aes-256-gcm', keyBytes, iv);
    decipher.setAuthTag(authTag);

    let decrypted = decipher.update(ciphertext, undefined, 'utf8');
    decrypted += decipher.final('utf8');

    const parsed = JSON.parse(decrypted) as { Provider: string; VideoId: string };
    const videoId = parsed.VideoId;

    // Generate the HTML embed page
    const html = generateEmbedHtml(videoId);

    return new NextResponse(html, {
      status: 200,
      headers: {
        'Content-Type': 'text/html; charset=utf-8',
        'Cache-Control': 'no-store, no-cache, must-revalidate, private',
        'X-Content-Type-Options': 'nosniff',
        'X-Frame-Options': 'SAMEORIGIN',
        // Prevent the embed from being loaded in external sites
        'Content-Security-Policy': "frame-ancestors 'self'",
      },
    });
  } catch (error) {
    console.error('[video-embed] Decryption failed:', error);
    return new NextResponse('Session expired or invalid', { status: 403 });
  }
}

function generateEmbedHtml(videoId: string): string {
  return `<!DOCTYPE html>
<html lang="ar" dir="rtl">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Player</title>
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    html, body { width: 100%; height: 100%; overflow: hidden; background: #000; }
    #shell { position: relative; width: 100%; height: 100%; }
    .click-overlay {
      position: absolute; inset: 0; z-index: 10;
      background: transparent; cursor: pointer;
    }
  </style>
</head>
<body oncontextmenu="return false" ondragstart="return false" onselectstart="return false">
  <!-- Shell is the visible host — its contents are hidden inside a closed Shadow DOM -->
  <div id="shell"></div>
  <div class="click-overlay" id="click-overlay"></div>

  <script>
    // ── Closed Shadow DOM inside the embed page ──
    // Even if someone drills into our API iframe in DevTools,
    // they'll only see <div id="shell"> with #shadow-root (closed).
    // The YouTube iframe is invisible.

    var shell = document.getElementById('shell');
    var shadow = shell.attachShadow({ mode: 'closed' });

    // Trap shadowRoot access
    try {
      Object.defineProperty(shell, 'shadowRoot', {
        get: function() { return null; },
        configurable: false
      });
    } catch(e) {}

    // Build player structure inside shadow
    var wrap = document.createElement('div');
    wrap.style.cssText = 'position:relative;width:100%;height:100%;background:#000;overflow:hidden';

    var ytDiv = document.createElement('div');
    ytDiv.id = 'yt-' + Math.random().toString(36).substr(2,9);
    ytDiv.style.cssText = 'position:absolute;top:0;left:0;width:100%;height:100%;pointer-events:none';

    // Cover overlays (hide YouTube branding)
    var coverTop = document.createElement('div');
    coverTop.style.cssText = 'position:absolute;top:0;left:0;right:0;height:80px;background:linear-gradient(to bottom,rgba(0,0,0,1) 40%,rgba(0,0,0,0) 100%);pointer-events:none;z-index:5';
    var coverLogo = document.createElement('div');
    coverLogo.style.cssText = 'position:absolute;bottom:0;right:0;width:140px;height:60px;background:#000;pointer-events:none;z-index:5';
    var coverShadow = document.createElement('div');
    coverShadow.style.cssText = 'position:absolute;inset:0;box-shadow:inset 0 0 40px 10px #000;pointer-events:none;z-index:5';

    wrap.appendChild(ytDiv);
    wrap.appendChild(coverTop);
    wrap.appendChild(coverLogo);
    wrap.appendChild(coverShadow);
    shadow.appendChild(wrap);

    // ── YouTube IFrame API ──
    var player = null;
    var progressInterval = null;
    var ytDivId = ytDiv.id;

    // YouTube API needs getElementById — patch it for shadow DOM
    var origGetById = document.getElementById.bind(document);
    document.getElementById = function(id) {
      if (id === ytDivId) return ytDiv;
      return origGetById(id);
    };

    var tag = document.createElement('script');
    tag.src = 'https://www.youtube.com/iframe_api';
    document.head.appendChild(tag);

    function onYouTubeIframeAPIReady() {
      player = new YT.Player(ytDivId, {
        videoId: '${videoId}',
        playerVars: {
          autoplay: 1,
          controls: 0,
          disablekb: 1,
          modestbranding: 1,
          rel: 0,
          fs: 0,
          iv_load_policy: 3,
          playsinline: 1
        },
        events: {
          onReady: function(e) {
            // Restore getElementById
            document.getElementById = origGetById;
            
            postToParent('ready', {
              duration: e.target.getDuration(),
              volume: e.target.getVolume(),
              isMuted: e.target.isMuted()
            });
            e.target.playVideo();
            startProgressUpdates();
          },
          onStateChange: function(e) {
            postToParent('stateChange', {
              state: e.data,
              isPlaying: e.data === YT.PlayerState.PLAYING
            });
          },
          onError: function(e) {
            postToParent('error', { code: e.data });
          }
        }
      });
    }

    function startProgressUpdates() {
      if (progressInterval) clearInterval(progressInterval);
      progressInterval = setInterval(function() {
        if (player && typeof player.getCurrentTime === 'function') {
          postToParent('timeUpdate', {
            currentTime: player.getCurrentTime(),
            duration: player.getDuration(),
            volume: player.getVolume(),
            isMuted: player.isMuted(),
            state: player.getPlayerState()
          });
        }
      }, 500);
    }

    window.addEventListener('message', function(event) {
      if (!player) return;
      var msg = event.data;
      if (!msg || !msg.type) return;
      switch (msg.type) {
        case 'play': player.playVideo(); break;
        case 'pause': player.pauseVideo(); break;
        case 'seekTo': player.seekTo(msg.time, true); break;
        case 'setVolume': player.setVolume(msg.volume); break;
        case 'mute': player.mute(); break;
        case 'unmute': player.unMute(); break;
        case 'setPlaybackRate': player.setPlaybackRate(msg.rate); break;
        case 'setQuality': if (player.setPlaybackQuality) player.setPlaybackQuality(msg.quality); break;
        case 'getState':
          if (typeof player.getCurrentTime === 'function') {
            postToParent('stateResponse', {
              currentTime: player.getCurrentTime(),
              duration: player.getDuration(),
              volume: player.getVolume(),
              isMuted: player.isMuted(),
              state: player.getPlayerState()
            });
          }
          break;
      }
    });

    function postToParent(type, data) {
      try {
        window.parent.postMessage({ source: 'video-embed', type: type, data: data }, '*');
      } catch(e) {}
    }

    document.getElementById('click-overlay').addEventListener('click', function() {
      if (player) {
        var state = player.getPlayerState();
        if (state === YT.PlayerState.PLAYING) {
          player.pauseVideo();
        } else {
          player.playVideo();
        }
      }
    });
  </script>
</body>
</html>`;
}
