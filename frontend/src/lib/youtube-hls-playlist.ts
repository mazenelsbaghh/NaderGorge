export type YouTubeHlsErrorCode = 'invalid-video' | 'invalid-playlist' | 'source-expired'
  | 'unavailable' | 'unsupported-source' | 'upstream' | 'busy';

const ERROR_STATUS: Record<YouTubeHlsErrorCode, number> = {
  'invalid-video': 400, 'invalid-playlist': 400, 'source-expired': 410,
  unavailable: 422, 'unsupported-source': 422, upstream: 502, busy: 503,
};

export class YouTubeHlsError extends Error {
  readonly code: YouTubeHlsErrorCode;
  readonly status: number;

  constructor(code: YouTubeHlsErrorCode) {
    super(`YouTube HLS: ${code}`);
    this.name = 'YouTubeHlsError';
    this.code = code;
    this.status = ERROR_STATUS[code];
  }
}

export interface YouTubeByteRange { start: number; end: number }
export interface YouTubeHlsFormat {
  url: string;
  codec: string;
  bandwidth: number;
  width: number;
  height: number;
  fps: number;
  contentLength: number;
  expiresAt: number;
  initRange: YouTubeByteRange;
  indexRange: YouTubeByteRange;
}
export interface YouTubeHlsSegment { offset: number; length: number; duration: number }
export interface YouTubeHlsTrack {
  format: YouTubeHlsFormat;
  segments: readonly YouTubeHlsSegment[];
  durationSeconds: number;
}
export interface YouTubeHlsQuality { height: number; width: number; fps: number; bandwidth: number }
export interface YouTubeHlsSource {
  videoId: string;
  version: string;
  expiresAt: number;
  durationSeconds: number;
  qualities: readonly YouTubeHlsQuality[];
  audio: YouTubeHlsTrack;
  videos: readonly YouTubeHlsTrack[];
}

export function youTubeMediaUrl(input: string): URL {
  let source: URL;
  try { source = new URL(input); } catch { throw new YouTubeHlsError('upstream'); }
  if (input.length > 8192 || /[\u0000-\u0020"\\]/.test(input)
    || source.protocol !== 'https:' || source.port || source.username || source.password || source.hash
    || !/^[a-z0-9-]+(?:\.[a-z0-9-]+)*\.googlevideo\.com$/i.test(source.hostname)
    || source.pathname !== '/videoplayback') throw new YouTubeHlsError('upstream');
  return source;
}

function unsigned64(bytes: Buffer, offset: number): number {
  const integer = bytes.readUInt32BE(offset) * 4294967296 + bytes.readUInt32BE(offset + 4);
  if (!Number.isSafeInteger(integer)) throw new YouTubeHlsError('unsupported-source');
  return integer;
}

function sidxBox(bytes: Buffer): { offset: number; end: number; header: number } {
  let offset = 0;
  while (offset + 8 <= bytes.length) {
    const shortSize = bytes.readUInt32BE(offset);
    const header = shortSize === 1 ? 16 : 8;
    if (offset + header > bytes.length) throw new YouTubeHlsError('upstream');
    const size = shortSize === 1 ? unsigned64(bytes, offset + 8) : (shortSize || bytes.length - offset);
    if (size < header || offset + size > bytes.length) throw new YouTubeHlsError('upstream');
    if (bytes.toString('ascii', offset + 4, offset + 8) === 'sidx') return { offset, end: offset + size, header };
    offset += size;
  }
  throw new YouTubeHlsError('unsupported-source');
}

function indexHeader(bytes: Buffer, box: ReturnType<typeof sidxBox>) {
  const start = box.offset + box.header;
  if (start + 24 > box.end) throw new YouTubeHlsError('upstream');
  const version = bytes[start];
  if (version !== 0 && version !== 1) throw new YouTubeHlsError('unsupported-source');
  const countOffset = start + (version === 0 ? 22 : 30);
  if (countOffset + 2 > box.end) throw new YouTubeHlsError('upstream');
  const earliest = version === 0 ? bytes.readUInt32BE(start + 12) : unsigned64(bytes, start + 12);
  const firstOffset = version === 0 ? bytes.readUInt32BE(start + 16) : unsigned64(bytes, start + 20);
  const timescale = bytes.readUInt32BE(start + 8);
  const count = bytes.readUInt16BE(countOffset);
  if (!timescale || !count || count > 10000 || earliest !== 0) throw new YouTubeHlsError('unsupported-source');
  if (countOffset + 2 + count * 12 !== box.end) throw new YouTubeHlsError('upstream');
  return { timescale, firstOffset, count, referencesOffset: countOffset + 2 };
}

export function parseYouTubeSidx(indexBytes: Uint8Array, format: YouTubeHlsFormat): YouTubeHlsTrack {
  const bytes = Buffer.from(indexBytes);
  const box = sidxBox(bytes);
  const header = indexHeader(bytes, box);
  let mediaOffset = format.indexRange.start + box.end + header.firstOffset;
  const segments: YouTubeHlsSegment[] = [];
  for (let index = 0; index < header.count; index += 1) {
    const referenceOffset = header.referencesOffset + index * 12;
    const reference = bytes.readUInt32BE(referenceOffset);
    const duration = bytes.readUInt32BE(referenceOffset + 4) / header.timescale;
    const sap = bytes.readUInt32BE(referenceOffset + 8);
    const length = reference & 0x7fffffff;
    if (reference >>> 31 || !length || !duration || sap >>> 28 !== 9) throw new YouTubeHlsError('unsupported-source');
    if (!Number.isSafeInteger(mediaOffset + length) || mediaOffset + length > format.contentLength) {
      throw new YouTubeHlsError('upstream');
    }
    segments.push({ offset: mediaOffset, length, duration });
    mediaOffset += length;
  }
  return { format, segments, durationSeconds: segments.reduce((total, segment) => total + segment.duration, 0) };
}

export function validateYouTubeInitialization(initialization: Uint8Array): void {
  const bytes = Buffer.from(initialization);
  let offset = 0;
  const boxes = new Set<string>();
  while (offset + 8 <= bytes.length) {
    const size = bytes.readUInt32BE(offset);
    if (size < 8 || offset + size > bytes.length) throw new YouTubeHlsError('unsupported-source');
    boxes.add(bytes.toString('ascii', offset + 4, offset + 8));
    offset += size;
  }
  if (offset !== bytes.length || !boxes.has('ftyp') || !boxes.has('moov')) throw new YouTubeHlsError('unsupported-source');
}

function mediaPlaylist(track: YouTubeHlsTrack): string {
  const { format, segments } = track;
  // Repeating signed URLs can otherwise turn a bounded index into an enormous response.
  if (segments.length * (format.url.length + 90) > 8 * 1024 * 1024) throw new YouTubeHlsError('unsupported-source');
  const targetDuration = Math.ceil(Math.max(...segments.map(segment => segment.duration)));
  const lines = ['#EXTM3U', '#EXT-X-VERSION:7', `#EXT-X-TARGETDURATION:${targetDuration}`,
    '#EXT-X-MEDIA-SEQUENCE:0', '#EXT-X-PLAYLIST-TYPE:VOD',
    `#EXT-X-MAP:URI="${format.url}",BYTERANGE="${format.initRange.end - format.initRange.start + 1}@${format.initRange.start}"`];
  for (const segment of segments) lines.push(`#EXTINF:${segment.duration.toFixed(9)},`,
    `#EXT-X-BYTERANGE:${segment.length}@${segment.offset}`, format.url);
  return [...lines, '#EXT-X-ENDLIST', ''].join('\n');
}

function masterPlaylist(source: YouTubeHlsSource, sessionId: string, quality?: string): string {
  const videos = quality === undefined ? source.videos : source.videos.filter(track => String(track.format.height) === quality);
  if (!videos.length) throw new YouTubeHlsError('invalid-playlist');
  const localUri = (playlist: string) => `/api/video/youtube-hls?s=${encodeURIComponent(sessionId)}&playlist=${playlist}&v=${source.version}`;
  const lines = ['#EXTM3U', '#EXT-X-VERSION:7', '#EXT-X-INDEPENDENT-SEGMENTS',
    `#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="audio",NAME="Audio",DEFAULT=YES,AUTOSELECT=YES,URI="${localUri('audio')}"`];
  for (const { format } of videos) {
    lines.push(`#EXT-X-STREAM-INF:BANDWIDTH=${format.bandwidth + source.audio.format.bandwidth},CODECS="${format.codec},${source.audio.format.codec}",RESOLUTION=${format.width}x${format.height},FRAME-RATE=${format.fps.toFixed(3)},AUDIO="audio"`, localUri(String(format.height)));
  }
  return [...lines, ''].join('\n');
}

export function getYouTubeHlsPlaylist(source: YouTubeHlsSource, request: {
  sessionId: string; playlist: string; quality?: string;
}): string {
  if (!/^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(request.sessionId)
    || (request.quality !== undefined && !source.qualities.some(quality => String(quality.height) === request.quality))) {
    throw new YouTubeHlsError('invalid-playlist');
  }
  if (source.expiresAt <= Date.now()) throw new YouTubeHlsError('source-expired');
  if (request.playlist === 'master') return masterPlaylist(source, request.sessionId, request.quality);
  if (request.playlist === 'audio') return mediaPlaylist(source.audio);
  const track = source.videos.find(video => String(video.format.height) === request.playlist);
  if (!track) throw new YouTubeHlsError('invalid-playlist');
  return mediaPlaylist(track);
}
