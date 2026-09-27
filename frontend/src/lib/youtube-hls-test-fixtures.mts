export function youtubeIndexFixture() {
  const initialization = Buffer.alloc(16);
  initialization.writeUInt32BE(8, 0);
  initialization.write('ftyp', 4);
  initialization.writeUInt32BE(8, 8);
  initialization.write('moov', 12);
  const index = Buffer.alloc(56);
  index.writeUInt32BE(56, 0);
  index.write('sidx', 4);
  index.writeUInt32BE(1, 12);
  index.writeUInt32BE(1000, 16);
  index.writeUInt16BE(2, 30);
  for (const [offset, length] of [[32, 100], [44, 150]]) {
    index.writeUInt32BE(length, offset);
    index.writeUInt32BE(5000, offset + 4);
    index.writeUInt32BE(0x90000000, offset + 8);
  }
  return { initialization, index };
}

export function createYouTubeHlsFetchFixture() {
  const requests: { url: URL; options: RequestInit }[] = [];
  const { initialization, index } = youtubeIndexFixture();
  const fixtureFetch = async (input: string | URL | Request, options: RequestInit = {}): Promise<Response> => {
    const url = new URL(input instanceof Request ? input.url : String(input));
    requests.push({ url, options });
    if (url.pathname.endsWith('/visitor_id')) return Response.json({ responseContext: { visitorData: 'synthetic-visitor' } });
    if (url.pathname.endsWith('/player')) {
      const payload = JSON.parse(String(options.body));
      const adaptiveFormats = [360, 720, 0].map(height => ({
        url: `https://rr1.googlevideo.com/videoplayback?expire=${Math.floor(Date.now() / 1000) + 3600}&itag=${height}`,
        mimeType: height ? 'video/mp4; codecs="avc1.4d401f"' : 'audio/mp4; codecs="mp4a.40.2"',
        bitrate: height ? height * 2000 : 128000, width: height === 360 ? 640 : 1280,
        height, fps: 30, contentLength: '322',
        initRange: { start: '0', end: '15' }, indexRange: { start: '16', end: '71' },
      }));
      return Response.json({ playabilityStatus: { status: 'OK' }, videoDetails: { videoId: payload.videoId },
        streamingData: { adaptiveFormats } });
    }
    if (url.hostname !== 'rr1.googlevideo.com' || options.method !== 'GET') throw new Error('Unexpected fixture request');
    const range = new Headers(options.headers).get('range');
    const bytes = range === 'bytes=0-15' ? initialization : range === 'bytes=16-71' ? index : undefined;
    if (!bytes) throw new Error('Only initialization and index metadata may be fetched');
    return new Response(new Uint8Array(bytes), { status: 206, headers: {
      'Content-Range': `${range!.replace('=', ' ')}/322`, 'Content-Length': String(bytes.length),
    } });
  };
  return { fetch: fixtureFetch, requests };
}
