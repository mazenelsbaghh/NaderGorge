import { getYouTubeHlsMediaRange, youTubeMediaUrl, YouTubeHlsError, type YouTubeHlsSource } from './youtube-hls-playlist.ts';

export async function relayYouTubeHlsMedia(source: YouTubeHlsSource, media: string, part: string, requestSignal: AbortSignal): Promise<Response> {
  const { url, range, contentLength } = getYouTubeHlsMediaRange(source, media, part);
  const expectedLength = range.end - range.start + 1;
  const signal = AbortSignal.any([requestSignal, AbortSignal.timeout(120_000)]);
  let upstreamUrl = url;
  for (let redirects = 0; redirects <= 3; redirects += 1) {
    let upstream: Response;
    try {
      upstream = await fetch(upstreamUrl, { headers: {
        Range: `bytes=${range.start}-${range.end}`, 'Accept-Encoding': 'identity',
      }, credentials: 'omit', redirect: 'manual', cache: 'no-store', signal });
    } catch {
      throw new YouTubeHlsError('upstream');
    }
    if (upstream.status >= 300 && upstream.status < 400) {
      await upstream.body?.cancel();
      const location = upstream.headers.get('location');
      if (!location) throw new YouTubeHlsError('upstream');
      try { upstreamUrl = youTubeMediaUrl(new URL(location, upstreamUrl).href); }
      catch { throw new YouTubeHlsError('upstream'); }
      continue;
    }
    if (upstream.status !== 206
      || upstream.headers.get('content-range') !== `bytes ${range.start}-${range.end}/${contentLength}`
      || upstream.headers.get('content-length') !== String(expectedLength)
      || (upstream.headers.has('content-encoding') && upstream.headers.get('content-encoding') !== 'identity')
      || !upstream.body) {
      await upstream.body?.cancel();
      throw new YouTubeHlsError('upstream');
    }
    const reader = upstream.body.getReader();
    let received = 0;
    const body = new ReadableStream<Uint8Array>({
      async pull(controller) {
        try {
          const chunk = await reader.read();
          if (chunk.done) {
            if (received !== expectedLength) controller.error(new YouTubeHlsError('upstream'));
            else controller.close();
            return;
          }
          received += chunk.value.byteLength;
          if (received > expectedLength) {
            controller.error(new YouTubeHlsError('upstream'));
            await reader.cancel();
            return;
          }
          controller.enqueue(chunk.value);
        } catch {
          controller.error(new YouTubeHlsError('upstream'));
        }
      },
      cancel() { return reader.cancel(); },
    });
    return new Response(body, { headers: {
      'Content-Type': 'video/mp4', 'Content-Length': String(expectedLength),
      'Cache-Control': 'no-store, private', 'X-Content-Type-Options': 'nosniff',
    } });
  }
  throw new YouTubeHlsError('upstream');
}
