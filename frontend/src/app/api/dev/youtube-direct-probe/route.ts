import { readFile } from 'node:fs/promises';

const responseTotals: Record<string, { requests: number; bodyBytes: number }> = {};
const qualityKeys = ['144', '240', '360', '480', '720', '1080'];

function measuredResponse(resource: string, body: string, headers: HeadersInit) {
  const total = responseTotals[resource] ?? { requests: 0, bodyBytes: 0 };
  total.requests += 1;
  total.bodyBytes += Buffer.byteLength(body, 'utf8');
  responseTotals[resource] = total;
  return new Response(body, { headers });
}

// Temporary, local-only research page. Media is fetched by the browser from Google.
async function serveProbe(request: Request) {
  const url = new URL(request.url);
  if (process.env.NODE_ENV !== 'development'
    || !/^(localhost|127\.0\.0\.1|\[::1\])(?::\d+)?$/i.test(request.headers.get('host') ?? '')
    || !['localhost', '127.0.0.1', '[::1]'].includes(url.hostname)) {
    return new Response(null, { status: 404 });
  }
  if (url.searchParams.get('stats') === '1') {
    return Response.json({
      measurement: 'Successful response bodies only; excludes HTTP headers and extraction requests.',
      resources: responseTotals,
      totalBodyBytes: Object.values(responseTotals).reduce((sum, item) => sum + item.bodyBytes, 0),
    }, { headers: { 'Cache-Control': 'no-store' } });
  }
  const native = url.searchParams.get('native') === '1';
  const manifest = JSON.parse(await readFile(native
    ? '/tmp/codex-youtube-unlisted-native-filtered.json'
    : '/tmp/codex-youtube-unlisted-hls.json', 'utf8'));
  if (url.searchParams.has('playlist')) {
    const key = url.searchParams.get('playlist') ?? '';
    if (!['master', 'audio', ...qualityKeys].includes(key)) return new Response(null, { status: 404 });
    let playlist = manifest.playlists[key];
    if (typeof playlist !== 'string') return new Response(null, { status: 404 });
    const quality = url.searchParams.get('quality');
    if (native && key === 'master' && quality && qualityKeys.includes(quality)) {
      playlist = manifest.playlists[quality];
      if (typeof playlist !== 'string') return new Response(null, { status: 404 });
    } else if (!native && key === 'master' && quality && qualityKeys.includes(quality)) {
      const sections = playlist.split('#EXT-X-STREAM-INF:');
      const selected = sections.slice(1).find((section: string) => section.includes('\n?playlist=' + quality + '\n'));
      if (!selected) return new Response(null, { status: 404 });
      playlist = sections[0] + '#EXT-X-STREAM-INF:' + selected;
    }
    return measuredResponse((native ? 'native-' : '') + key, playlist, {
      'Content-Type': 'application/vnd.apple.mpegurl', 'Cache-Control': 'no-store',
    });
  }
  const qualityOptions = qualityKeys.filter(key => typeof manifest.playlists[key] === 'string')
    .map(key => '<option value="' + key + '">' + key + 'p</option>').join('');
  return measuredResponse(native ? 'native-page' : 'page', `<!doctype html><html lang="ar" dir="rtl"><head>
<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>اختبار HLS مباشر</title>
<style>body{background:#101819;color:#fff;font:18px system-ui;padding:24px;margin:auto;max-width:850px}video{width:100%;aspect-ratio:16/9;background:#000}button,select{font:inherit;margin:8px;padding:10px}pre{white-space:pre-wrap;direction:ltr;text-align:left;background:#1d292b;padding:16px}</style>
</head><body><h1>اختبار HLS مباشر من Google</h1><p>${native ? 'سيرفرنا يقدّم قائمة الجودات فقط. قوائم الأجزاء والفيديو والصوت تأتي مباشرة من Google.' : 'سيرفرنا يقدّم قوائم التشغيل. متصفحك يحمّل أجزاء الفيديو والصوت مباشرة من Google.'}</p><p>تجربة مؤقتة للفيديو الذي أرسلته. الاختبار على Safari للكمبيوتر؛ لم يُختبر على آيفون أو شبكة مختلفة. روابط الفيديو تنتهي صلاحيتها.</p>
<video id="video" playsinline preload="metadata" controls></video>
<div><button id="fragments">تشغيل HLS على Safari</button><select id="quality" aria-label="الجودة" disabled><option value="-1">تلقائي</option>${qualityOptions}</select>
<button id="middle">الانتقال للمنتصف</button><button id="end">قرب النهاية</button><button id="stop">إيقاف</button></div>
<pre id="status">جاهز للاختبار</pre>
<script>
const video = document.getElementById('video');
const output = document.getElementById('status');
const select = document.getElementById('quality');
const masterSource = '${native ? '?native=1&playlist=master' : '?playlist=master'}';
let failure = '';
let pendingSeek = null;
function play() { video.play().catch(error => { failure = error.name; }); }
function load(source, time = null, resume = true) {
  failure = '';
  pendingSeek = time;
  video.src = source;
  if (resume) play();
}
video.addEventListener('loadedmetadata', () => {
  if (pendingSeek !== null) { video.currentTime = pendingSeek; pendingSeek = null; }
});
document.getElementById('fragments').onclick = () => {
  select.disabled = false;
  select.value = '-1';
  load(masterSource);
};
select.onchange = () => {
  load(masterSource + '&quality=' + select.value, pendingSeek ?? video.currentTime, !video.paused);
};
document.getElementById('middle').onclick = () => {
  if (Number.isFinite(video.duration)) { video.currentTime = video.duration / 2; play(); }
};
document.getElementById('end').onclick = () => {
  if (Number.isFinite(video.duration)) { video.currentTime = Math.max(0, video.duration - 30); play(); }
};
document.getElementById('stop').onclick = () => {
  video.pause(); video.removeAttribute('src'); video.load(); pendingSeek = null; select.disabled = true;
};
setInterval(() => {
  output.textContent = JSON.stringify({ time: video.currentTime, duration: video.duration,
    width: video.videoWidth, height: video.videoHeight, paused: video.paused,
    ready: video.readyState, error: video.error?.code ?? null, network: failure }, null, 2);
}, 500);
</script></body></html>`, {
    'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store',
    'Content-Security-Policy': "default-src 'none'; script-src 'self' 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self' https://*.googlevideo.com; media-src 'self' https://*.googlevideo.com blob:; frame-ancestors 'self'",
  });
}

export async function GET(request: Request) {
  try {
    return await serveProbe(request);
  } catch {
    return new Response('بيانات التجربة المؤقتة غير متاحة.', { status: 503, headers: { 'Cache-Control': 'no-store' } });
  }
}
