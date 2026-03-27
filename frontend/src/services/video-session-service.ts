import apiClient from './api-client';

export interface WatchInfo {
  currentCount: number;
  maxCount: number;
  isLocked: boolean;
}

export interface VideoSession {
  sessionId: string;
  token: string;
  key: string;
  expiresAt: string;
  provider: string;
  watchInfo: WatchInfo;
  videoTitle: string;
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

  trackProgress: (lessonVideoId: string, secondsWatched: number, registerView: boolean = false) => {
    return apiClient.post(`/student/video-session/${lessonVideoId}/track-progress`, {
      secondsWatched,
      registerView
    });
  },
};
