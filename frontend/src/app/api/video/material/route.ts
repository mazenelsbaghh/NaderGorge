import { decryptVideoEmbedMaterial } from '@/lib/video-embed-material';
import { generateVideoEmbedHtml } from '@/lib/video-embed-html';
import { generateBunnyHlsEmbedHtml } from '@/lib/bunny-hls-embed';
import { configureWatermarkHtml } from '@/lib/video-watermark';
import { validateVideoMediaRequest } from '@/lib/video-embed-request-guard';
import { fetchPlaybackMaterial, playbackErrorResponse, PlaybackRequestError, readPlaybackBootstrap } from '@/lib/video-playback-session';
import { videoPlayerResponse } from '@/lib/video-player-response';

export async function GET(request: Request) {
  try {
    if (validateVideoMediaRequest(request.url, request.headers)) throw new PlaybackRequestError(403);
    const sessionId = new URL(request.url).searchParams.get('s') ?? '';
    const browserSession = readPlaybackBootstrap(request, sessionId);
    const material = await fetchPlaybackMaterial(request, sessionId, { ...browserSession, includeWatermark: true });
    const video = decryptVideoEmbedMaterial(material);
    const provider = video.Provider?.toLowerCase() || 'youtube';
    const name = video.StudentName || 'Massar Academy';
    const phone = video.StudentPhone || '';
    const html = provider === 'bunny-hls'
      ? generateBunnyHlsEmbedHtml(video.VideoId, name, phone, { relaySource: `/api/video/hls?s=${encodeURIComponent(sessionId)}` })
      : generateVideoEmbedHtml(provider, video.VideoId, {
        youtubeQualityEnabled: (material.youTubeQualityEnabled ?? material.YouTubeQualityEnabled) === true,
        studentName: name, studentPhone: phone, bunnyEmbedQuery: material.bunnyEmbedQuery ?? material.BunnyEmbedQuery,
      });
    return videoPlayerResponse(configureWatermarkHtml(html, material.watermarkSettings ?? {}, {
      name, phone, studentId: material.studentId ?? '',
    }));
  } catch (error) {
    return playbackErrorResponse(error);
  }
}
