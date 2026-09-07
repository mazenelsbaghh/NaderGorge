const MAX_PLAYLIST_BYTES = 1024 * 1024;
const MAX_SEGMENT_BYTES = 32 * 1024 * 1024;
const MEDIA_TYPES: Record<string, string> = { ts: 'video/mp2t', mp4: 'video/mp4', m4s: 'video/mp4', aac: 'audio/aac' };

export function bunnyHlsRoot(signedPlaylist: string): URL {
  const source = new URL(signedPlaylist);
  if (source.protocol !== 'https:' || !/^[a-z0-9-]+\.b-cdn\.net$/i.test(source.hostname)
    || source.port || source.username || source.password || source.search || source.hash
    || !/^\/bcdn_token=[A-Za-z0-9_-]+&expires=\d+&token_path=%2F[0-9a-f-]+%2F\/[0-9a-f-]{36}\/playlist\.m3u8$/i.test(source.pathname)) {
    throw new Error('Invalid HLS scope');
  }
  return new URL('./', source);
}

export function bunnyHlsResource(root: URL, resource: string): URL {
  if (!/^[A-Za-z0-9_./-]+\.(m3u8|ts|m4s|mp4|aac|key|bin)$/i.test(resource)
    || resource.startsWith('/') || resource.split('/').some(segment => segment === '..' || segment === '.')) {
    throw new Error('Invalid HLS resource');
  }
  const resolved = new URL(resource, root);
  if (!resolved.href.startsWith(root.href)) throw new Error('Resource outside HLS scope');
  return resolved;
}

export function rewriteBunnyPlaylist(text: string, upstream: URL, sessionId: string, root: URL): string {
  if (!text.trimStart().startsWith('#EXTM3U')) throw new Error('Invalid HLS playlist');
  const localUri = (uri: string) => {
    const resolved = new URL(uri, upstream);
    if (!resolved.href.startsWith(root.href)) throw new Error('Playlist URI outside HLS scope');
    const resource = resolved.href.slice(root.href.length);
    bunnyHlsResource(root, resource);
    return `/api/video/hls?s=${encodeURIComponent(sessionId)}&path=${encodeURIComponent(resource)}`;
  };
  return text.split(/\r?\n/).map(line => {
    if (!line.trim()) return line;
    if (!line.startsWith('#')) return localUri(line.trim());
    return line.replace(/URI="([^"]+)"/g, (_match, uri: string) => `URI="${localUri(uri)}"`);
  }).join('\n');
}

export async function relayBunnyResource(upstream: URL, sessionId: string, root: URL, request: Request): Promise<Response> {
  const range = request.headers.get('range');
  if (range && !/^bytes=\d+-\d*$/.test(range)) return new Response(null, { status: 416 });
  const playlist = upstream.pathname.endsWith('.m3u8');
  const response = await fetch(upstream, {
    cache: 'no-store', redirect: 'error',
    signal: AbortSignal.any([request.signal, AbortSignal.timeout(playlist ? 20_000 : 120_000)]),
    headers: range && !playlist ? { Range: range } : {},
  });
  if (!response.ok) {
    await response.body?.cancel();
    return new Response(null, { status: response.status });
  }
  const limit = playlist ? MAX_PLAYLIST_BYTES : MAX_SEGMENT_BYTES;
  if (Number(response.headers.get('content-length')) > limit || !response.body) {
    await response.body?.cancel();
    return new Response(null, { status: 502 });
  }
  let received = 0;
  const boundedBody = response.body.pipeThrough(new TransformStream<Uint8Array, Uint8Array>({
    transform(chunk, controller) {
      received += chunk.byteLength;
      if (received > limit) throw new Error('HLS response exceeds limit');
      controller.enqueue(chunk);
    },
  }));
  const headers = new Headers({
    'Cache-Control': 'private, no-store', 'X-Content-Type-Options': 'nosniff',
    'Cross-Origin-Resource-Policy': 'same-origin',
    'Content-Type': playlist ? 'application/vnd.apple.mpegurl' : MEDIA_TYPES[upstream.pathname.split('.').at(-1) ?? ''] ?? 'application/octet-stream',
  });
  if (playlist) {
    const text = await new Response(boundedBody).text();
    return new Response(rewriteBunnyPlaylist(text, upstream, sessionId, root), { headers });
  }
  for (const name of ['content-length', 'content-range', 'accept-ranges']) {
    const header = response.headers.get(name);
    if (header) headers.set(name, header);
  }
  return new Response(boundedBody, { status: response.status, headers });
}
