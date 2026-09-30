function isLocalDevelopment(request: Request) {
  return process.env.NODE_ENV === 'development'
    && /^(localhost|127\.0\.0\.1|\[::1\])(?::\d+)?$/i.test(request.headers.get('host') ?? '')
    && ['localhost', '127.0.0.1', '[::1]'].includes(new URL(request.url).hostname);
}

// Retain a local notice for existing demo tabs. The rejected bandwidth relay is removed.
export function GET(request: Request) {
  if (!isLocalDevelopment(request)) return new Response(null, { status: 404 });
  return new Response(`<!doctype html>
<html lang="ar" dir="rtl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>تم إيقاف تجربة تمرير الفيديو</title>
<style>html{color-scheme:dark}body{margin:0;background:#090c0d;color:#fff;font:18px/1.8 system-ui,sans-serif;min-height:100dvh;display:grid;place-items:center}main{max-width:36rem;padding:32px}h1{font-size:24px}p{color:#b6c9cd}</style>
</head><body><main><h1>تم إيقاف تجربة تمرير الفيديو</h1><p>أُزيل مسار بث الفيديو عبر السيرفر لتجنّب استهلاك باندويث الفيديو. هذه الصفحة لا تحمّل أي فيديو.</p></main></body></html>`, {
    status: 410,
    headers: {
      'Content-Type': 'text/html; charset=utf-8',
      'Cache-Control': 'no-store',
      'Content-Security-Policy': "default-src 'none'; style-src 'unsafe-inline'; frame-ancestors 'self'",
    },
  });
}

export function POST(request: Request) {
  return new Response(null, { status: isLocalDevelopment(request) ? 410 : 404 });
}

export function OPTIONS(request: Request) {
  return new Response(null, { status: isLocalDevelopment(request) ? 410 : 404 });
}
