import { decryptVideoEmbedMaterial } from '@/lib/video-embed-material';
import { validateVideoMediaRequest } from '@/lib/video-embed-request-guard';
import { createPlaybackCookie, fetchPlaybackMaterial, isPlaybackSessionId, playbackCookieName, playbackErrorResponse, PlaybackRequestError } from '@/lib/video-playback-session';

export async function POST(request: Request) {
  try {
    if (validateVideoMediaRequest(request.url, request.headers)) throw new PlaybackRequestError(403);
    const body: unknown = await request.json();
    if (!body || typeof body !== 'object' || Array.isArray(body)) throw new PlaybackRequestError(400);
    const { sessionId, purpose, nativeHls } = body as Record<string, unknown>;
    if (!isPlaybackSessionId(sessionId) || (purpose !== 'start' && purpose !== 'renew')
      || (nativeHls !== undefined && typeof nativeHls !== 'boolean')) throw new PlaybackRequestError(400);
    const authorization = request.headers.get('authorization') ?? '';
    if (!/^Bearer [A-Za-z0-9_.-]+$/.test(authorization)) throw new PlaybackRequestError(401);
    const material = await fetchPlaybackMaterial(request, sessionId, {
      authorization, surface: request.headers.get('x-app-surface') ?? '', nativeHls: nativeHls === true,
    });
    const expiresAt = material.expiresAt ?? material.ExpiresAt ?? '';
    const headers = { 'Cache-Control': 'no-store, private', 'Set-Cookie': createPlaybackCookie(request, sessionId, expiresAt) };
    if (purpose === 'start') return Response.json({ data: { expiresAt } }, { headers });
    const video = decryptVideoEmbedMaterial(material);
    if (video.Provider?.toLowerCase() !== 'bunny-hls') throw new PlaybackRequestError(400);
    const signedSourceExpiresAtMs = Number(new URL(video.VideoId).pathname.match(/(?:^|&)expires=(\d+)(?:&|$)/)?.[1]) * 1000;
    if (!Number.isFinite(signedSourceExpiresAtMs) || signedSourceExpiresAtMs <= Date.now()) throw new PlaybackRequestError(410);
    return Response.json({ data: { source: video.VideoId, serverNowMs: Date.now(), signedSourceExpiresAtMs, sessionExpiresAtMs: Date.parse(expiresAt) } }, { headers });
  } catch (error) {
    return playbackErrorResponse(error instanceof SyntaxError ? new PlaybackRequestError(400) : error);
  }
}

export async function DELETE(request: Request) {
  if (validateVideoMediaRequest(request.url, request.headers)) return playbackErrorResponse(new PlaybackRequestError(403));
  const sessionId = new URL(request.url).searchParams.get('s');
  if (sessionId !== null && !isPlaybackSessionId(sessionId)) return playbackErrorResponse(new PlaybackRequestError(400));
  const headers = new Headers({ 'Cache-Control': 'no-store, private' });
  for (const part of (request.headers.get('cookie') ?? '').split(';')) {
    const name = part.trim().split('=')[0];
    if (/^ng_video_[a-f0-9]{32}$/.test(name) && (sessionId === null || name === playbackCookieName(sessionId))) {
      headers.append('Set-Cookie', `${name}=; Path=/api/video; HttpOnly; SameSite=Strict; Max-Age=0`);
    }
  }
  return new Response(null, { status: 204, headers });
}
