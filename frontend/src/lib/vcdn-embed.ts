import { parseVcdnVideoId } from './vcdn-video-reference.ts';
import { generateDirectHlsEmbedHtml } from './bunny-hls-embed.ts';
import { validateVcdnPlaybackUrl, type VcdnPlaybackSource } from './vcdn-playback-source.ts';

export function generateVcdnEmbedHtml(videoReference: string, studentName: string, studentPhone: string, grant: VcdnPlaybackSource): string {
  if (!parseVcdnVideoId(videoReference)) throw new Error('VCDN requires an authorized signed playback source');
  const playlistUrl = validateVcdnPlaybackUrl(grant.source);
  return generateDirectHlsEmbedHtml({
    playlistUrl, sourceExpiresAtMs: grant.expiresAt, serverNowMs: grant.serverNowMs,
    provider: 'vcdn', studentName, studentPhone,
    vcdnPlatformSession: grant.protection === 'platform-session',
  });
}
