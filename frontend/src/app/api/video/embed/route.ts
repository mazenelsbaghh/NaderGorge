import { NextRequest, NextResponse } from 'next/server';
import crypto from 'crypto';
import {
  isBunnyLibraryId,
  isBunnyVideoGuid,
  parseScopedBunnyVideoReference,
  type BunnyVideoReference,
} from '@/lib/bunny-video-reference';
import { createDevToolsSuspensionScript } from '@/lib/video-embed-devtools-guard';
import { validateVideoEmbedNavigation } from '@/lib/video-embed-request-guard';

/**
 * GET /api/video/embed?s=<sessionId>
 * 
 * Fetches encrypted video material server-side and returns an HTML page with
 * the player embedded. The browser-visible URL never carries token/key data.
 */
const API_URL = (process.env.INTERNAL_API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://backend:5245/api').replace(/\/$/, '');
const INTERNAL_TOKEN = process.env.API_CALLBACK_SECRET || process.env.AI_CALLBACK_SECRET;

type VideoEmbedMaterialResponse = {
  token?: string;
  key?: string;
  Token?: string;
  Key?: string;
};

function embedErrorHtml(message: string) {
  const safeMessage = JSON.stringify(message);
  const visibleMessage = escapeHtml(message);
  return `<!DOCTYPE html>
<html lang="ar" dir="rtl">
<head><meta charset="UTF-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"></head>
<body style="margin:0;background:#000;color:#fff;font-family:system-ui,sans-serif;display:grid;place-items:center;height:100vh;text-align:center;padding:24px">
<p>${visibleMessage}</p>
<script>
try {
  window.parent.postMessage({ source: 'video-embed', type: 'error', data: { message: ${safeMessage} } }, window.location.origin);
} catch (e) {}
</script>
</body>
</html>`;
}

function iframeError(message: string, status = 500) {
  return new NextResponse(embedErrorHtml(message), {
    status,
    headers: {
      'Content-Type': 'text/html; charset=utf-8',
      'Cache-Control': 'no-store, no-cache, must-revalidate, private',
      'X-Content-Type-Options': 'nosniff',
      'X-Frame-Options': 'SAMEORIGIN',
      'Content-Security-Policy': "frame-ancestors 'self'",
      'Permissions-Policy': 'display-capture=(), picture-in-picture=()',
      // YouTube requires an HTTP Referer (or equivalent client identity) for embeds.
      'Referrer-Policy': 'strict-origin-when-cross-origin',
    },
  });
}

export async function GET(request: NextRequest) {
  try {
    // Fetch Metadata is defense-in-depth around the short-lived session ID.
    const navigationError = validateVideoEmbedNavigation(request.url, request.headers);
    if (navigationError === 'missing-context') {
      return iframeError('Embed must be loaded within Massar Academy', 403);
    }
    if (navigationError === 'unauthorized-origin') {
      return iframeError('Unauthorized embedding', 403);
    }

    const { searchParams } = new URL(request.url);
    const sessionId = searchParams.get('s');

    if (!sessionId) {
      return iframeError('Missing session', 400);
    }

    if (!INTERNAL_TOKEN) {
      return iframeError('Embed proxy is not configured. API_CALLBACK_SECRET is missing from the frontend runtime.', 503);
    }

    let materialResponse: Response;
    try {
      materialResponse = await fetch(`${API_URL}/v1/internal/video-sessions/${encodeURIComponent(sessionId)}/embed-material`, {
        headers: {
          'X-Internal-Token': INTERNAL_TOKEN,
        },
        cache: 'no-store',
        redirect: 'error',
      });
    } catch (error) {
      console.error('[video-embed] Embed material request failed:', error);
      return iframeError('تعذر الاتصال بخدمة الفيديو. حاول مرة أخرى.', 502);
    }

    if (!materialResponse.ok) {
      if (materialResponse.status === 404 || materialResponse.status === 410) {
        return iframeError('Session expired or invalid', materialResponse.status);
      }

      console.error(`[video-embed] Embed material request returned status ${materialResponse.status}`);
      return iframeError('تعذر الاتصال بخدمة الفيديو. حاول مرة أخرى.', 502);
    }

    const material = (await materialResponse.json()) as VideoEmbedMaterialResponse;
    const encryptedToken = material.token ?? material.Token;
    const base64Key = material.key ?? material.Key;

    if (!encryptedToken || !base64Key) {
      return iframeError('Embed material response is missing token/key', 502);
    }

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

    const parsed = JSON.parse(decrypted) as { Provider: string; VideoId: string; StudentName?: string; StudentPhone?: string };
    const videoId = parsed.VideoId;
    const provider = parsed.Provider?.toLowerCase() || 'youtube';
    const studentName = parsed.StudentName || 'Massar Academy';
    const studentPhone = parsed.StudentPhone || '';

    if (provider === 'vk' && !videoId.match(/oid=([^&]+)&id=([^&]+)/)) {
      return iframeError('Invalid VK video identifier format. Expected: oid=-XXXXX&id=XXXXX', 400);
    }
    const html = generateVideoEmbedHtml(provider, videoId, studentName, studentPhone);

    return new NextResponse(html, {
      status: 200,
      headers: {
        'Content-Type': 'text/html; charset=utf-8',
        'Cache-Control': 'no-store, no-cache, must-revalidate, private',
        'X-Content-Type-Options': 'nosniff',
        'X-Frame-Options': 'SAMEORIGIN',
        'Content-Security-Policy': "frame-ancestors 'self'",
        'Permissions-Policy': 'display-capture=(), picture-in-picture=()',
        // Preserve the application origin for the nested YouTube iframe.
        'Referrer-Policy': 'strict-origin-when-cross-origin',
      },
    });
  } catch (error) {
    console.error('[video-embed] Embed material preparation failed:', error);
    return iframeError('تعذر تجهيز جلسة الفيديو. حاول مرة أخرى.', 502);
  }
}

/** Shared by secured lesson sessions and public teacher-introduction videos. */
export function generateVideoEmbedHtml(provider: string, videoId: string, studentName = 'Massar Academy', studentPhone = ''): string {
  const normalizedProvider = provider.toLowerCase();
  if (normalizedProvider === 'vk') {
    const match = videoId.match(/oid=([^&]+)&id=([^&]+)/);
    return match ? generateVkEmbedHtml(match[1], match[2], studentName, studentPhone) : '';
  }
  if (normalizedProvider === 'bunny') {
    return generateBunnyEmbedHtml(videoId, studentName, studentPhone);
  }
  return generateYouTubeEmbedHtml(videoId, studentName, studentPhone);
}

function configuredLegacyBunnyLibraryId() {
  const libraryId = (process.env.BUNNY_STREAM_LIBRARY_ID || process.env.NEXT_PUBLIC_BUNNY_STREAM_LIBRARY_ID || '').trim();
  return isBunnyLibraryId(libraryId) ? libraryId : undefined;
}

function resolveBunnyVideoReference(videoId: string): BunnyVideoReference | string {
  const normalizedVideoId = videoId.trim();
  const scopedReference = parseScopedBunnyVideoReference(normalizedVideoId);
  if (scopedReference) return scopedReference;

  // Bare GUIDs only exist in sessions created before videos stored their library scope.
  if (isBunnyVideoGuid(normalizedVideoId)) {
    const legacyLibraryId = configuredLegacyBunnyLibraryId();
    return legacyLibraryId
      ? { libraryId: legacyLibraryId, videoGuid: normalizedVideoId }
      : 'تعذر تشغيل فيديو Bunny القديم لأن رقم المكتبة غير مُهيأ.';
  }

  return 'مرجع فيديو Bunny غير صالح. يجب أن يحتوي على رقم المكتبة ومعرّف الفيديو.';
}

function generateBunnyEmbedHtml(videoId: string, studentName: string, studentPhone: string): string {
  const reference = resolveBunnyVideoReference(videoId);
  if (typeof reference === 'string') return embedErrorHtml(reference);

  const playerQuery = 'autoplay=false&playsinline=true&disableIosPlayer=true';
  const safeLegacySrc = JSON.stringify(`https://iframe.mediadelivery.net/embed/${reference.libraryId}/${reference.videoGuid}?${playerQuery}`);
  const safeModernSrc = JSON.stringify(`https://player.mediadelivery.net/embed/${reference.libraryId}/${reference.videoGuid}?${playerQuery}`);
  const watermarkBrand = escapeHtml('Massar Academy');
  const watermarkStudentName = escapeHtml(studentName);
  const watermarkStudentPhone = escapeHtml(studentPhone);
  const devToolsGuard = createDevToolsSuspensionScript('suspendBunnyPlayerForInspection');

  return `<!DOCTYPE html>
<html lang="ar" dir="rtl">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <meta name="referrer" content="strict-origin-when-cross-origin">
  <title>Player</title>
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Tajawal:wght@700;900&family=Montserrat:wght@700;900&display=swap" rel="stylesheet">
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    html, body { width: 100%; height: 100%; overflow: hidden; background: #000; }
    #wrap { position: relative; width: 100%; height: 100%; background: #000; }
    #bunny-frame { position: absolute; inset: 0; width: 100%; height: 100%; border: 0; }
    #video-watermark {
      position: absolute; top: 0; left: 0; z-index: 20; pointer-events: none;
      color: rgba(255,255,255,.18); font-size: .9rem; font-size: clamp(.9rem, 4vw, 1.4rem); font-family: Tajawal, Montserrat, system-ui, sans-serif;
      text-shadow: 1px 1px 2px rgba(0,0,0,.5); user-select: none; white-space: pre-wrap;
      width: 42vw; max-width: 18rem; overflow-wrap: anywhere;
      transform: translate3d(15vw, 15vh, 0); text-align: center; line-height: 1.3;
      transition: transform 1.5s ease-in-out;
    }
  </style>
</head>
<body oncontextmenu="return false" ondragstart="return false" onselectstart="return false">
  <div id="wrap">
    <iframe id="bunny-frame" allow="accelerometer;gyroscope;autoplay;encrypted-media;picture-in-picture"></iframe>
    <div id="video-watermark">
      <span style="font-weight:900">${watermarkBrand}</span><br>
      <span style="font-size:.75em;font-weight:700">${watermarkStudentName}</span><br>
      <span style="font-size:.6em">${watermarkStudentPhone}</span>
    </div>
  </div>

  <script src="/vendor/playerjs/player-0.1.0.min.js"></script>
  <script>
    // ═══════════════════════════════════════════════════════
    // Bunny Player.js → Parent PostMessage Bridge
    // ═══════════════════════════════════════════════════════
    function postToParent(type, data) {
      if (typeof __videoEmbedSuspended !== 'undefined' && __videoEmbedSuspended) return;
      try {
        window.parent.postMessage({ source: 'video-embed', type: type, data: data }, window.location.origin);
      } catch (e) {}
    }

    var iframe = document.getElementById('bunny-frame');
    var player = null;
    var isPlaying = false;
    var progressInterval = null;
    var playerReady = false;
    var parentReadySent = false;
    var lastKnownDuration = 0;
    var lastKnownVolume = 1;
    var lastKnownPlaybackRate = 1;
    var lastObservedTime = null;
    var advancingTimeSamples = 0;
    var pollTimer = null;
    var bridgeReadyProbeListener = 'massar-bunny-ready-probe-v1';
    var bunnyBridgeOrigins = ['https://player.mediadelivery.net', 'https://iframe.mediadelivery.net'];
    var bunnyBridgeOrigin = null;
    // Prefer Bunny's current player endpoint and retain its legacy iframe
    // endpoint as a compatibility/network failover. Some managed Android
    // devices can reach only one of the two Bunny hostnames.
    var bunnyEmbedSources = [${safeModernSrc}, ${safeLegacySrc}];
    var bunnyEmbedSourceIndex = 0;
    var supportedPlaybackRates = [0.5, 0.75, 1, 1.25, 1.5, 1.75, 2];

    function isSupportedPlaybackRate(playbackRate) {
      return supportedPlaybackRates.indexOf(playbackRate) !== -1;
    }

    function readyPayloadSupportsTime(value) {
      if (!value || !Array.isArray(value.events) || !Array.isArray(value.methods)) return false;
      return value.methods.indexOf('getCurrentTime') !== -1 || value.events.indexOf('timeupdate') !== -1;
    }

    function bridgeSupportsTime(candidate) {
      if (!candidate || !candidate.isReady || typeof candidate.supports !== 'function') return false;
      try {
        return candidate.supports('method', 'getCurrentTime') || candidate.supports('event', 'timeupdate');
      } catch (e) {
        return false;
      }
    }

    function parsePlayerJsMessage(data) {
      if (typeof data === 'string') {
        try { return JSON.parse(data); } catch (e) { return null; }
      }
      return data && typeof data === 'object' ? data : null;
    }

    function isTrustedBunnyBridgeOrigin(origin) {
      return bunnyBridgeOrigins.indexOf(origin) !== -1;
    }

    function sendBunnyBridgeMessage(message) {
      if (!iframe || !iframe.contentWindow) return false;
      var serialized = JSON.stringify(message);
      var targetOrigins = bunnyBridgeOrigin ? [bunnyBridgeOrigin] : bunnyBridgeOrigins;
      var sent = false;
      targetOrigins.forEach(function (targetOrigin) {
        try {
          iframe.contentWindow.postMessage(serialized, targetOrigin);
          sent = true;
        } catch (e) {}
      });
      return sent;
    }

    function requestBunnyReadyProbe() {
      return sendBunnyBridgeMessage({
        context: 'player.js',
        version: '0.0.11',
        method: 'addEventListener',
        value: 'ready',
        listener: bridgeReadyProbeListener
      });
    }

    // Some Android WebViews normalize the iframe URL differently from the src
    // echoed by Bunny's Player.js ready event. The vendored bridge then rejects
    // an otherwise valid ready message. Validate the source window and repair
    // that specific handshake without weakening the origin boundary.
    function recoverBunnyPlayerReady(event) {
      if (__videoEmbedSuspended || !iframe || !isTrustedBunnyBridgeOrigin(event.origin) || event.source !== iframe.contentWindow || !player || player.isReady) return;
      var message = parsePlayerJsMessage(event.data);
      if (!message || message.context !== 'player.js' || message.event !== 'ready') return;
      if (!message.value || typeof message.value.src !== 'string' || !readyPayloadSupportsTime(message.value)) return;
      bunnyBridgeOrigin = event.origin;
      try {
        // Player.js derives its target from the requested iframe URL. Bunny can
        // serve the same trusted embed from its alternate iframe hostname, so
        // keep subsequent commands on the verified origin that actually
        // produced this ready event.
        player.origin = bunnyBridgeOrigin;
        player.ready(message);
      } catch (e) {}
      if (message.listener === bridgeReadyProbeListener) {
        sendBunnyBridgeMessage({
          context: 'player.js',
          version: '0.0.11',
          method: 'removeEventListener',
          value: 'ready',
          listener: bridgeReadyProbeListener
        });
      }
    }
    window.addEventListener('message', recoverBunnyPlayerReady);

    function notifyParentReady() {
      if (__videoEmbedSuspended || parentReadySent) return;
      parentReadySent = true;
      postToParent('ready', {
        duration: lastKnownDuration,
        volume: Math.round(lastKnownVolume * 100),
        isMuted: lastKnownVolume === 0,
        provider: 'bunny',
        playbackRate: lastKnownPlaybackRate
      });
    }

    function postBunnyTimeUpdate(time) {
      if (__videoEmbedSuspended) return;
      var parsedTime = Number(time);
      if (!isFinite(parsedTime)) parsedTime = 0;
      if (lastObservedTime !== null && parsedTime > lastObservedTime + 0.01) {
        advancingTimeSamples++;
        // A native Play tap can occur before Player.js flushes its queued play
        // listener on older WebViews. Consecutive clock movement repairs that
        // missed event without treating one seek sample as active playback.
        if (!isPlaying && advancingTimeSamples >= 2) {
          isPlaying = true;
          postToParent('stateChange', { state: 1, isPlaying: true, recoveredFromClock: true });
        }
      } else if (lastObservedTime !== null && parsedTime < lastObservedTime - 0.5) {
        advancingTimeSamples = 0;
      }
      lastObservedTime = parsedTime;
      postToParent('timeUpdate', {
        currentTime: parsedTime,
        duration: lastKnownDuration,
        volume: Math.round(lastKnownVolume * 100),
        isMuted: lastKnownVolume === 0,
        state: isPlaying ? 1 : 2,
        playbackRate: lastKnownPlaybackRate
      });
    }

    function suspendBunnyPlayerForInspection() {
      if (progressInterval) {
        clearInterval(progressInterval);
        progressInterval = null;
      }
      if (pollTimer) {
        clearInterval(pollTimer);
        pollTimer = null;
      }
      playerReady = false;
      parentReadySent = false;
      isPlaying = false;
      if (player && typeof player.pause === 'function') {
        try { player.pause(); } catch (e) {}
      }
      player = null;
      window.removeEventListener('message', recoverBunnyPlayerReady);
      if (iframe) {
        iframe.removeAttribute('src');
        iframe.src = 'about:blank';
        iframe.remove();
        iframe = null;
      }
    }

    ${devToolsGuard}

    if (!__videoEmbedSuspended && iframe) {
      iframe.addEventListener('load', function () {
        if (__videoEmbedSuspended || playerReady) return;
        // The Bunny document is already usable even when an older Android
        // WebView delays Player.js readiness. Let the parent uncover the
        // native player while the bridge continues connecting in background.
        postToParent('providerLoaded', { provider: 'bunny' });
        requestBunnyReadyProbe();
      });
      iframe.src = bunnyEmbedSources[bunnyEmbedSourceIndex];
    }

    function detachPlayerCallbacks(candidate) {
      if (!candidate || typeof candidate.off !== 'function') return;
      ['ready', 'timeupdate', 'playbackratechange', 'play', 'pause', 'ended', 'error'].forEach(function (eventName) {
        try { candidate.off(eventName); } catch (e) {}
      });
    }

    function retryPlayerBridgeInPlace() {
      if (__videoEmbedSuspended || !iframe || playerReady) return;
      if (progressInterval) {
        clearInterval(progressInterval);
        progressInterval = null;
      }
      var previousPlayer = player;
      player = null;
      detachPlayerCallbacks(previousPlayer);
      parentReadySent = false;
      lastObservedTime = null;
      advancingTimeSamples = 0;
      // A browser-level 404 or network failure still fires the iframe load
      // event for its internal error document. The next bounded bridge retry
      // therefore tries Bunny's other supported player generation instead of
      // navigating to the same unavailable URL again.
      if (bunnyEmbedSourceIndex + 1 < bunnyEmbedSources.length) {
        bunnyEmbedSourceIndex += 1;
        bunnyBridgeOrigin = null;
        iframe.src = bunnyEmbedSources[bunnyEmbedSourceIndex];
      }
      initPlayer();
      // Player.js queues its own subscriptions until it sees the ready event. Ask an
      // already-ready Bunny receiver to replay that handshake without
      // navigating or pausing the provider iframe. Until Bunny answers, probe
      // both of its documented embed hostnames because redirects are opaque to
      // the parent page.
      if (!requestBunnyReadyProbe()) {
        postToParent('error', { message: 'Failed to retry Bunny player bridge', provider: 'bunny' });
      }
    }

    function initPlayer() {
      if (__videoEmbedSuspended || !iframe) return;
      var activePlayer;
      try {
        if (typeof playerjs !== 'undefined' && playerjs.Player) {
          if (!playerjs.Player.prototype.setPlaybackRate) {
            playerjs.Player.prototype.setPlaybackRate = function (rate) {
              this.send({ method: 'setPlaybackRate', value: rate });
            };
          }
          if (!playerjs.Player.prototype.getPlaybackRate) {
            playerjs.Player.prototype.getPlaybackRate = function (callback) {
              this.send({ method: 'getPlaybackRate' }, callback);
            };
          }
        }
        activePlayer = new playerjs.Player(iframe);
        player = activePlayer;
      } catch (e) {
        postToParent('error', { message: 'Failed to initialize Bunny player: ' + e.message, provider: 'bunny' });
        return;
      }

      activePlayer.on('ready', function () {
        if (__videoEmbedSuspended || player !== activePlayer) return;
        if (!bridgeSupportsTime(activePlayer)) {
          postToParent('error', { message: 'Bunny player bridge cannot report playback time', provider: 'bunny' });
          return;
        }
        playerReady = true;
        // Do not gate visual readiness on metadata callbacks. Some tablet
        // WebViews never answer getVolume/getDuration even though Bunny is
        // ready and its native controls are fully usable.
        notifyParentReady();

        activePlayer.getDuration(function (dur) {
          if (__videoEmbedSuspended || player !== activePlayer) return;
          var parsedDuration = Number(dur);
          if (isFinite(parsedDuration) && parsedDuration > 0) {
            lastKnownDuration = parsedDuration;
          }
        });
        activePlayer.getVolume(function (vol) {
          if (__videoEmbedSuspended || player !== activePlayer) return;
          var parsedVolume = Number(vol);
          if (isFinite(parsedVolume)) {
            lastKnownVolume = Math.max(0, Math.min(1, parsedVolume));
          }
        });

        activePlayer.getPlaybackRate(function (rate) {
          if (__videoEmbedSuspended || player !== activePlayer) return;
          var parsedRate = Number(rate);
          if (isSupportedPlaybackRate(parsedRate)) {
            lastKnownPlaybackRate = parsedRate;
          }
        });
        activePlayer.getPaused(function (paused) {
          if (__videoEmbedSuspended || player !== activePlayer || typeof paused !== 'boolean') return;
          isPlaying = !paused;
          postToParent('stateChange', { state: paused ? 2 : 1, isPlaying: !paused, recoveredFromPlayer: true });
        });

        // Start periodic time updates
        if (progressInterval) clearInterval(progressInterval);
        progressInterval = setInterval(function () {
          if (__videoEmbedSuspended || !playerReady || player !== activePlayer) return;
          try {
            activePlayer.getCurrentTime(function (time) {
              if (__videoEmbedSuspended || player !== activePlayer) return;
              postBunnyTimeUpdate(time);
            });
            activePlayer.getPlaybackRate(function (rate) {
              var parsedRate = Number(rate);
              if (isSupportedPlaybackRate(parsedRate)) {
                lastKnownPlaybackRate = parsedRate;
              }
            });
          } catch (e) {}
        }, 1000);
      });

      activePlayer.on('timeupdate', function (value) {
        if (__videoEmbedSuspended || player !== activePlayer) return;
        var time = typeof value === 'number'
          ? value
          : Number(value && (value.seconds !== undefined ? value.seconds : value.currentTime));
        if (isFinite(time)) postBunnyTimeUpdate(time);
      });

      activePlayer.on('playbackratechange', function (value) {
        if (__videoEmbedSuspended || player !== activePlayer) return;
        var rate = Number(value && value.playbackRate !== undefined ? value.playbackRate : value);
        if (isSupportedPlaybackRate(rate)) {
          lastKnownPlaybackRate = rate;
          postToParent('playbackRateChange', { playbackRate: rate, provider: 'bunny' });
        }
      });

      activePlayer.on('play', function () {
        if (__videoEmbedSuspended || player !== activePlayer) return;
        isPlaying = true;
        advancingTimeSamples = 0;
        postToParent('stateChange', { state: 1, isPlaying: true });
      });

      activePlayer.on('pause', function () {
        if (__videoEmbedSuspended || player !== activePlayer) return;
        isPlaying = false;
        advancingTimeSamples = 0;
        postToParent('stateChange', { state: 2, isPlaying: false });
      });

      activePlayer.on('ended', function () {
        if (__videoEmbedSuspended || player !== activePlayer) return;
        isPlaying = false;
        advancingTimeSamples = 0;
        postToParent('stateChange', { state: 0, isPlaying: false });
      });

      activePlayer.on('error', function (err) {
        if (__videoEmbedSuspended || player !== activePlayer) return;
        postToParent('error', { message: err || 'Bunny playback error', provider: 'bunny' });
      });
    }

    // Wait for playerjs script to load, then init
    if (typeof playerjs !== 'undefined') {
      initPlayer();
    } else {
      // Fallback: poll for playerjs availability
      var pollCount = 0;
      pollTimer = setInterval(function () {
        if (__videoEmbedSuspended) {
          clearInterval(pollTimer);
          pollTimer = null;
          return;
        }
        pollCount++;
        if (typeof playerjs !== 'undefined') {
          clearInterval(pollTimer);
          initPlayer();
        } else if (pollCount > 100) {
          // Allow slow mobile connections before reporting a missing player library.
          clearInterval(pollTimer);
          postToParent('error', { message: 'Failed to load Bunny player library', provider: 'bunny' });
        }
      }, 250);
    }

    // ═══════════════════════════════════════════════════════
    // Listen for commands from parent SecureVideoPlayer
    // ═══════════════════════════════════════════════════════
    window.addEventListener('message', function (event) {
      if (event.origin !== window.location.origin || event.source !== window.parent) return;
      var msg = event.data;
      if (!msg || !msg.type || msg.source === 'video-embed') return;
      if (__videoEmbedSuspended) return;
      if (msg.type === 'retryBridge') {
        retryPlayerBridgeInPlace();
        return;
      }
      if (!player || !playerReady) return;
      switch (msg.type) {
        case 'play': player.play(); break;
        case 'pause': player.pause(); break;
        case 'seekTo': player.setCurrentTime(msg.time); break;
        case 'setVolume': player.setVolume(msg.volume / 100); break;
        case 'mute': player.setVolume(0); break;
        case 'unmute': player.setVolume(1); break;
        case 'setPlaybackRate': player.setPlaybackRate(msg.rate); break;
      }
    });

    // Keep the watermark fully inside the embedded player and the
    // platform-owned fullscreen surface, including after device rotation.
    var watermark = document.getElementById('video-watermark');
    function moveWatermark() {
      if (!watermark) return;
      var edge = 8;
      var maxX = Math.max(edge, window.innerWidth - watermark.offsetWidth - edge);
      var maxY = Math.max(edge, window.innerHeight - watermark.offsetHeight - edge);
      var x = edge + Math.random() * Math.max(0, maxX - edge);
      var y = edge + Math.random() * Math.max(0, maxY - edge);
      watermark.style.transform = 'translate3d(' + Math.round(x) + 'px,' + Math.round(y) + 'px,0)';
    }
    moveWatermark();
    window.addEventListener('resize', moveWatermark);
    setInterval(moveWatermark, 120000);
  </script>
</body>
</html>`;
}

function escapeHtml(value: string): string {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}



function generateYouTubeEmbedHtml(videoId: string, studentName: string, studentPhone: string): string {
  // ── Server-side: XOR-encode the video ID so it never appears as plain text ──
  const xorKey = Math.floor(Math.random() * 200) + 50;
  const encodedId = Array.from(videoId).map(c => c.charCodeAt(0) ^ xorKey);
  const watermarkBrand = JSON.stringify('Massar Academy');
  const watermarkStudentName = JSON.stringify(studentName);
  const watermarkStudentPhone = JSON.stringify(studentPhone);
  const devToolsGuard = createDevToolsSuspensionScript('suspendYouTubePlayerForInspection');

  return `<!DOCTYPE html>
<html lang="ar" dir="rtl">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <meta name="referrer" content="strict-origin-when-cross-origin">
  <title>Player</title>
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Tajawal:wght@700;900&family=Montserrat:wght@700;900&display=swap" rel="stylesheet">
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    html, body { width: 100%; height: 100%; overflow: hidden; background: #000; }
    #shell { position: relative; width: 100%; height: 100%; }
    .click-overlay {
      position: absolute; inset: 0; z-index: 10;
      background: transparent; cursor: pointer; touch-action: manipulation;
      -webkit-tap-highlight-color: transparent;
    }
  </style>
</head>
<body oncontextmenu="return false" ondragstart="return false" onselectstart="return false">
  <div id="shell"></div>
  <div class="click-overlay" id="click-overlay"></div>

  <script>
// ═══════════════════════════════════════════════════════
// LAYER 1: XOR-decode the video ID at runtime
// ═══════════════════════════════════════════════════════
var _k = ${xorKey};
var _d = [${encodedId.join(',')}];
var _vid = _d.map(function(c) { return String.fromCharCode(c ^ _k); }).join('');
var _useNativeIPhonePlayer = /iPhone|iPod/i.test(navigator.userAgent);

// ═══════════════════════════════════════════════════════
// LAYER 2: Closed Shadow DOM
// ═══════════════════════════════════════════════════════
var shell = document.getElementById('shell');
var shadow = shell.attachShadow({ mode: 'closed' });

// Trap shadowRoot access
try {
  Object.defineProperty(shell, 'shadowRoot', {
    get: function () { return null; },
    configurable: false
  });
} catch (e) { }

var wrap = document.createElement('div');
wrap.style.cssText = 'position:relative;width:100%;height:100%;background:#000;overflow:hidden';

var ytDiv = document.createElement('div');
ytDiv.id = 'yt-' + Math.random().toString(36).substr(2, 9);
ytDiv.style.cssText = 'position:absolute;top:0;left:0;width:100%;height:100%;pointer-events:none';

var watermark = document.createElement('div');
watermark.id = 'video-watermark';
watermark.style.cssText = 'position: absolute; top: 0; left: 0; z-index: 99; pointer-events: none; color: rgba(255, 255, 255, 0.18); font-size: 1.5rem; font-family: Tajawal, Montserrat, system-ui, -apple-system, BlinkMacSystemFont, sans-serif; text-shadow: 1px 1px 2px rgba(0, 0, 0, 0.5); user-select: none; transition: transform 1.5s ease-in-out; transform: translate3d(15vw, 15vh, 0); text-align: center; line-height: 1.3; white-space: pre-wrap; width: 42vw; max-width: 18rem; overflow-wrap: anywhere;';
[
  { text: ${watermarkBrand}, css: 'font-weight: 900; letter-spacing: 0.05em;' },
  { text: ${watermarkStudentName}, css: 'font-size: 0.75em; font-weight: bold; opacity: 0.85;' },
  { text: ${watermarkStudentPhone}, css: 'font-size: 0.6em; opacity: 0.75;' }
].filter(function(line) { return line.text; }).forEach(function(line, index) {
  if (index > 0) watermark.appendChild(document.createElement('br'));
  var span = document.createElement('span');
  span.style.cssText = line.css;
  span.textContent = line.text;
  watermark.appendChild(span);
});

setInterval(function() {
  if (!watermark) return;
  var topPos = Math.random() * 58 + 8;
  var leftPos = Math.random() * 52 + 4;
  watermark.style.transform = 'translate3d(' + leftPos + 'vw, ' + topPos + 'vh, 0)';
}, 120000);

wrap.appendChild(ytDiv);
wrap.appendChild(watermark);
shadow.appendChild(wrap);

var player = null;
var progressInterval = null;
var ytDivId = ytDiv.id;

var origGetById = document.getElementById.bind(document);
document.getElementById = function (id) {
  if (id === ytDivId) return ytDiv;
  return origGetById(id);
};



// ═══════════════════════════════════════════════════════
// LAYER 4: Override querySelectorAll to hide iframes
// ═══════════════════════════════════════════════════════
var _origQSA = document.querySelectorAll.bind(document);
document.querySelectorAll = function(sel) {
  var result = _origQSA(sel);
  if (typeof sel === 'string' && (sel.indexOf('iframe') !== -1 || sel === '*')) {
    return _origQSA(sel + ':not([id])');
  }
  return result;
};
var _origQS = document.querySelector.bind(document);
document.querySelector = function(sel) {
  if (typeof sel === 'string' && sel.indexOf('iframe') !== -1) return null;
  return _origQS(sel);
};
var _origGEBTN = document.getElementsByTagName.bind(document);
document.getElementsByTagName = function(tag) {
  if (tag && typeof tag === 'string' && tag.toLowerCase() === 'iframe') return document.createDocumentFragment().childNodes;
  return _origGEBTN(tag);
};

function suspendYouTubePlayerForInspection() {
  if (progressInterval) {
    clearInterval(progressInterval);
    progressInterval = null;
  }
  if (player && typeof player.pauseVideo === 'function') {
    try { player.pauseVideo(); } catch (e) {}
  }
  var currentPlayer = shadow.getElementById ? shadow.getElementById(ytDivId) : shadow.querySelector('iframe');
  if (currentPlayer) {
    currentPlayer.removeAttribute('src');
    currentPlayer.remove();
  }
  if (player && typeof player.destroy === 'function') {
    try { player.destroy(); } catch (e) {}
  }
  player = null;
}

${devToolsGuard}

if (!__videoEmbedSuspended) {
  var tag = document.createElement('script');
  tag.src = 'https://www.youtube.com/iframe_api';
  document.head.appendChild(tag);
}

function onYouTubeIframeAPIReady() {
  if (__videoEmbedSuspended || !window.YT || !YT.Player) return;
  player = new YT.Player(ytDivId, {
    videoId: _vid,  // use decoded variable, not plain string
    playerVars: {
      autoplay: _useNativeIPhonePlayer ? 0 : 1,
      controls: 0, disablekb: 1, modestbranding: 1, rel: 0, fs: 0, iv_load_policy: 3,
      // Keep playback inside the protected platform surface. Native iPhone
      // fullscreen detaches the video from the student watermark.
      playsinline: 1,
      // Explicit client identity is required by YouTube when an embed is nested in our secure player.
      origin: window.location.origin,
      widget_referrer: window.location.origin,
      start: (typeof window._lastVideoTime !== 'undefined' && window._lastVideoTime > 0) ? Math.floor(window._lastVideoTime) : 0
    },
    events: {
      onReady: function (e) {
        if (__videoEmbedSuspended) {
          try { e.target.destroy(); } catch (err) {}
          return;
        }
        document.getElementById = origGetById;
        postToParent('ready', {
          duration: e.target.getDuration(), volume: e.target.getVolume(), isMuted: e.target.isMuted(), provider: 'youtube'
        });
        if (!_useNativeIPhonePlayer) e.target.playVideo();
        startProgressUpdates();
        // Send available quality levels after a short delay (they're not available immediately)
        setTimeout(function() {
          if (__videoEmbedSuspended) return;
          try {
            var levels = e.target.getAvailableQualityLevels();
            postToParent('qualityLevels', { levels: levels, current: e.target.getPlaybackQuality() });
          } catch(err) {}
        }, 2000);
      },
      onStateChange: function (e) {
        if (__videoEmbedSuspended) return;
        var isPlayingState = e.data === YT.PlayerState.PLAYING;
        postToParent('stateChange', { state: e.data, isPlaying: isPlayingState });
      },
      onAutoplayBlocked: function () {
        if (__videoEmbedSuspended) return;
        postToParent('autoplayBlocked', { provider: 'youtube' });
      },
      onError: function (e) {
        if (__videoEmbedSuspended) return;
        postToParent('error', { code: e.data });
      }
    }
  });
}

function startProgressUpdates() {
  if (__videoEmbedSuspended) return;
  if (progressInterval) clearInterval(progressInterval);
  progressInterval = setInterval(function () {
    if (!__videoEmbedSuspended && player && typeof player.getCurrentTime === 'function') {
      postToParent('timeUpdate', {
        currentTime: player.getCurrentTime(), duration: player.getDuration(), volume: player.getVolume(),
        isMuted: player.isMuted(), state: player.getPlayerState()
      });
    }
  }, 1000);
}

window.addEventListener('message', function (event) {
  if (event.origin !== window.location.origin) return;
  if (__videoEmbedSuspended || !player) return;
  var msg = event.data;
  if (!msg || !msg.type || msg.source === 'video-embed') return;
  switch (msg.type) {
    case 'play': player.playVideo(); break;
    case 'pause': player.pauseVideo(); break;
    case 'seekTo':
      player.seekTo(msg.time, true);
      break;
    case 'setVolume': player.setVolume(msg.volume); break;
    case 'mute': player.mute(); break;
    case 'unmute': player.unMute(); break;
    case 'setPlaybackRate': player.setPlaybackRate(msg.rate); break;
    case 'setQuality':
      try {
        player.setPlaybackQuality(msg.quality);
      } catch(e) {}
      break;
    case 'getQualities':
      try {
        var levels = player.getAvailableQualityLevels();
        postToParent('qualityLevels', { levels: levels, current: player.getPlaybackQuality() });
      } catch(e) {}
      break;
  }
});

function postToParent(type, data) {
  if (typeof __videoEmbedSuspended !== 'undefined' && __videoEmbedSuspended) return;
  try { window.parent.postMessage({ source: 'video-embed', type: type, data: data }, window.location.origin); } catch (e) { }
}

document.getElementById('click-overlay').addEventListener('click', function () {
  if (!__videoEmbedSuspended && player) {
    var state = player.getPlayerState();
    if (state === YT.PlayerState.PLAYING) { player.pauseVideo(); } 
    else { player.playVideo(); }
  }
});
</script>
</body>
</html>`;
}


function generateVkEmbedHtml(oid: string, videoId: string, studentName: string, studentPhone: string): string {
  // ── Server-side: XOR-encode the VK URL so it never appears as plain text in the HTML source ──
  const vkUrl = `https://vk.com/video_ext.php?oid=${oid}&id=${videoId}&hd=2&js_api=1`;
  const xorKey = Math.floor(Math.random() * 200) + 50; // random key 50-249
  const encoded = Array.from(vkUrl).map(c => c.charCodeAt(0) ^ xorKey);
  const watermarkBrand = JSON.stringify('Massar Academy');
  const watermarkStudentName = JSON.stringify(studentName);
  const watermarkStudentPhone = JSON.stringify(studentPhone);
  const devToolsGuard = createDevToolsSuspensionScript('suspendVkPlayerForInspection');

  return `<!DOCTYPE html>
<html lang="ar" dir="rtl">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Player</title>
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Tajawal:wght@700;900&family=Montserrat:wght@700;900&display=swap" rel="stylesheet">
  <link rel="preconnect" href="https://vk.com" crossorigin>
  <link rel="dns-prefetch" href="https://vk.com">
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    html, body { width: 100%; height: 100%; overflow: hidden; background: #000; }
    #shell { position: relative; width: 100%; height: 100%; }
  </style>
</head>
<body oncontextmenu="return false" ondragstart="return false" onselectstart="return false">
  <div id="shell"></div>

  <script src="https://vk.com/js/api/videoplayer.js"></script>
  <script>
    // ═══════════════════════════════════════════════════════
    // LAYER 1: XOR-decode the URL at runtime (never plain text in source)
    // ═══════════════════════════════════════════════════════
    var _k = ${xorKey};
    var _d = [${encoded.join(',')}];
    var _u = _d.map(function(c) { return String.fromCharCode(c ^ _k); }).join('');

    var player = null;
    var progressInterval = null;
    var isPlaying = false;

    // ═══════════════════════════════════════════════════════
    // LAYER 2: Closed Shadow DOM (Elements panel protection)
    // ═══════════════════════════════════════════════════════
    var shell = document.getElementById('shell');
    var shadow = shell.attachShadow({ mode: 'closed' });

    // Trap shadowRoot access
    try {
      Object.defineProperty(shell, 'shadowRoot', {
        get: function() { return null; },
        configurable: false
      });
    } catch(e) {}

    var wrap = document.createElement('div');
    wrap.style.cssText = 'position: relative; width: 100%; height: 100%;';

    var iframe = document.createElement('iframe');
    iframe.style.cssText = 'position: absolute; top: 0; left: 0; width: 100%; height: 100%; border: none; pointer-events: none;';
    iframe.setAttribute('allow', 'autoplay; encrypted-media; fullscreen; picture-in-picture');
    iframe.setAttribute('frameborder', '0');
    iframe.setAttribute('allowfullscreen', '');



    var clickOverlay = document.createElement('div');
    clickOverlay.id = 'click-overlay';
    clickOverlay.style.cssText = 'position: absolute; inset: 0; z-index: 2147483647; background: transparent; cursor: pointer;';

    var watermark = document.createElement('div');
    watermark.id = 'video-watermark';
    watermark.style.cssText = 'position: absolute; top: 0; left: 0; z-index: 2147483646; pointer-events: none; color: rgba(255, 255, 255, 0.18); font-size: 1.5rem; font-family: Tajawal, Montserrat, system-ui, -apple-system, BlinkMacSystemFont, sans-serif; text-shadow: 1px 1px 2px rgba(0, 0, 0, 0.5); user-select: none; transition: transform 1.5s ease-in-out; transform: translate3d(15vw, 15vh, 0); text-align: center; line-height: 1.3; white-space: pre-wrap; width: 42vw; max-width: 18rem; overflow-wrap: anywhere;';
    [
      { text: ${watermarkBrand}, css: 'font-weight: 900; letter-spacing: 0.05em;' },
      { text: ${watermarkStudentName}, css: 'font-size: 0.75em; font-weight: bold; opacity: 0.85;' },
      { text: ${watermarkStudentPhone}, css: 'font-size: 0.6em; opacity: 0.75;' }
    ].filter(function(line) { return line.text; }).forEach(function(line, index) {
      if (index > 0) watermark.appendChild(document.createElement('br'));
      var span = document.createElement('span');
      span.style.cssText = line.css;
      span.textContent = line.text;
      watermark.appendChild(span);
    });

    wrap.appendChild(iframe);
    wrap.appendChild(clickOverlay);
    wrap.appendChild(watermark);
    shadow.appendChild(wrap);

    // ═══════════════════════════════════════════════════════
    // LAYER 4: Override querySelectorAll to hide iframes
    // ═══════════════════════════════════════════════════════
    var _origQSA = document.querySelectorAll.bind(document);
    document.querySelectorAll = function(sel) {
      var result = _origQSA(sel);
      if (typeof sel === 'string' && (sel.indexOf('iframe') !== -1 || sel === '*')) {
        return _origQSA(sel + ':not([id])');  
      }
      return result;
    };
    var _origQS = document.querySelector.bind(document);
    document.querySelector = function(sel) {
      if (typeof sel === 'string' && sel.indexOf('iframe') !== -1) return null;
      return _origQS(sel);
    };
    var _origGEBTN = document.getElementsByTagName.bind(document);
    document.getElementsByTagName = function(tag) {
      if (tag && typeof tag === 'string' && tag.toLowerCase() === 'iframe') return document.createDocumentFragment().childNodes;
      return _origGEBTN(tag);
    };

    function suspendVkPlayerForInspection() {
      if (progressInterval) {
        clearInterval(progressInterval);
        progressInterval = null;
      }
      if (player && typeof player.pause === 'function') {
        try { player.pause(); } catch (e) {}
      }
      if (iframe) {
        iframe.removeAttribute('src');
        iframe.src = 'about:blank';
        iframe.remove();
        iframe = null;
      }
      isPlaying = false;
      player = null;
    }

    ${devToolsGuard}

    if (!__videoEmbedSuspended && iframe) {
      iframe.src = _u;
    }

    // Watermark roaming
    setInterval(function() {
      if (!watermark) return;
      var topPos = Math.random() * 58 + 8;
      var leftPos = Math.random() * 52 + 4;
      watermark.style.transform = 'translate3d(' + leftPos + 'vw, ' + topPos + 'vh, 0)';
    }, 120000);

    function postToParent(type, data) {
      if (typeof __videoEmbedSuspended !== 'undefined' && __videoEmbedSuspended) return;
      try { window.parent.postMessage({ source: 'video-embed', type: type, data: data }, window.location.origin); } catch (e) { }
    }

    var initTimeout = setTimeout(function() {
      if (__videoEmbedSuspended) return;
      if (!player && typeof VK === 'undefined') {
        postToParent('error', { code: 'VK_INIT_FAILED' });
      }
    }, 10000);

    var checkVK = setInterval(function() {
      if (__videoEmbedSuspended) {
        clearInterval(checkVK);
        clearTimeout(initTimeout);
        return;
      }
      if (typeof VK !== 'undefined' && VK.VideoPlayer) {
        clearInterval(checkVK);
        clearTimeout(initTimeout);
        initPlayer();
      }
    }, 100);

    var _lastVideoTimeVK = 0;
    function initPlayer() {
      if (__videoEmbedSuspended || !iframe) return;
      try {
        player = VK.VideoPlayer(iframe);

        player.on('inited', function() {
          if (__videoEmbedSuspended || !player) return;
          var vol = 100;
          var muted = false;
          try {
             if (typeof player.getVolume === 'function') vol = player.getVolume() * 100;
             if (typeof player.isMuted === 'function') muted = player.isMuted();
          } catch(e) {}
          
          // Discover all available methods on the VK player object
          var methods = [];
          for (var key in player) {
            try { methods.push(key + ':' + typeof player[key]); } catch(e) {}
          }
          
          postToParent('ready', { duration: 0, volume: vol, isMuted: muted, provider: 'vk', vkMethods: methods });
          if (typeof _lastVideoTimeVK !== 'undefined' && _lastVideoTimeVK > 0) {
            try { player.seek(_lastVideoTimeVK); } catch(e) {}
          }
          player.play();
        });

        player.on('timeupdate', function(e) {
          if (__videoEmbedSuspended) return;
          _lastVideoTimeVK = e.time || 0;
          postToParent('timeUpdate', { currentTime: e.time || 0, duration: e.duration || 0 });
        });

        player.on('started', function() {
          if (__videoEmbedSuspended) return;
          isPlaying = true;
          postToParent('stateChange', { isPlaying: true });
        });

        player.on('resumed', function() {
          if (__videoEmbedSuspended) return;
          isPlaying = true;
          postToParent('stateChange', { isPlaying: true });
        });

        player.on('paused', function() {
          if (__videoEmbedSuspended) return;
          isPlaying = false;
          postToParent('stateChange', { isPlaying: false });
        });

        player.on('ended', function() {
          if (__videoEmbedSuspended) return;
          isPlaying = false;
          postToParent('stateChange', { isPlaying: false });
        });

        player.on('error', function() {
          if (__videoEmbedSuspended) return;
          postToParent('error', { code: 'VK_PLAYBACK_ERROR' });
        });

      } catch (e) {
        postToParent('error', { code: 'VK_INIT_FAILED' });
      }
    }

    window.addEventListener('message', function (event) {
      if (event.origin !== window.location.origin) return;
      if (__videoEmbedSuspended || !player) return;
      var msg = event.data;
      if (!msg || !msg.type || msg.source === 'video-embed') return;
      switch (msg.type) {
        case 'play': player.play(); break;
        case 'pause': player.pause(); break;
        case 'seekTo': player.seek(msg.time !== undefined ? msg.time : msg.seconds); break;
        case 'setVolume': player.setVolume(msg.volume / 100); break;
        case 'mute': player.mute(); break;
        case 'unmute': player.unmute(); break;
        case 'setPlaybackRate':
          var rate = msg.rate || 1;
          // Strategy 1: Try the SDK method (may exist undocumented)
          try { if (typeof player.setPlaybackRate === 'function') { player.setPlaybackRate(rate); } } catch(e) {}
          // Strategy 2: Try to find the <video> element inside the VK iframe
          try {
            var vkIframeDoc = iframe.contentDocument || iframe.contentWindow.document;
            var videoEl = vkIframeDoc.querySelector('video');
            if (videoEl) { videoEl.playbackRate = rate; }
          } catch(e) {}
          // Strategy 3: postMessage to the VK iframe (internal VK command format)
          try {
            if (iframe.contentWindow) {
              iframe.contentWindow.postMessage({ action: 'setPlaybackRate', value: rate }, 'https://vk.com');
              iframe.contentWindow.postMessage({ type: 'player:setPlaybackRate', rate: rate }, 'https://vk.com');
            }
          } catch(e) {}
          break;
      }
    });

    clickOverlay.addEventListener('click', function () {
      if (!__videoEmbedSuspended && player) {
         if (isPlaying) { player.pause(); } else { player.play(); }
      }
    });
  </script>
</body>
</html>`;
}
