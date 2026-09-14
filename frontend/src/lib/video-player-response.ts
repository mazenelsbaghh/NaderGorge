export function videoPlayerResponse(html: string, status = 200) {
  return new Response(html, { status, headers: {
    'Content-Type': 'text/html; charset=utf-8',
    'Cache-Control': 'no-store, no-cache, must-revalidate, private',
    'X-Content-Type-Options': 'nosniff',
    'X-Frame-Options': 'SAMEORIGIN',
    'Content-Security-Policy': "frame-ancestors 'self'",
    'Permissions-Policy': 'display-capture=(), picture-in-picture=()',
    // YouTube's nested iframe needs the application origin as its client identity.
    'Referrer-Policy': 'strict-origin-when-cross-origin',
  } });
}

export function videoBootstrapHtml(sessionId: string) {
  const endpoint = JSON.stringify(`/api/video/material?s=${encodeURIComponent(sessionId)}`);
  return `<!DOCTYPE html><html lang="ar" dir="rtl"><head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
<body style="margin:0;background:#000;color:#fff"><script>
(async function(){
  try {
    var response=await fetch(${endpoint},{credentials:'same-origin',cache:'no-store'});
    if(!response.ok)throw new Error(String(response.status));
    var html=await response.text();
    document.open();document.write(html);document.close();
  } catch(error) {
    var status=Number(error.message)||0;
    window.parent.postMessage({source:'video-embed',type:'bootstrapError',data:{status:status}},window.location.origin);
  }
})();
</script></body></html>`;
}
