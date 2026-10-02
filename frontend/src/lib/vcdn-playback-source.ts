import { parseVcdnVideoId } from './vcdn-video-reference.ts';
import { validateVideoMediaRequest } from './video-embed-request-guard.ts';
import { PlaybackRequestError } from './video-playback-session.ts';

export type VcdnPlaybackSource = { source: string; expiresAt: number; serverNowMs: number; protection?: 'platform-session' };
const streamHosts = new Set(['stream.vcdn.me', 'cdn.vcdn.me', 'embed.vcdn.me']);

export function validateVcdnPlaybackUrl(source: string): URL {
  let url: URL;
  try { url = new URL(source); } catch { throw new PlaybackRequestError(502); }
  if (url.protocol !== 'https:' || !streamHosts.has(url.hostname) || url.port || url.username || url.password
    || url.hash || new URL('./', url).pathname === '/' || !url.pathname.endsWith('.m3u8') || !url.searchParams.get('token') || url.searchParams.getAll('token').length !== 1) throw new PlaybackRequestError(502);
  return url;
}

export async function mintVcdnPlaybackSource(request: Request, videoId: string, sessionExpiresAt: number): Promise<VcdnPlaybackSource> {
  const apiKey = protectedApiKey();
  if (parseVcdnVideoId(videoId) !== videoId) throw new PlaybackRequestError(422);
  const serverNowMs = Date.now();
  const ttlSeconds = Math.min(600, Math.floor((sessionExpiresAt - serverNowMs) / 1000));
  if (!Number.isFinite(sessionExpiresAt) || ttlSeconds < 60) throw new PlaybackRequestError(410);
  const context = request.headers.get('origin') || request.headers.get('referer');
  if (!context || validateVideoMediaRequest(request.url, request.headers)) throw new PlaybackRequestError(403);
  const referer = new URL(context).origin + '/';
  if (process.env.VCDN_REFERRER_PROTECTION_ENABLED !== 'true') {
    return resolvePlatformSessionSource(request, { videoId, apiKey, referer, sessionExpiresAt, serverNowMs });
  }
  const payload = await requestPlaybackToken(request, { videoId, apiKey, referer, ttlSeconds });
  return validatedPlaybackSource(payload, videoId, serverNowMs, sessionExpiresAt);
}

function protectedApiKey(): string {
  const apiKey = process.env.VCDN_API_KEY?.trim();
  // Domain defaults alone do not enable strict referrer enforcement on VCDN's CDN.
  if (!apiKey || (process.env.VCDN_REFERRER_PROTECTION_ENABLED !== 'true'
    && process.env.VCDN_PLATFORM_SESSION_PLAYBACK_ENABLED !== 'true')) throw new PlaybackRequestError(503);
  return apiKey;
}

async function resolvePlatformSessionSource(request: Request, options: {
  videoId: string; apiKey: string; referer: string; sessionExpiresAt: number; serverNowMs: number;
}): Promise<VcdnPlaybackSource> {
  const { videoId, apiKey, referer, sessionExpiresAt, serverNowMs } = options;
  const metadata = await fetchVcdnJson(request, `https://cdn.vcdn.me/api/v1/videos/${videoId}`, { 'X-API-Key': apiKey, Referer: referer });
  if (metadata.id !== videoId || metadata.status !== 'ready') throw new PlaybackRequestError(422);
  const config = await fetchVcdnJson(request, `https://embed.vcdn.me/api/bff/player-config/${videoId}`, { Referer: referer });
  if (config.videoId !== videoId || config.mode !== 'hls' || typeof config.streamUrl !== 'string'
    || typeof config.expires !== 'number' || !Number.isSafeInteger(config.expires)) throw new PlaybackRequestError(502);
  const playlistUrl = validateVcdnPlaybackUrl(config.streamUrl);
  if (!['cdn.vcdn.me', 'embed.vcdn.me'].includes(playlistUrl.hostname)
    || !new RegExp(`^/stream/${videoId}/providers/p[1-9][0-9]*/master\\.m3u8$`).test(playlistUrl.pathname)
    || [...playlistUrl.searchParams.keys()].some(key => key !== 'token')) throw new PlaybackRequestError(502);
  const expiresAt = Math.min(config.expires * 1000, sessionExpiresAt, serverNowMs + 180_000);
  if (expiresAt < serverNowMs + 30_000) throw new PlaybackRequestError(410);
  // The provider issues this token publicly. This expiry limits our player session, not copied CDN links.
  return { source: playlistUrl.href, expiresAt, serverNowMs, protection: 'platform-session' };
}

async function fetchVcdnJson(request: Request, endpoint: string, headers: Record<string, string>): Promise<Record<string, unknown>> {
  const response = await fetch(endpoint, { headers, redirect: 'error', cache: 'no-store',
    signal: AbortSignal.any([request.signal, AbortSignal.timeout(10_000)]) });
  if (!response.ok) { await response.body?.cancel(); throw new PlaybackRequestError(response.status === 404 ? 404 : 502); }
  const payload: unknown = await response.json();
  if (!payload || typeof payload !== 'object' || Array.isArray(payload)) throw new PlaybackRequestError(502);
  return payload as Record<string, unknown>;
}

async function requestPlaybackToken(request: Request, options: { videoId: string; apiKey: string; referer: string; ttlSeconds: number }): Promise<unknown> {
  const { videoId, apiKey, referer, ttlSeconds } = options;
  const response = await fetch(`https://cdn.vcdn.me/api/v1/videos/${videoId}/playback-token`, {
    method: 'POST', redirect: 'error', cache: 'no-store',
    signal: AbortSignal.any([request.signal, AbortSignal.timeout(10_000)]),
    headers: { 'X-API-Key': apiKey, 'Content-Type': 'application/json', Referer: referer },
    body: JSON.stringify({ ttlSeconds }),
  });
  if (!response.ok) {
    await response.body?.cancel();
    throw new PlaybackRequestError(response.status === 503 ? 503 : 502);
  }
  return response.json();
}

function validatedPlaybackSource(payload: unknown, videoId: string, serverNowMs: number, sessionExpiresAt: number): VcdnPlaybackSource {
  if (!payload || typeof payload !== 'object') throw new PlaybackRequestError(502);
  const grant = payload as Record<string, unknown>;
  if (typeof grant.streamUrl !== 'string' || typeof grant.token !== 'string' || typeof grant.videoId !== 'string' || grant.videoId.toLowerCase() !== videoId.toLowerCase()
    || typeof grant.exp !== 'number' || !Number.isSafeInteger(grant.exp)) throw new PlaybackRequestError(502);
  const url = validateVcdnPlaybackUrl(grant.streamUrl);
  const expiresAt = grant.exp * 1000;
  if (url.searchParams.get('token') !== grant.token || expiresAt <= serverNowMs || expiresAt > sessionExpiresAt
    || expiresAt > serverNowMs + 601_000) throw new PlaybackRequestError(502);
  return { source: url.toString(), expiresAt, serverNowMs };
}
