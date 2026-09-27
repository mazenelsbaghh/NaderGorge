import { randomBytes } from 'node:crypto';
import {
  parseYouTubeSidx, validateYouTubeInitialization, youTubeMediaUrl, YouTubeHlsError,
  type YouTubeByteRange, type YouTubeHlsFormat, type YouTubeHlsSource, type YouTubeHlsTrack,
} from './youtube-hls-playlist.ts';

export { getYouTubeHlsPlaylist, YouTubeHlsError } from './youtube-hls-playlist.ts';
export type { YouTubeHlsSource } from './youtube-hls-playlist.ts';

export interface YouTubeHlsSharedCache {
  read(videoId: string, version?: string): Promise<YouTubeHlsSource | undefined>;
  write(source: YouTubeHlsSource): Promise<void>;
}

const MAX_JSON_BYTES = 2 * 1024 * 1024;
const MAX_RANGE_BYTES = 128 * 1024;
const MAX_CACHE_BYTES = 20 * 1024 * 1024;
const MAX_SOURCE_BYTES = 2 * 1024 * 1024;
const SOURCE_EXPIRY_MARGIN_MS = 120_000;
const CLIENT_AGENT = 'com.google.ios.youtube/1.02 (RealityDevice14,1; U; CPU OS 25_6_0 like Mac OS X; US)';
const cache = new Map<string, { source: YouTubeHlsSource; bytes: number; shared: boolean }>();
const latestVersion = new Map<string, string>();
const pending = new Map<string, Promise<YouTubeHlsSource>>();
let cacheBytes = 0;

function objectRecord(input: unknown): Record<string, unknown> {
  if (!input || typeof input !== 'object' || Array.isArray(input)) throw new YouTubeHlsError('upstream');
  return input as Record<string, unknown>;
}

function positiveInteger(input: unknown): number {
  if (typeof input !== 'number' && (typeof input !== 'string' || !/^\d+$/.test(input))) {
    throw new YouTubeHlsError('upstream');
  }
  const integer = Number(input);
  if (!Number.isSafeInteger(integer) || integer <= 0) throw new YouTubeHlsError('upstream');
  return integer;
}

function formatRange(input: unknown): YouTubeByteRange {
  const range = objectRecord(input);
  const start = range.start === '0' || range.start === 0 ? 0 : positiveInteger(range.start);
  const end = positiveInteger(range.end);
  if (end < start || end - start + 1 > MAX_RANGE_BYTES) throw new YouTubeHlsError('unsupported-source');
  return { start, end };
}

async function boundedBody(response: Response, maxBytes: number, signal: AbortSignal): Promise<Buffer> {
  if (!response.body) throw new YouTubeHlsError('upstream');
  const reader = response.body.getReader();
  const chunks: Uint8Array[] = [];
  let receivedBytes = 0;
  const cancelReader = () => { void reader.cancel().catch(() => { /* Preserve the original read failure. */ }); };
  signal.addEventListener('abort', cancelReader, { once: true });
  try {
    if (Number(response.headers.get('content-length')) > maxBytes) throw new YouTubeHlsError('upstream');
    while (true) {
      signal.throwIfAborted();
      const chunk = await reader.read();
      signal.throwIfAborted();
      if (chunk.done) return Buffer.concat(chunks, receivedBytes);
      receivedBytes += chunk.value.byteLength;
      if (receivedBytes > maxBytes) throw new YouTubeHlsError('upstream');
      chunks.push(chunk.value);
    }
  } finally {
    signal.removeEventListener('abort', cancelReader);
    cancelReader();
  }
}

interface MetadataRequest {
  url: URL;
  body?: string;
  range?: YouTubeByteRange;
  contentLength?: number;
}

function requestHeaders(request: MetadataRequest): Headers {
  const headers = new Headers({ 'User-Agent': CLIENT_AGENT, 'Accept-Encoding': 'identity' });
  if (request.body !== undefined) {
    headers.set('Content-Type', 'application/json');
    headers.set('X-Goog-Api-Format-Version', '2');
  }
  if (request.range) headers.set('Range', `bytes=${request.range.start}-${request.range.end}`);
  return headers;
}

function rangeResponseIsValid(response: Response, request: MetadataRequest): boolean {
  if (!request.range) return response.status === 200;
  const { start, end } = request.range;
  return response.status === 206
    && response.headers.get('content-range') === `bytes ${start}-${end}/${request.contentLength}`
    && (!response.headers.has('content-encoding') || response.headers.get('content-encoding') === 'identity');
}

async function requestMetadataBytes(request: MetadataRequest): Promise<Buffer> {
  const signal = AbortSignal.timeout(15_000);
  let upstream = request.url;
  for (let redirect = 0; redirect <= 3; redirect += 1) {
    const response = await fetch(upstream, { method: request.body === undefined ? 'GET' : 'POST',
      body: request.body, headers: requestHeaders(request), credentials: 'omit', redirect: 'manual', cache: 'no-store', signal });
    if (response.status >= 300 && response.status < 400 && request.range) {
      await response.body?.cancel();
      const location = response.headers.get('location');
      if (!location) throw new YouTubeHlsError('upstream');
      upstream = youTubeMediaUrl(new URL(location, upstream).href);
      continue;
    }
    if (!rangeResponseIsValid(response, request)) {
      await response.body?.cancel();
      throw new YouTubeHlsError('upstream');
    }
    const limit = request.range ? request.range.end - request.range.start + 1 : MAX_JSON_BYTES;
    const bytes = await boundedBody(response, limit, signal);
    if (request.range && bytes.length !== limit) throw new YouTubeHlsError('upstream');
    return bytes;
  }
  throw new YouTubeHlsError('upstream');
}

async function innertubeMetadata(endpoint: string, payload: unknown): Promise<Record<string, unknown>> {
  const bytes = await requestMetadataBytes({ url: new URL(endpoint), body: JSON.stringify(payload) });
  try { return objectRecord(JSON.parse(bytes.toString('utf8'))); } catch (error) {
    if (error instanceof SyntaxError) throw new YouTubeHlsError('upstream');
    throw error;
  }
}

async function playerMetadata(videoId: string): Promise<Record<string, unknown>> {
  const client: Record<string, unknown> = { clientName: 'VISIONOS', clientVersion: '1.02', clientScreen: 'WATCH',
    platform: 'MOBILE', deviceMake: 'Apple', deviceModel: 'RealityDevice14,1', osName: 'visionOS',
    osVersion: '25.6.0.23O471', hl: 'en', gl: 'US', utcOffsetMinutes: 0 };
  const context = { client, request: { internalExperimentFlags: [], useSsl: true }, user: { lockedSafetyMode: false } };
  const visitor = await innertubeMetadata('https://www.youtube.com/youtubei/v1/visitor_id?prettyPrint=false', { context });
  const visitorData = objectRecord(visitor.responseContext).visitorData;
  if (typeof visitorData !== 'string' || !visitorData || visitorData.length > 8192) throw new YouTubeHlsError('upstream');
  client.visitorData = visitorData;
  const query = new URLSearchParams({ prettyPrint: 'false', t: randomBytes(9).toString('base64url'), id: videoId });
  return innertubeMetadata(`https://youtubei.googleapis.com/youtubei/v1/player?${query}`, {
    context, videoId, cpn: randomBytes(12).toString('base64url'), contentCheckOk: true, racyCheckOk: true,
  });
}

function playableFormats(player: Record<string, unknown>, videoId: string): Record<string, unknown>[] {
  if (objectRecord(player.playabilityStatus).status !== 'OK') throw new YouTubeHlsError('unavailable');
  const details = objectRecord(player.videoDetails);
  if (details.videoId !== videoId) throw new YouTubeHlsError('upstream');
  if (details.isLive === true || details.isLiveContent === true || details.isPostLiveDvr === true) {
    throw new YouTubeHlsError('unsupported-source');
  }
  const formats = objectRecord(player.streamingData).adaptiveFormats;
  if (!Array.isArray(formats) || formats.length > 256) throw new YouTubeHlsError('upstream');
  return formats.map(objectRecord);
}

function parsedFormat(format: Record<string, unknown>, expiresAfter = Date.now() + 2 * SOURCE_EXPIRY_MARGIN_MS): YouTubeHlsFormat {
  if (typeof format.url !== 'string' || typeof format.mimeType !== 'string') throw new YouTubeHlsError('upstream');
  const url = youTubeMediaUrl(format.url);
  const expiry = url.searchParams.getAll('expire');
  const expiresAt = positiveInteger(expiry.length === 1 ? expiry[0] : null) * 1000;
  if (!Number.isSafeInteger(expiresAt) || expiresAt <= expiresAfter
    || expiresAt > Date.now() + 24 * 60 * 60_000) throw new YouTubeHlsError('upstream');
  const codec = /codecs="(avc1\.[0-9a-f]{6}|mp4a\.40\.2)"/i.exec(format.mimeType)?.[1];
  if (!codec) throw new YouTubeHlsError('unsupported-source');
  const video = codec.toLowerCase().startsWith('avc1.');
  const parsed: YouTubeHlsFormat = { url: url.href, codec, expiresAt,
    contentLength: positiveInteger(format.contentLength), bandwidth: positiveInteger(format.bitrate),
    width: video ? positiveInteger(format.width) : 0, height: video ? positiveInteger(format.height) : 0,
    fps: video ? positiveInteger(format.fps) : 0, initRange: formatRange(format.initRange), indexRange: formatRange(format.indexRange) };
  if (parsed.initRange.start !== 0 || parsed.initRange.end >= parsed.indexRange.start
    || parsed.indexRange.end >= parsed.contentLength || parsed.width > 4096 || parsed.fps > 120) {
    throw new YouTubeHlsError('unsupported-source');
  }
  return parsed;
}

function selectedVideoFormats(formats: Record<string, unknown>[]): YouTubeHlsFormat[] {
  const videos = new Map<number, YouTubeHlsFormat>();
  for (const format of formats) {
    if (typeof format.mimeType !== 'string' || !format.mimeType.startsWith('video/mp4;')
      || !format.mimeType.includes('avc1.') || ![144, 240, 360, 480, 720, 1080].includes(Number(format.height))
      || typeof format.url !== 'string') continue;
    const video = parsedFormat(format);
    if (!videos.has(video.height) || video.fps > videos.get(video.height)!.fps) videos.set(video.height, video);
  }
  if (!videos.size) throw new YouTubeHlsError('unsupported-source');
  return [...videos.values()].sort((left, right) => left.height - right.height);
}

function selectedAudioFormat(formats: Record<string, unknown>[]): YouTubeHlsFormat {
  const audio = formats.filter(format => typeof format.mimeType === 'string'
    && format.mimeType.startsWith('audio/mp4;') && format.mimeType.includes('mp4a.40.2') && typeof format.url === 'string');
  const defaultTrack = audio.find(format => format.audioTrack && objectRecord(format.audioTrack).audioIsDefault === true);
  const selected = defaultTrack ?? audio.find(format => !format.audioTrack) ?? audio[0];
  if (!selected) throw new YouTubeHlsError('unsupported-source');
  return parsedFormat(selected);
}

async function formatTrack(format: YouTubeHlsFormat): Promise<YouTubeHlsTrack> {
  const url = youTubeMediaUrl(format.url);
  const initialization = await requestMetadataBytes({ url, range: format.initRange, contentLength: format.contentLength });
  validateYouTubeInitialization(initialization);
  const index = await requestMetadataBytes({ url, range: format.indexRange, contentLength: format.contentLength });
  return parseYouTubeSidx(index, format);
}

async function completedTracks(formats: YouTubeHlsFormat[]): Promise<YouTubeHlsTrack[]> {
  // Keep a failing extraction in its concurrency slot until its other bounded reads finish.
  const tracks = await Promise.allSettled(formats.map(formatTrack));
  const failed = tracks.find(track => track.status === 'rejected');
  if (failed?.status === 'rejected') throw failed.reason;
  return tracks.filter((track): track is PromiseFulfilledResult<YouTubeHlsTrack> => track.status === 'fulfilled')
    .map(track => track.value);
}

async function extractedSource(videoId: string): Promise<YouTubeHlsSource> {
  const formats = playableFormats(await playerMetadata(videoId), videoId);
  const selected = [...selectedVideoFormats(formats), selectedAudioFormat(formats)];
  const tracks = await completedTracks(selected);
  const audio = tracks[tracks.length - 1];
  const videos = tracks.slice(0, -1);
  const durationSeconds = Math.max(...videos.map(track => track.durationSeconds));
  if ([audio, ...videos].some(track => Math.abs(track.durationSeconds - durationSeconds) > 2)) {
    throw new YouTubeHlsError('unsupported-source');
  }
  return { videoId, version: randomBytes(12).toString('base64url'), durationSeconds,
    expiresAt: Math.min(...selected.map(format => format.expiresAt)) - SOURCE_EXPIRY_MARGIN_MS,
    qualities: videos.map(({ format }) => ({ height: format.height, width: format.width, fps: format.fps,
      bandwidth: format.bandwidth + audio.format.bandwidth })), audio, videos };
}

function restoredTrack(input: unknown): YouTubeHlsTrack {
  const track = objectRecord(input);
  const storedFormat = objectRecord(track.format);
  if (typeof storedFormat.codec !== 'string' || !/^(avc1\.[0-9a-f]{6}|mp4a\.40\.2)$/i.test(storedFormat.codec)) {
    throw new YouTubeHlsError('upstream');
  }
  const format = parsedFormat({ ...storedFormat, bitrate: storedFormat.bandwidth,
    mimeType: `application/mp4; codecs="${storedFormat.codec}"` }, Date.now());
  if (!Array.isArray(track.segments) || !track.segments.length || track.segments.length > 10000) {
    throw new YouTubeHlsError('upstream');
  }
  let previousEnd = format.indexRange.end + 1;
  const segments = track.segments.map((inputSegment: unknown, index: number) => {
    const segment = objectRecord(inputSegment);
    const offset = positiveInteger(segment.offset), length = positiveInteger(segment.length);
    if (typeof segment.duration !== 'number' || !Number.isFinite(segment.duration) || segment.duration <= 0
      || offset < previousEnd || (index > 0 && offset !== previousEnd)
      || !Number.isSafeInteger(offset + length) || offset + length > format.contentLength) throw new YouTubeHlsError('upstream');
    previousEnd = offset + length;
    return { offset, length, duration: segment.duration };
  });
  const durationSeconds = segments.reduce((total, segment) => total + segment.duration, 0);
  if (typeof track.durationSeconds !== 'number' || !Number.isFinite(durationSeconds)
    || Math.abs(track.durationSeconds - durationSeconds) > 0.01) throw new YouTubeHlsError('upstream');
  return { format, segments, durationSeconds };
}

function restoredSource(input: unknown, videoId: string, version?: string): YouTubeHlsSource | undefined {
  const stored = objectRecord(input);
  if (Buffer.byteLength(JSON.stringify(stored)) > MAX_SOURCE_BYTES || stored.videoId !== videoId
    || typeof stored.version !== 'string' || !/^[A-Za-z0-9_-]{16}$/.test(stored.version)
    || (version !== undefined && stored.version !== version)) throw new YouTubeHlsError('upstream');
  const expiresAt = positiveInteger(stored.expiresAt);
  if (expiresAt <= Date.now()) return undefined;
  if (!Array.isArray(stored.videos) || !stored.videos.length || stored.videos.length > 6) throw new YouTubeHlsError('upstream');
  const videos = stored.videos.map(restoredTrack);
  const audio = restoredTrack(stored.audio);
  const heights = videos.map(track => track.format.height);
  if (audio.format.codec !== 'mp4a.40.2' || new Set(heights).size !== heights.length
    || videos.some(track => !track.format.codec.startsWith('avc1.') || ![144, 240, 360, 480, 720, 1080].includes(track.format.height))) {
    throw new YouTubeHlsError('upstream');
  }
  const durationSeconds = Math.max(...videos.map(track => track.durationSeconds));
  if (expiresAt > Math.min(audio.format.expiresAt, ...videos.map(track => track.format.expiresAt)) - SOURCE_EXPIRY_MARGIN_MS
    || [audio, ...videos].some(track => Math.abs(track.durationSeconds - durationSeconds) > 2)) throw new YouTubeHlsError('upstream');
  return { videoId, version: stored.version, expiresAt, durationSeconds, audio, videos,
    qualities: videos.map(({ format }) => ({ height: format.height, width: format.width, fps: format.fps,
      bandwidth: format.bandwidth + audio.format.bandwidth })) };
}

function discardCachedSource(version: string): void {
  const previous = cache.get(version);
  if (previous) {
    cacheBytes -= previous.bytes;
    if (latestVersion.get(previous.source.videoId) === version) latestVersion.delete(previous.source.videoId);
  }
  cache.delete(version);
}

function storeSource(entry: { source: YouTubeHlsSource; shared: boolean }): void {
  const { source } = entry;
  const shared = entry.shared || cache.get(source.version)?.shared === true;
  const bytes = Buffer.byteLength(JSON.stringify(source));
  if (bytes > MAX_SOURCE_BYTES || source.expiresAt <= Date.now()) {
    throw new YouTubeHlsError('unsupported-source');
  }
  for (const [version, entry] of cache) if (entry.source.expiresAt <= Date.now()) discardCachedSource(version);
  discardCachedSource(source.version);
  while (cache.size >= 32 || cacheBytes + bytes > MAX_CACHE_BYTES) discardCachedSource(cache.keys().next().value!);
  cache.set(source.version, { source, shared, bytes });
  const current = cache.get(latestVersion.get(source.videoId) ?? '');
  if (!current || current.source.expiresAt <= source.expiresAt) latestVersion.set(source.videoId, source.version);
  cacheBytes += bytes;
}

function cachedSource(videoId: string, version?: string): YouTubeHlsSource | undefined {
  const cachedVersion = version ?? latestVersion.get(videoId);
  const entry = cachedVersion ? cache.get(cachedVersion) : undefined;
  const usableUntil = Date.now() + (version === undefined ? SOURCE_EXPIRY_MARGIN_MS : 0);
  if (entry && entry.source.videoId === videoId && entry.source.expiresAt > usableUntil) {
    cache.delete(entry.source.version);
    cache.set(entry.source.version, entry);
    return entry.source;
  }
  if (entry && entry.source.expiresAt <= Date.now()) discardCachedSource(entry.source.version);
  return undefined;
}

async function validatedExtraction(videoId: string): Promise<YouTubeHlsSource> {
  try {
    const source = await extractedSource(videoId);
    if (source.expiresAt <= Date.now() + SOURCE_EXPIRY_MARGIN_MS
      || Buffer.byteLength(JSON.stringify(source)) > MAX_SOURCE_BYTES) throw new YouTubeHlsError('unsupported-source');
    return source;
  } finally {
    pending.delete(videoId);
  }
}

function sharedExtraction(videoId: string): Promise<YouTubeHlsSource> {
  const existing = pending.get(videoId);
  if (existing) return existing;
  if (pending.size >= 4) throw new YouTubeHlsError('busy');
  // Viewer authorization and cancellation must never become part of a shared Google request.
  const extraction = validatedExtraction(videoId);
  pending.set(videoId, extraction);
  return extraction;
}

async function resolveFreshSource(videoId: string, sharedCache?: YouTubeHlsSharedCache): Promise<YouTubeHlsSource> {
  const shared = await sharedCache?.read(videoId);
  const restored = shared ? restoredSource(shared, videoId) : undefined;
  if (restored && restored.expiresAt > Date.now() + SOURCE_EXPIRY_MARGIN_MS) {
    storeSource({ source: restored, shared: true });
    return restored;
  }
  const cached = cachedSource(videoId);
  if (cached) return shareLocalSource(cached, sharedCache);
  const source = await sharedExtraction(videoId);
  await sharedCache?.write(source);
  storeSource({ source, shared: sharedCache !== undefined });
  return source;
}

async function shareLocalSource(source: YouTubeHlsSource, sharedCache?: YouTubeHlsSharedCache): Promise<YouTubeHlsSource> {
  const entry = cache.get(source.version);
  if (sharedCache && !entry?.shared) {
    await sharedCache.write(source);
    if (entry) entry.shared = true;
  }
  return source;
}

async function sourceGeneration(videoId: string, version: string, sharedCache?: YouTubeHlsSharedCache): Promise<YouTubeHlsSource> {
  const shared = await sharedCache?.read(videoId, version);
  const source = shared ? restoredSource(shared, videoId, version) : undefined;
  if (!source) throw new YouTubeHlsError('source-expired');
  storeSource({ source, shared: true });
  return source;
}

async function resolvedSource(videoId: string, version?: string,
  sharedCache?: YouTubeHlsSharedCache): Promise<YouTubeHlsSource> {
  if (!/^[A-Za-z0-9_-]{11}$/.test(videoId)) throw new YouTubeHlsError('invalid-video');
  if (version !== undefined && !/^[A-Za-z0-9_-]{16}$/.test(version)) throw new YouTubeHlsError('source-expired');
  const cached = cachedSource(videoId, version);
  if (cached) return shareLocalSource(cached, sharedCache);
  if (version !== undefined) return sourceGeneration(videoId, version, sharedCache);
  return resolveFreshSource(videoId, sharedCache);
}

export async function resolveYouTubeHlsSource(videoId: string, version?: string,
  sharedCache?: YouTubeHlsSharedCache): Promise<YouTubeHlsSource> {
  try { return await resolvedSource(videoId, version, sharedCache); } catch (error) {
    // Never attach upstream messages or causes: they can contain signed URLs and visitor data.
    if (error instanceof TypeError || error instanceof DOMException) throw new YouTubeHlsError('upstream');
    throw error;
  }
}
