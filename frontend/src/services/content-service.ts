import apiClient from './api-client';

export interface PackageDto {
  id: string;
  name: string;
  description: string;
  price: number;
  programId: string;
  isEnrolled: boolean;
}

export interface ContentSectionDto {
  id: string;
  title: string;
  order: number;
}

export interface LessonSummaryDto {
  id: string;
  title: string;
  summary: string;
  order: number;
  hasAccess: boolean;
  isCompleted: boolean;
}

export interface VideoDto {
  id: string;
  title: string;
  provider: string;
  embedUrl: string;
  order: number;
  limit: number;
  watched: number;
  isLocked: boolean;
}

export interface ResourceDto {
  id: string;
  title: string;
  fileUrl: string;
  type: string;
}

export interface LessonDetailDto {
  id: string;
  title: string;
  summary: string;
  examId?: string;
  videos: VideoDto[];
  resources: ResourceDto[];
}

export const contentService = {
  getPackages: () => apiClient.get('/content/packages'),
  getSections: (packageId: string) => apiClient.get(`/content/packages/${packageId}/sections`),
  getLessons: (sectionId: string) => apiClient.get(`/content/sections/${sectionId}/lessons`),
  getLessonDetail: (lessonId: string) => apiClient.get(`/content/lessons/${lessonId}`),
  recordVideoEvent: (lessonVideoId: string, watchedSeconds: number) => 
    apiClient.post('/tracking/video-event', { lessonVideoId, watchedSeconds }),
};
