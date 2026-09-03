import apiClient from './api-client';
import { getAccessToken } from '@/lib/auth-memory';
import { getStoredAccessToken } from '@/lib/auth-storage';
import { getSurfaceName } from '@/packages/surface-runtime/config';

const API_BASE_URL = (process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5245/api').replace(/\/+$/, '');

export interface WatchInfo {
  currentCount: number;
  maxCount: number;
  isLocked: boolean;
  totalTrackedSeconds: number;
}

export interface VideoSession {
  sessionId: string;
  expiresAt: string;
  provider: string;
  watchInfo: WatchInfo;
  videoTitle: string;
  thresholdPercentage: number;
  durationSeconds?: number | null;
  isPreview: boolean;
}

export interface TrackProgressRequest {
  lessonVideoId: string;
  sessionId: string;
  progressSequence: number;
  secondsWatched: number;
  playbackRate: number;
  totalDurationSeconds: number;
}

export interface TrackProgressSegmentRequest {
  progressSequence: number;
  secondsWatched: number;
  playbackRate: number;
}

export interface TrackProgressBatchRequest {
  lessonVideoId: string;
  sessionId: string;
  totalDurationSeconds: number;
  progressSegments: TrackProgressSegmentRequest[];
}

export interface WatchProgressResponse {
  currentCount: number;
  maxCount: number;
  isLocked: boolean;
  viewRegistered: boolean;
  sessionHasRegisteredView?: boolean;
  totalTrackedSeconds: number;
  thresholdSeconds: number;
  sessionExpiresAt: string;
  duplicate: boolean;
}

export type ExtraWatchRequestStatus = 'Pending' | 'Approved' | 'Rejected';

export interface ExtraWatchStatusDto {
  canWatch: boolean;
  hasPendingRequest: boolean;
  hasRejectedRequest: boolean;
  requestStatus?: ExtraWatchRequestStatus | null;
  rejectionReason?: string | null;
}

function progressPayload(request: TrackProgressRequest) {
  return {
    sessionId: request.sessionId,
    progressSequence: request.progressSequence,
    secondsWatched: request.secondsWatched,
    playbackRate: request.playbackRate,
    totalDurationSeconds: request.totalDurationSeconds,
  };
}

function progressBatchPayload(request: TrackProgressBatchRequest) {
  return {
    sessionId: request.sessionId,
    totalDurationSeconds: request.totalDurationSeconds,
    progressSegments: request.progressSegments,
  };
}

function postProgressWithKeepalive(
  lessonVideoId: string,
  payload: ReturnType<typeof progressPayload> | ReturnType<typeof progressBatchPayload>,
) {
  const token = getAccessToken() ?? getStoredAccessToken();
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    'X-App-Surface': getSurfaceName(),
  };
  if (token) headers.Authorization = `Bearer ${token}`;

  // Calling fetch directly starts the keepalive request within the pagehide
  // event itself; an async Axios interceptor may not run before a Custom Tab
  // freezes or discards its page.
  return fetch(`${API_BASE_URL}/student/video-session/${encodeURIComponent(lessonVideoId)}/track-progress`, {
    method: 'POST',
    headers,
    credentials: 'include',
    keepalive: true,
    body: JSON.stringify(payload),
  }).then(async (response) => {
    const body = await response.json().catch(() => null) as { data?: WatchProgressResponse; message?: string } | null;
    if (!response.ok) {
      throw {
        message: body?.message || `Progress delivery failed with HTTP ${response.status}`,
        response: { status: response.status, data: body },
      };
    }
    return { data: body as { data: WatchProgressResponse } };
  });
}

function trackProgressWithKeepalive(request: TrackProgressRequest) {
  return postProgressWithKeepalive(request.lessonVideoId, progressPayload(request));
}

function trackProgressBatchWithKeepalive(request: TrackProgressBatchRequest) {
  return postProgressWithKeepalive(request.lessonVideoId, progressBatchPayload(request));
}

export const videoSessionService = {
  createSession: (lessonVideoId: string) => {
    return apiClient.post<{ data: VideoSession }>('/student/video-session', {
      lessonVideoId,
    });
  },

  consumeSession: (sessionId: string) => {
    return apiClient.post(`/student/video-session/${sessionId}/consume`, {});
  },

  requestExtraWatch: (lessonVideoId: string, reason: string) => {
    return apiClient.post(`/student/video-session/${lessonVideoId}/request-extra`, { reason });
  },

  getExtraWatchStatus: (lessonVideoId: string) => {
    return apiClient.get<{ data: ExtraWatchStatusDto }>(`/student/video-session/${lessonVideoId}/request-status`);
  },

  trackProgress: (request: TrackProgressRequest, options?: { keepalive?: boolean }) => {
    if (options?.keepalive) return trackProgressWithKeepalive(request);
    return apiClient.post<{ data: WatchProgressResponse }>(
      `/student/video-session/${request.lessonVideoId}/track-progress`,
      progressPayload(request),
    );
  },

  trackProgressBatch: (request: TrackProgressBatchRequest, options?: { keepalive?: boolean }) => {
    if (options?.keepalive) return trackProgressBatchWithKeepalive(request);
    return apiClient.post<{ data: WatchProgressResponse }>(
      `/student/video-session/${request.lessonVideoId}/track-progress`,
      progressBatchPayload(request),
    );
  },
};
