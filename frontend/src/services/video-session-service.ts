import apiClient from './api-client';

export interface WatchInfo {
  currentCount: number;
  maxCount: number;
  isLocked: boolean;
  totalTrackedSeconds: number;
}

export interface VideoSession {
  sessionId: string;
  token: string;
  key: string;
  expiresAt: string;
  provider: string;
  watchInfo: WatchInfo;
  videoTitle: string;
  thresholdPercentage: number;
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

  trackProgress: (lessonVideoId: string, secondsWatched: number, totalDurationSeconds: number, registerView: boolean = false) => {
    return apiClient.post(`/student/video-session/${lessonVideoId}/track-progress`, {
      secondsWatched,
      totalDurationSeconds,
      registerView
    });
  },
};
