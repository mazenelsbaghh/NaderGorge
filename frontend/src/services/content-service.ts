import apiClient from './api-client';
import type { AxiosResponse } from 'axios';

export const CONTENT_CACHE_KEYS = {
  packages: 'content:packages',
  lessons: 'content:lessons',
} as const;

export interface PackageDto {
  id: string;
  name: string;
  description: string;
  price: number;
  programId: string;
  isEnrolled: boolean;
  hasDirectPackageAccess?: boolean;
  imageUrl?: string;
  teacherId?: string;
  subjectId?: string;
  teacherName?: string;
  teacherProfileImageUrl?: string;
  subjectName?: string;
  teacherBio?: string;
  teacherSpecialization?: string;
  targetGrade?: string;
}

export interface TermDto {
  id: string;
  title: string;
  order: number;
  price?: number;
  imageUrl?: string;
  isPurchased?: boolean;
}

export interface ContentSectionDto {
  id: string;
  title: string;
  order: number;
  price?: number;
  imageUrl?: string;
  isPurchased?: boolean;
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
  videos?: LessonVideoSummaryDto[];
}

export interface LessonVideoSummaryDto {
  id: string;
  title: string;
  order: number;
  hasAccess: boolean;
  isUnlockedByCode?: boolean;
  videoTypeId?: string;
  videoTypeName?: string;
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
  hasAccess?: boolean;
  isUnlockedByCode?: boolean;
  unlockLabel?: string;
  videoTypeId?: string;
  videoTypeName?: string;
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
  questionType?: string;
  possibleAnswers?: string[];
  correctAnswerKey?: string;
  audioUrl?: string;
  writtenCorrection?: string;
  hintText?: string;
  baseText?: string;
  mistakeStartIndex?: number | null;
  mistakeEndIndex?: number | null;
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
  examPassed?: boolean;
  homeworkId?: string;
  homeworkPassed?: boolean;
  videos: VideoDto[];
  resources?: ResourceDto[];
  homework?: HomeworkDto;
  isLocked?: boolean;
  lockedReason?: string;
  blockingExamId?: string;
  blockingHomeworkLessonId?: string;
  price?: number;
  hasAccess?: boolean;
  isExamLocked?: boolean;
  examLockedReason?: string;
  examStatus?: string;
  homeworkStatus?: string;
  termId?: string;
  sectionId?: string;
  isVideoOnlyAccess?: boolean;
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

type PackagesResponse = AxiosResponse<ContentApiResponse<PackageDto[]>>;

export const contentService = {
  // Kept source-compatible for callers that still pass force while the old
  // module-level cache is retired. Every request now reads current server data.
  getPackages: (options?: { force?: boolean }): Promise<PackagesResponse> => {
    void options;
    return apiClient.get('/content/packages');
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
};
