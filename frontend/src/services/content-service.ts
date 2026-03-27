import apiClient from './api-client';

export interface PackageDto {
  id: string;
  name: string;
  description: string;
  price: number;
  programId: string;
  isEnrolled: boolean;
  imageUrl?: string;
}

export interface TermDto {
  id: string;
  title: string;
  order: number;
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

export interface HomeworkQuestionDto {
  id: string;
  text: string;
  order: number;
  maxPoints: number;
  questionType?: string; // Currently missing from backend, default to 'Essay' in component
}

export interface HomeworkDto {
  id: string;
  title: string;
  instructions: string;
  isMandatory: boolean;
  requiredPointsToPass: number;
  questions: HomeworkQuestionDto[];
}

export interface LessonDetailDto {
  id: string;
  title: string;
  summary: string;
  examId?: string;
  videos: VideoDto[];
  resources: ResourceDto[];
  homework?: HomeworkDto;
}

export const contentService = {
  getPackages: () => apiClient.get('/content/packages'),
  getTerms: (packageId: string) => apiClient.get(`/content/packages/${packageId}/terms`),
  getSections: (termId: string) => apiClient.get(`/content/terms/${termId}/sections`),
  getLessons: (sectionId: string) => apiClient.get(`/content/sections/${sectionId}/lessons`),
  getLessonDetail: (lessonId: string) => apiClient.get(`/content/lessons/${lessonId}`),
  recordVideoEvent: (lessonVideoId: string, watchedSeconds: number) => 
    apiClient.post('/tracking/video-event', { lessonVideoId, watchedSeconds }),
};
