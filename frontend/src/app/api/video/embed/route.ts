import { NextRequest, NextResponse } from 'next/server';
import crypto from 'crypto';

/**
 * GET /api/video/embed?t=<encryptedToken>&k=<base64Key>
 * 
 * Decrypts the video session token SERVER-SIDE and returns an HTML page
 * with the player embedded.
 */
export async function GET(request: NextRequest) {
  try {
    // Prevent direct loading of the iframe (copying the iframe URL)
    const dest = request.headers.get('sec-fetch-dest');
    const referer = request.headers.get('referer');
    const host = request.headers.get('host');

    if (dest === 'document' && !referer) {
      return new NextResponse('Embed must be loaded within the academy platform', { status: 403 });
    }

    if (referer && host && !referer.includes(host)) {
      return new NextResponse('Unauthorized embedding', { status: 403 });
    }

    const { searchParams } = new URL(request.url);
    const encryptedToken = searchParams.get('t');
    const base64Key = searchParams.get('k');

    if (!encryptedToken || !base64Key) {
      return new NextResponse('Missing parameters', { status: 400 });
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
    const studentName = parsed.StudentName || 'Nader George Academy';
    const studentPhone = parsed.StudentPhone || '';

    let html = '';
    if (provider === 'vk') {
      // T004 & T021: Parse ProviderVideoId
      const match = videoId.match(/oid=([^&]+)&id=([^&]+)/);
      if (!match) {
        return new NextResponse('Invalid VK video identifier format. Expected: oid=-XXXXX&id=XXXXX', { status: 400 });
      }
      const oid = match[1];
      const id = match[2];
      html = generateVkEmbedHtml(oid, id, studentName, studentPhone);
    } else {
      html = generateYouTubeEmbedHtml(videoId, studentName, studentPhone);
    }

    return new NextResponse(html, {
      status: 200,
      headers: {
        'Content-Type': 'text/html; charset=utf-8',
        'Cache-Control': 'no-store, no-cache, must-revalidate, private',
        'X-Content-Type-Options': 'nosniff',
        'X-Frame-Options': 'SAMEORIGIN',
        'Content-Security-Policy': "frame-ancestors 'self'",
      },
    });
  } catch (error) {
    console.error('[video-embed] Decryption failed:', error);
    return new NextResponse('Session expired or invalid', { status: 403 });
  }
}



function generateYouTubeEmbedHtml(videoId: string, studentName: string, studentPhone: string): string {
  // ── Server-side: XOR-encode the video ID so it never appears as plain text ──
  const xorKey = Math.floor(Math.random() * 200) + 50;
  const encodedId = Array.from(videoId).map(c => c.charCodeAt(0) ^ xorKey);
  const watermarkBrand = JSON.stringify('Nader George Academy');
  const watermarkStudentName = JSON.stringify(studentName);
  const watermarkStudentPhone = JSON.stringify(studentPhone);

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
  <div id="shell"></div>
  <div class="click-overlay" id="click-overlay"></div>

  <script>
// ═══════════════════════════════════════════════════════
// LAYER 1: XOR-decode the video ID at runtime
// ═══════════════════════════════════════════════════════
var _k = ${xorKey};
var _d = [${encodedId.join(',')}];
var _vid = _d.map(function(c) { return String.fromCharCode(c ^ _k); }).join('');

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

var coverTop = document.createElement('div');
coverTop.style.cssText = 'position:absolute;top:0;left:0;right:0;height:80px;background:linear-gradient(to bottom,rgba(0,0,0,1) 40%,rgba(0,0,0,0) 100%);pointer-events:none;z-index:5';
var coverShadow = document.createElement('div');
coverShadow.style.cssText = 'position:absolute;inset:0;box-shadow:inset 0 0 40px 10px #000;pointer-events:none;z-index:5';

var watermark = document.createElement('div');
watermark.id = 'video-watermark';
watermark.style.cssText = 'position: absolute; top: 15%; left: 15%; z-index: 99; pointer-events: none; color: rgba(255, 255, 255, 0.18); font-size: 1.25rem; font-family: system-ui, -apple-system, BlinkMacSystemFont, Arial, sans-serif; text-shadow: 1px 1px 2px rgba(0, 0, 0, 0.5); user-select: none; transition: top 1.5s ease-in-out, left 1.5s ease-in-out; text-align: center; line-height: 1.3; white-space: pre-wrap;';
[
  { text: ${watermarkBrand}, css: 'font-weight: 900; letter-spacing: 0.05em;' },
  { text: ${watermarkStudentName}, css: 'font-size: 0.85em; font-weight: bold; opacity: 0.85;' },
  { text: ${watermarkStudentPhone}, css: 'font-size: 0.8em; opacity: 0.75;' }
].filter(function(line) { return line.text; }).forEach(function(line, index) {
  if (index > 0) watermark.appendChild(document.createElement('br'));
  var span = document.createElement('span');
  span.style.cssText = line.css;
  span.textContent = line.text;
  watermark.appendChild(span);
});

setInterval(function() {
  if (!watermark) return;
  var topPos = Math.random() * 80 + 10;
  var leftPos = Math.random() * 80 + 10;
  watermark.style.top = topPos + '%';
  watermark.style.left = leftPos + '%';
}, 12000);

wrap.appendChild(ytDiv);
wrap.appendChild(coverTop);
wrap.appendChild(coverShadow);
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
  if (sel && (sel.indexOf('iframe') !== -1 || sel === '*')) {
    return _origQSA(sel + ':not([id])');
  }
  return result;
};
var _origQS = document.querySelector.bind(document);
document.querySelector = function(sel) {
  if (sel && sel.indexOf('iframe') !== -1) return null;
  return _origQS(sel);
};
var _origGEBTN = document.getElementsByTagName.bind(document);
document.getElementsByTagName = function(tag) {
  if (tag.toLowerCase() === 'iframe') return document.createDocumentFragment().childNodes;
  return _origGEBTN(tag);
};

// ═══════════════════════════════════════════════════════
// LAYER 5: DevTools detection — pause video if DevTools opens
// ═══════════════════════════════════════════════════════
var _devtoolsOpen = false;
setInterval(function() {
  var isOpen = false;
  try {
    var topW = window.top || window;
    var widthThreshold = topW.outerWidth - topW.innerWidth > 160;
    var heightThreshold = topW.outerHeight - topW.innerHeight > 160;
    isOpen = widthThreshold || heightThreshold;
  } catch(e) { }
  if (isOpen && !_devtoolsOpen) {
    _devtoolsOpen = true;
    if (player && typeof player.pauseVideo === 'function') {
      if (typeof player.getCurrentTime === 'function') {
        window._lastVideoTime = player.getCurrentTime();
      }
      try { player.pauseVideo(); } catch(e) {}
      postToParent('stateChange', { state: 2, isPlaying: false });
    }
    // WIPE DOM: Remove the current iframe & destroy player instance
    var currentIframe = shadow.getElementById ? shadow.getElementById(ytDivId) : shadow.querySelector('iframe');
    if (currentIframe) { currentIframe.remove(); }
    if (player && player.destroy) { try { player.destroy(); } catch(e) {} player = null; }
  } else if (!isOpen && _devtoolsOpen) {
    _devtoolsOpen = false;
    // RESTORE DOM: Recreate target container and API bindings
    var newYtDiv = document.createElement('div');
    newYtDiv.id = ytDivId;
    newYtDiv.style.cssText = 'position:absolute;top:0;left:0;width:100%;height:100%;pointer-events:none';
    wrap.insertBefore(newYtDiv, coverTop);
    
    document.getElementById = function (id) {
      if (id === ytDivId) return newYtDiv;
      return origGetById(id);
    };
    
    if (typeof YT !== 'undefined' && YT.Player) {
      onYouTubeIframeAPIReady();
    }
  }
}, 500);

var tag = document.createElement('script');
tag.src = 'https://www.youtube.com/iframe_api';
document.head.appendChild(tag);

function onYouTubeIframeAPIReady() {
  player = new YT.Player(ytDivId, {
    videoId: _vid,  // use decoded variable, not plain string
    playerVars: {
      autoplay: 1, controls: 0, disablekb: 1, modestbranding: 1, rel: 0, fs: 0, iv_load_policy: 3, playsinline: 1,
      start: (typeof window._lastVideoTime !== 'undefined' && window._lastVideoTime > 0) ? Math.floor(window._lastVideoTime) : 0
    },
    events: {
      onReady: function (e) {
        document.getElementById = origGetById;
        postToParent('ready', {
          duration: e.target.getDuration(), volume: e.target.getVolume(), isMuted: e.target.isMuted(), provider: 'youtube'
        });
        e.target.playVideo();
        startProgressUpdates();
        // Send available quality levels after a short delay (they're not available immediately)
        setTimeout(function() {
          try {
            var levels = e.target.getAvailableQualityLevels();
            postToParent('qualityLevels', { levels: levels, current: e.target.getPlaybackQuality() });
          } catch(err) {}
        }, 2000);
      },
      onStateChange: function (e) {
        postToParent('stateChange', { state: e.data, isPlaying: e.data === YT.PlayerState.PLAYING });
      },
      onError: function (e) {
        postToParent('error', { code: e.data });
      }
    }
  });
}

function startProgressUpdates() {
  if (progressInterval) clearInterval(progressInterval);
  progressInterval = setInterval(function () {
    if (player && typeof player.getCurrentTime === 'function') {
      postToParent('timeUpdate', {
        currentTime: player.getCurrentTime(), duration: player.getDuration(), volume: player.getVolume(),
        isMuted: player.isMuted(), state: player.getPlayerState()
      });
    }
  }, 500);
}

window.addEventListener('message', function (event) {
  if (!player) return;
  var msg = event.data;
  if (!msg || !msg.type || msg.source === 'video-embed') return;
  switch (msg.type) {
    case 'play': player.playVideo(); break;
    case 'pause': player.pauseVideo(); break;
    case 'seekTo': player.seekTo(msg.time, true); break;
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
  try { window.parent.postMessage({ source: 'video-embed', type: type, data: data }, '*'); } catch (e) { }
}

document.getElementById('click-overlay').addEventListener('click', function () {
  if (player) {
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
  const watermarkBrand = JSON.stringify('Nader George Academy');
  const watermarkStudentName = JSON.stringify(studentName);
  const watermarkStudentPhone = JSON.stringify(studentPhone);

  return `<!DOCTYPE html>
<html lang="ar" dir="rtl">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Player</title>
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
    iframe.src = _u;   // set src from decoded variable
    iframe.style.cssText = 'position: absolute; top: 0; left: 0; width: 100%; height: 100%; border: none; pointer-events: none;';
    iframe.setAttribute('allow', 'autoplay; encrypted-media; fullscreen; picture-in-picture');
    iframe.setAttribute('frameborder', '0');
    iframe.setAttribute('allowfullscreen', '');



    var clickOverlay = document.createElement('div');
    clickOverlay.id = 'click-overlay';
    clickOverlay.style.cssText = 'position: absolute; inset: 0; z-index: 2147483647; background: transparent; cursor: pointer;';

    var watermark = document.createElement('div');
    watermark.id = 'video-watermark';
    watermark.style.cssText = 'position: absolute; top: 15%; left: 15%; z-index: 2147483646; pointer-events: none; color: rgba(255, 255, 255, 0.18); font-size: 1.25rem; font-family: system-ui, -apple-system, BlinkMacSystemFont, Arial, sans-serif; text-shadow: 1px 1px 2px rgba(0, 0, 0, 0.5); user-select: none; transition: top 1.5s ease-in-out, left 1.5s ease-in-out; text-align: center; line-height: 1.3; white-space: pre-wrap;';
    [
      { text: ${watermarkBrand}, css: 'font-weight: 900; letter-spacing: 0.05em;' },
      { text: ${watermarkStudentName}, css: 'font-size: 0.85em; font-weight: bold; opacity: 0.85;' },
      { text: ${watermarkStudentPhone}, css: 'font-size: 0.8em; opacity: 0.75;' }
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
      if (sel && (sel.indexOf('iframe') !== -1 || sel === '*')) {
        return _origQSA(sel + ':not([id])');  
      }
      return result;
    };
    var _origQS = document.querySelector.bind(document);
    document.querySelector = function(sel) {
      if (sel && sel.indexOf('iframe') !== -1) return null;
      return _origQS(sel);
    };
    var _origGEBTN = document.getElementsByTagName.bind(document);
    document.getElementsByTagName = function(tag) {
      if (tag.toLowerCase() === 'iframe') return document.createDocumentFragment().childNodes;
      return _origGEBTN(tag);
    };

    // ═══════════════════════════════════════════════════════
    // LAYER 5: DevTools detection — pause video if DevTools opens
    // ═══════════════════════════════════════════════════════
    var _devtoolsOpen = false;
    setInterval(function() {
      var isOpen = false;
      try {
        var topW = window.top || window;
        var widthThreshold = topW.outerWidth - topW.innerWidth > 160;
        var heightThreshold = topW.outerHeight - topW.innerHeight > 160;
        isOpen = widthThreshold || heightThreshold;
      } catch(e) { }
      if (isOpen && !_devtoolsOpen) {
        _devtoolsOpen = true;
        if (player && isPlaying) {
          try { player.pause(); } catch(e) {}
          postToParent('stateChange', { isPlaying: false });
        }
        // WIPE DOM: Remove VK iframe completely
        if (iframe && iframe.parentNode) {
          iframe.remove();
        }
      } else if (!isOpen && _devtoolsOpen) {
        _devtoolsOpen = false;
        // RESTORE DOM: Recreate iframe and append before clickOverlay
        iframe = document.createElement('iframe');
        iframe.src = _u;
        iframe.style.cssText = 'position: absolute; top: 0; left: 0; width: 100%; height: 100%; border: none; pointer-events: none;';
        iframe.setAttribute('allow', 'autoplay; encrypted-media; fullscreen; picture-in-picture');
        iframe.setAttribute('frameborder', '0');
        iframe.setAttribute('allowfullscreen', '');
        wrap.insertBefore(iframe, clickOverlay);
        
        if (typeof VK !== 'undefined') {
          initPlayer();
        }
      }
    }, 500);

    // Watermark roaming
    setInterval(function() {
      if (!watermark) return;
      var topPos = Math.random() * 80 + 10;
      var leftPos = Math.random() * 80 + 10;
      watermark.style.top = topPos + '%';
      watermark.style.left = leftPos + '%';
    }, 12000);

    function postToParent(type, data) {
      try { window.parent.postMessage({ source: 'video-embed', type: type, data: data }, '*'); } catch (e) { }
    }

    var initTimeout = setTimeout(function() {
      if (!player && typeof VK === 'undefined') {
        postToParent('error', { code: 'VK_INIT_FAILED' });
      }
    }, 10000);

    var checkVK = setInterval(function() {
      if (typeof VK !== 'undefined' && VK.VideoPlayer) {
        clearInterval(checkVK);
        clearTimeout(initTimeout);
        initPlayer();
      }
    }, 100);

    var _lastVideoTimeVK = 0;
    function initPlayer() {
      try {
        player = VK.VideoPlayer(iframe);

        player.on('inited', function() {
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
          _lastVideoTimeVK = e.time || 0;
          postToParent('timeUpdate', { currentTime: e.time || 0, duration: e.duration || 0 });
        });

        player.on('started', function() {
          isPlaying = true;
          postToParent('stateChange', { isPlaying: true });
        });

        player.on('resumed', function() {
          isPlaying = true;
          postToParent('stateChange', { isPlaying: true });
        });

        player.on('paused', function() {
          isPlaying = false;
          postToParent('stateChange', { isPlaying: false });
        });

        player.on('ended', function() {
          isPlaying = false;
          postToParent('stateChange', { isPlaying: false });
        });

        player.on('error', function() {
          postToParent('error', { code: 'VK_PLAYBACK_ERROR' });
        });

      } catch (e) {
        postToParent('error', { code: 'VK_INIT_FAILED' });
      }
    }

    window.addEventListener('message', function (event) {
      if (!player) return;
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
              iframe.contentWindow.postMessage({ action: 'setPlaybackRate', value: rate }, '*');
              iframe.contentWindow.postMessage({ type: 'player:setPlaybackRate', rate: rate }, '*');
            }
          } catch(e) {}
          break;
      }
    });

    clickOverlay.addEventListener('click', function () {
      if (player) {
         if (isPlaying) { player.pause(); } else { player.play(); }
      }
    });
  </script>
</body>
</html>`;
}
