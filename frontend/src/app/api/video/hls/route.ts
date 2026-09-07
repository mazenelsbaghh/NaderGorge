import { bunnyHlsResource, bunnyHlsRoot, relayBunnyResource } from '@/lib/bunny-hls-relay';
import { decryptVideoEmbedMaterial } from '@/lib/video-embed-material';
import { validateVideoMediaRequest } from '@/lib/video-embed-request-guard';

export async function GET(request: Request): Promise<Response> {
  if (validateVideoMediaRequest(request.url, request.headers)) return new Response(null, { status: 403 });
  const params = new URL(request.url).searchParams;
  const sessionId = params.get('s') ?? '';
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(sessionId)) {
    return new Response(null, { status: 400 });
  }
  const secret = process.env.API_CALLBACK_SECRET || process.env.AI_CALLBACK_SECRET;
  if (!secret) return new Response(null, { status: 503 });
  const apiUrl = (process.env.INTERNAL_API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://backend:5245/api').replace(/\/$/, '');
  try {
    // Validate expiry/supersession for every resource; a relay URL grants no extra lifetime.
    const material = await fetch(`${apiUrl}/v1/internal/video-sessions/${sessionId}/embed-material`, {
      headers: { 'X-Internal-Token': secret }, cache: 'no-store', redirect: 'error',
      signal: AbortSignal.any([request.signal, AbortSignal.timeout(15_000)]),
    });
    if (!material.ok) return new Response(null, { status: material.status === 404 ? 410 : 502 });
    const video = decryptVideoEmbedMaterial(await material.json());
    if (video.Provider?.toLowerCase() !== 'bunny-hls') return new Response(null, { status: 403 });
    const root = bunnyHlsRoot(video.VideoId);
    const upstream = bunnyHlsResource(root, params.get('path') ?? 'playlist.m3u8');
    return await relayBunnyResource(upstream, sessionId, root, request);
  } catch {
    // Never log signed CDN URLs or return upstream/internal exception details.
    return new Response(null, { status: 502, headers: { 'Cache-Control': 'no-store' } });
  }
}
