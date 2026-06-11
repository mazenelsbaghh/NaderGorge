import apiClient from './api-client';
import { registerCacheStore } from '@/lib/cache-invalidation';
import type { AxiosResponse } from 'axios';

export interface PackageDto {
  id: string;
  name: string;
  description: string;
  price: number;
  programId: string;
  isEnrolled: boolean;
  imageUrl?: string;
  teacherId?: string;
  subjectId?: string;
  teacherName?: string;
  teacherProfileImageUrl?: string;
  subjectName?: string;
  teacherBio?: string;
  teacherSpecialization?: string;
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
  isLocked?: boolean;
  lockedReason?: string;
  blockingExamId?: string;
  blockingHomeworkLessonId?: string;
}

export interface VideoChapterDto {
  id: string;
  title: string;
  startTime: number;
  endTime: number;
  summaryText: string;
  order: number;
  mindmapImageUrl?: string;
}

export interface VideoDto {
  id: string;
  title: string;
  provider: string;
  order: number;
  limit: number;
  watched: number;
  isLocked: boolean;
  watchedSeconds: number;
  lastWatchedAt?: string;
  subtitleUrl?: string;
  isProcessingAI?: boolean;
  isProcessingMindmaps?: boolean;
  examId?: string;
  examPassed?: boolean;
  isExamLocked?: boolean;
  chapters?: VideoChapterDto[];
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
  totalScore?: number;
  questions: HomeworkQuestionDto[];
}

export interface LessonDetailDto {
  id: string;
  title: string;
  summary: string;
  packageId: string;
  examId?: string;
  videos: VideoDto[];
  resources?: ResourceDto[];
  homework?: HomeworkDto;
  isLocked?: boolean;
  lockedReason?: string;
  blockingExamId?: string;
  blockingHomeworkLessonId?: string;
}

export interface LessonCommentDto {
  id: string;
  lessonId: string;
  authorName: string;
  body: string;
  status: string;
  createdAt: string;
  isOwnComment: boolean;
  authorAvatarSlug?: string | null;
}

export interface CreateLessonCommentResponse {
  id: string;
  status: string;
  createdAt: string;
  message: string;
}

export interface PackageCodePageHeroDto {
  eyebrow: string;
  title: string;
  description: string;
}

export interface PackageCodePagePanelDto {
  title: string;
  description: string;
}

export interface PackageCodePageDto {
  packageId: string;
  packageName: string;
  packageDescription: string;
  packagePrice: number;
  isPackageActive: boolean;
  isUsingCustomProfile: boolean;
  hero: PackageCodePageHeroDto;
  offerPanel: PackageCodePagePanelDto;
  activationPanel: PackageCodePagePanelDto;
  supportPanel: PackageCodePagePanelDto;
  themeAccentKey: string;
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

const readPackagesFromCache = () => packagesCache?.data?.data ?? [];

export const contentService = {
  clearPackagesCache: () => {
    packagesCache = null;
    packagesCacheAt = 0;
    packagesInFlight = null;
  },
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
  peekCachedPackageById: (packageId: string) => {
    if (!packageId) return null;
    return readPackagesFromCache().find((pkg) => pkg.id === packageId) ?? null;
  },
  getTerms: (packageId: string) => apiClient.get(`/content/packages/${packageId}/terms`),
  getPackageCodePage: (packageId: string) => apiClient.get<ContentApiResponse<PackageCodePageDto>>(`/content/packages/${packageId}/code-page`),
  getSections: (termId: string) => apiClient.get(`/content/terms/${termId}/sections`),
  getLessons: (sectionId: string) => apiClient.get(`/content/sections/${sectionId}/lessons`),
  getLessonDetail: (lessonId: string) => apiClient.get<ContentApiResponse<LessonDetailDto>>(`/content/lessons/${lessonId}`),
  getLessonComments: (lessonId: string, offset = 0, limit = 50) => apiClient.get<ContentApiResponse<LessonCommentDto[]>>(`/content/lessons/${lessonId}/comments?offset=${offset}&limit=${limit}`),
  getLessonResources: (lessonId: string) => apiClient.get<ContentApiResponse<ResourceDto[]>>(`/content/lessons/${lessonId}/resources`),
  getMyLessonComments: (lessonId: string) => apiClient.get<ContentApiResponse<LessonCommentDto[]>>(`/content/lessons/${lessonId}/comments/mine`),
  createLessonComment: (lessonId: string, body: string) =>
    apiClient.post<ContentApiResponse<CreateLessonCommentResponse>>(`/content/lessons/${lessonId}/comments`, { body }),
  recordVideoEvent: (lessonVideoId: string, watchedSeconds: number, totalDurationSeconds = 0) => 
    apiClient.post('/tracking/video-event', { lessonVideoId, watchedSeconds, totalDurationSeconds }),
};

// Register with centralized cache invalidation registry
registerCacheStore(
  'content:packages',
  () => contentService.clearPackagesCache(),
  () => void contentService.getPackages({ force: true })
);
