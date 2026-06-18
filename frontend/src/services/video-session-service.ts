import apiClient from './api-client';

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
}

export interface TrackProgressRequest {
  lessonVideoId: string;
  sessionId: string;
  progressSequence: number;
  secondsWatched: number;
  totalDurationSeconds: number;
}

export interface WatchProgressResponse {
  currentCount: number;
  maxCount: number;
  isLocked: boolean;
  viewRegistered: boolean;
  totalTrackedSeconds: number;
  thresholdSeconds: number;
  sessionExpiresAt: string;
  duplicate: boolean;
}

export type ExtraWatchRequestStatus = 'Pending' | 'Approved' | 'Rejected';

export interface ExtraWatchStatusDto {
  hasPendingRequest: boolean;
  hasRejectedRequest: boolean;
  requestStatus?: ExtraWatchRequestStatus | null;
  rejectionReason?: string | null;
}

export const videoSessionService = {
  createSession: (lessonVideoId: string) => {
    return apiClient.post('/student/video-session', {
      lessonVideoId,
    });
  },

  consumeSession: (sessionId: string) => {
    return apiClient.post(`/student/video-session/${sessionId}/consume`, {});
  },

  requestExtraWatch: (lessonVideoId: string) => {
    return apiClient.post(`/student/video-session/${lessonVideoId}/request-extra`, {});
  },

  getExtraWatchStatus: (lessonVideoId: string) => {
    return apiClient.get<{ data: ExtraWatchStatusDto }>(`/student/video-session/${lessonVideoId}/request-status`);
  },

  trackProgress: (request: TrackProgressRequest) => {
    return apiClient.post<{ data: WatchProgressResponse }>(`/student/video-session/${request.lessonVideoId}/track-progress`, {
      sessionId: request.sessionId,
      progressSequence: request.progressSequence,
      secondsWatched: request.secondsWatched,
      totalDurationSeconds: request.totalDurationSeconds,
    });
  },
};
