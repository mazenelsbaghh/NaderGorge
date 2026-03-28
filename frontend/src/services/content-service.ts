import apiClient from './api-client';
import type { AxiosResponse } from 'axios';

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
  price?: number;
}

export interface ContentSectionDto {
  id: string;
  title: string;
  order: number;
  price?: number;
}

export interface LessonSummaryDto {
  id: string;
  title: string;
  summary: string;
  order: number;
  hasAccess: boolean;
  isCompleted: boolean;
  price?: number;
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

interface ContentApiResponse<T> {
  success?: boolean;
  message?: string;
  data?: T;
}

const PACKAGES_CACHE_TTL_MS = 10_000;
type PackagesResponse = AxiosResponse<ContentApiResponse<PackageDto[]>>;

let packagesInFlight: Promise<PackagesResponse> | null = null;
let packagesCache: PackagesResponse | null = null;
let packagesCacheAt = 0;

export const contentService = {
  getPackages: (options?: { force?: boolean }) => {
    const force = options?.force ?? false;
    const isCacheFresh = !force && packagesCache && Date.now() - packagesCacheAt < PACKAGES_CACHE_TTL_MS;

    if (isCacheFresh && packagesCache) {
      return Promise.resolve(packagesCache);
    }

    if (!force && packagesInFlight) {
      return packagesInFlight;
    }

    packagesInFlight = apiClient.get('/content/packages').then((response) => {
      packagesCache = response;
      packagesCacheAt = Date.now();
      return response;
    }).finally(() => {
      packagesInFlight = null;
    });

    return packagesInFlight;
  },
  getTerms: (packageId: string) => apiClient.get(`/content/packages/${packageId}/terms`),
  getSections: (termId: string) => apiClient.get(`/content/terms/${termId}/sections`),
  getLessons: (sectionId: string) => apiClient.get(`/content/sections/${sectionId}/lessons`),
  getLessonDetail: (lessonId: string) => apiClient.get(`/content/lessons/${lessonId}`),
  recordVideoEvent: (lessonVideoId: string, watchedSeconds: number) => 
    apiClient.post('/tracking/video-event', { lessonVideoId, watchedSeconds }),
};
