import { createCipheriv, createDecipheriv, createHash, randomBytes } from 'node:crypto';
import { deflateRawSync, inflateRawSync } from 'node:zlib';
import type { VideoEmbedMaterial } from './video-embed-material';

const BOOTSTRAP_LIFETIME_MS = 90_000;
// Keep the browser grant alive through the 30-minute CDN signature renewal.
const MEDIA_COOKIE_LIFETIME_MS = 35 * 60_000;
const COOKIE_PURPOSE = 'massar-video-browser-session-v1';

export type PlaybackMaterial = VideoEmbedMaterial & {
  expiresAt?: string;
  ExpiresAt?: string;
  watermarkSettings?: Record<string, string>;
  studentId?: string;
  bunnyEmbedQuery?: string;
  BunnyEmbedQuery?: string;
  youTubeQualityEnabled?: boolean;
  YouTubeQualityEnabled?: boolean;
  youTubeQualityBottomCoverPercent?: number;
  YouTubeQualityBottomCoverPercent?: number;
  youTubeQualityMobileBottomCoverPercent?: number;
  YouTubeQualityMobileBottomCoverPercent?: number;
};

type BrowserPlaybackSession = {
  sessionId: string;
  authorization: string;
  surface: string;
  bootstrapExpiresAt: number;
  expiresAt: number;
};

export class PlaybackRequestError extends Error {
  readonly status: number;
  constructor(status: number) {
    super('Video authorization failed');
    this.status = status;
  }
}

export function isPlaybackSessionId(value: unknown): value is string {
  return typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
}

function internalSecret() {
  const secret = process.env.API_CALLBACK_SECRET || process.env.AI_CALLBACK_SECRET;
  if (!secret) throw new PlaybackRequestError(503);
  return secret;
}

function cookieKey() {
  return createHash('sha256').update(COOKIE_PURPOSE).update(internalSecret()).digest();
}

export function playbackCookieName(sessionId: string) {
  return `ng_video_${sessionId.toLowerCase().replaceAll('-', '')}`;
}

export function sealPlaybackSession(session: BrowserPlaybackSession) {
  const nonce = randomBytes(12);
  const cipher = createCipheriv('aes-256-gcm', cookieKey(), nonce);
  cipher.setAAD(Buffer.from(COOKIE_PURPOSE));
  // Staff JWTs include permissions and can exceed a cookie's limit without compression.
  const serialized = Buffer.from(JSON.stringify(session));
  if (serialized.length > 32 * 1024) throw new PlaybackRequestError(503);
  const encrypted = Buffer.concat([cipher.update(deflateRawSync(serialized)), cipher.final()]);
  const sealed = Buffer.concat([nonce, cipher.getAuthTag(), encrypted]).toString('base64url');
  if (sealed.length > 3800) throw new PlaybackRequestError(503);
  return sealed;
}

export function readPlaybackSession(request: Request, sessionId: string): BrowserPlaybackSession {
  if (!isPlaybackSessionId(sessionId)) throw new PlaybackRequestError(400);
  const prefix = `${playbackCookieName(sessionId)}=`;
  const value = request.headers.get('cookie')?.split(';').map(part => part.trim()).find(part => part.startsWith(prefix))?.slice(prefix.length);
  if (!value || value.length > 3800) throw new PlaybackRequestError(401);
  const key = cookieKey();
  try {
    const bytes = Buffer.from(value, 'base64url');
    if (bytes.length < 29) throw new Error('Invalid cookie');
    const decipher = createDecipheriv('aes-256-gcm', key, bytes.subarray(0, 12));
    decipher.setAAD(Buffer.from(COOKIE_PURPOSE));
    decipher.setAuthTag(bytes.subarray(12, 28));
    const compressed = Buffer.concat([decipher.update(bytes.subarray(28)), decipher.final()]);
    const session = JSON.parse(inflateRawSync(compressed, { maxOutputLength: 32 * 1024 }).toString('utf8')) as BrowserPlaybackSession;
    if (session.sessionId !== sessionId.toLowerCase() || !Number.isFinite(session.expiresAt)
      || session.expiresAt <= Date.now() || !/^Bearer [A-Za-z0-9_.-]+$/.test(session.authorization)) {
      throw new Error('Expired or mismatched cookie');
    }
    return session;
  } catch {
    throw new PlaybackRequestError(401);
  }
}

export function readPlaybackBootstrap(request: Request, sessionId: string): BrowserPlaybackSession {
  const session = readPlaybackSession(request, sessionId);
  if (!Number.isFinite(session.bootstrapExpiresAt) || session.bootstrapExpiresAt <= Date.now()) throw new PlaybackRequestError(401);
  return session;
}

export function createPlaybackCookie(request: Request, sessionId: string, watchExpiresAt: string) {
  const authorization = request.headers.get('authorization') ?? '';
  if (!/^Bearer [A-Za-z0-9_.-]+$/.test(authorization)) throw new PlaybackRequestError(401);
  const expiresAt = Math.min(Date.parse(watchExpiresAt), Date.now() + MEDIA_COOKIE_LIFETIME_MS);
  if (!Number.isFinite(expiresAt) || expiresAt <= Date.now()) throw new PlaybackRequestError(410);
  const value = sealPlaybackSession({ sessionId: sessionId.toLowerCase(), authorization,
    surface: request.headers.get('x-app-surface') ?? '',
    bootstrapExpiresAt: Math.min(expiresAt, Date.now() + BOOTSTRAP_LIFETIME_MS), expiresAt });
  const secure = new URL(request.url).protocol === 'https:' || request.headers.get('x-forwarded-proto')?.split(',')[0].trim() === 'https';
  return `${playbackCookieName(sessionId)}=${value}; Path=/api/video; HttpOnly; SameSite=Strict; Max-Age=${Math.floor((expiresAt - Date.now()) / 1000)}${secure ? '; Secure' : ''}`;
}

export async function fetchPlaybackMaterial(request: Request, sessionId: string, options: {
  authorization: string; surface?: string; includeWatermark?: boolean; nativeHls?: boolean;
}): Promise<PlaybackMaterial> {
  const apiUrl = (process.env.INTERNAL_API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://backend:5245/api').replace(/\/$/, '');
  const query = new URLSearchParams({ includeWatermark: String(Boolean(options.includeWatermark)), nativeHls: String(Boolean(options.nativeHls)) });
  const response = await fetch(`${apiUrl}/v1/internal/video-sessions/${encodeURIComponent(sessionId)}/embed-material?${query}`, {
    headers: { 'X-Internal-Token': internalSecret(), Authorization: options.authorization, 'X-App-Surface': options.surface ?? '' },
    cache: 'no-store', redirect: 'error', signal: AbortSignal.any([request.signal, AbortSignal.timeout(15_000)]),
  });
  if (!response.ok) {
    await response.body?.cancel();
    throw new PlaybackRequestError([401, 403, 404, 409, 410, 429, 503].includes(response.status) ? response.status : 502);
  }
  return response.json() as Promise<PlaybackMaterial>;
}

export function playbackErrorResponse(error: unknown) {
  const status = error instanceof PlaybackRequestError ? error.status : 502;
  return Response.json({ message: status === 401 || status === 403 ? 'تعذر التحقق من صلاحية المشاهدة.' : 'تعذر تجهيز الفيديو. حاول مرة أخرى.' }, {
    status, headers: { 'Cache-Control': 'no-store, private' },
  });
}
