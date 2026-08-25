import apiClient from './api-client';
import type { AxiosResponse } from 'axios';
import type { AiOutputLanguage } from '@/lib/ai-output-language';
import { isFullPackagePurchaseDisabled } from '@/lib/content-access';

export type { AiOutputLanguage } from '@/lib/ai-output-language';

export const CONTENT_CACHE_KEYS = {
  packages: 'content:packages',
  lessons: 'content:lessons',
} as const;

export type PackageContentMode =
  | 'TermWithSections'
  | 'SectionWithLessons'
  | 'LessonsOnly'
  | 'SingleLesson';

export type ContentArchiveMode = 'None' | 'ActiveSubscribersOnly' | 'HiddenFromEveryone';

export type ContentRootLabel = 'باقة' | 'ترم' | 'قسم' | 'حصة';

export type PackageContentModeOption = {
  value: PackageContentMode;
  entityLabel: ContentRootLabel;
  label: string;
  description: string;
};

const PACKAGE_CONTENT_MODE_OPTION_MAP: Record<PackageContentMode, PackageContentModeOption> = {
  TermWithSections: { value: 'TermWithSections', entityLabel: 'باقة', label: 'باقة كاملة: باقة ← ترم ← قسم ← حصص', description: 'أنشئ باقة كاملة، ثم أضف داخلها ترمات وأقسامًا وحصصًا.' },
  SectionWithLessons: { value: 'SectionWithLessons', entityLabel: 'ترم', label: 'ترم مستقل: ترم ← قسم ← حصص', description: 'أنشئ ترمًا للبيع مباشرة، ثم أضف داخله الأقسام والحصص.' },
  LessonsOnly: { value: 'LessonsOnly', entityLabel: 'قسم', label: 'قسم مستقل: قسم ← حصص', description: 'أنشئ قسمًا للبيع مباشرة، ثم أضف داخله الحصص.' },
  SingleLesson: { value: 'SingleLesson', entityLabel: 'حصة', label: 'حصة مستقلة', description: 'أنشئ حصة مستقلة جاهزة لإضافة الفيديوهات والملفات.' },
};

export const PACKAGE_CONTENT_MODE_OPTIONS = Object.values(PACKAGE_CONTENT_MODE_OPTION_MAP);

export function getContentRootOption(contentMode: PackageContentMode): PackageContentModeOption {
  return PACKAGE_CONTENT_MODE_OPTION_MAP[contentMode];
}

export function getContentRootLabel(contentMode: PackageContentMode): ContentRootLabel {
  return getContentRootOption(contentMode).entityLabel;
}

export interface PackageDto {
  id: string;
  name: string;
  description: string;
  price: number;
  programId: string;
  isEnrolled: boolean;
  hasDirectPackageAccess?: boolean;
  hasRootContentAccess?: boolean;
  imageUrl?: string;
  teacherId?: string;
  subjectId?: string;
  teacherName?: string;
  teacherProfileImageUrl?: string;
  subjectName?: string;
  teacherBio?: string;
  teacherSpecialization?: string;
  targetGrade?: string;
  aiOutputLanguage?: AiOutputLanguage;
  contentMode?: PackageContentMode;
  allowFullPackagePurchase?: boolean;
  rootTermId?: string;
  rootSectionId?: string;
  directSections?: PackageDirectSectionDto[];
  directLessons?: PackageDirectLessonDto[];
  archiveMode?: ContentArchiveMode;
  archivedAt?: string | null;
}

export type ContentRootPurchaseReference = {
  contentType: 'Package' | 'Term' | 'Month' | 'Lesson';
  contentId: string;
};

export function getContentRootPurchaseReference(pkg: PackageDto): ContentRootPurchaseReference | null {
  if (isFullPackagePurchaseDisabled(pkg)) {
    return null;
  }

  switch (pkg.contentMode ?? 'TermWithSections') {
    case 'SectionWithLessons':
      return pkg.rootTermId ? { contentType: 'Term', contentId: pkg.rootTermId } : null;
    case 'LessonsOnly':
      return pkg.rootSectionId ? { contentType: 'Month', contentId: pkg.rootSectionId } : null;
    case 'SingleLesson':
      return pkg.directLessons?.[0]
        ? { contentType: 'Lesson', contentId: pkg.directLessons[0].id }
        : null;
    default:
      return { contentType: 'Package', contentId: pkg.id };
  }
}

export interface TermDto {
  id: string;
  title: string;
  order: number;
  price?: number;
  imageUrl?: string;
  isPurchased?: boolean;
  archiveMode?: ContentArchiveMode;
  archivedAt?: string | null;
}

export interface ContentSectionDto {
  id: string;
  title: string;
  order: number;
  price?: number;
  imageUrl?: string;
  isPurchased?: boolean;
  archiveMode?: ContentArchiveMode;
  archivedAt?: string | null;
}

export interface PackageDirectSectionDto {
  id: string;
  title: string;
  order: number;
  price?: number;
  imageUrl?: string;
  isPurchased?: boolean;
  archiveMode?: ContentArchiveMode;
  archivedAt?: string | null;
}

export interface PackageDirectLessonDto {
  id: string;
  title: string;
  summary: string;
  order: number;
  price?: number;
  hasAccess?: boolean;
  archiveMode?: ContentArchiveMode;
  archivedAt?: string | null;
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
  archiveMode?: ContentArchiveMode;
  archivedAt?: string | null;
}

export interface LessonVideoSummaryDto {
  id: string;
  title: string;
  order: number;
  hasAccess: boolean;
  isUnlockedByCode?: boolean;
  videoTypeId?: string;
  videoTypeName?: string;
  archiveMode?: ContentArchiveMode;
  archivedAt?: string | null;
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

export interface ContentAcquisitionCountDto {
  purchased: number;
  gifts: number;
}

export interface ContentPackageSummaryDto {
  packageId: string;
  packageName: string;
  teacherName: string;
  package: ContentAcquisitionCountDto;
  term: ContentAcquisitionCountDto;
  section: ContentAcquisitionCountDto;
  lesson: ContentAcquisitionCountDto;
  purchasedStudents: number;
  giftStudents: number;
  totalStudents: number;
}

export interface PackageCombinationSummaryDto {
  packageIds: string[];
  packageNames: string[];
  studentsCount: number;
}

export interface ContentSummaryDto {
  fromUtc?: string | null;
  toUtc?: string | null;
  packages: ContentPackageSummaryDto[];
  packageCombinations: PackageCombinationSummaryDto[];
}

export interface ContentSummaryTeacherDto {
  id: string;
  fullName: string;
  profileImageUrl?: string;
  specialization: string;
  subjectIds: string[];
  subjectNames: string[];
  packagesCount: number;
}

export interface ContentSummaryRequest {
  teacherId?: string;
  fromUtc?: string;
  toUtc?: string;
  signal?: AbortSignal;
}

type ContentListApiResponse<T> = Omit<ContentApiResponse<T[]>, 'data'> & { data: T[] };

type PackagesResponse = AxiosResponse<ContentApiResponse<PackageDto[]>>;

export const contentService = {
  // Kept source-compatible for callers that still pass force while the old
  // module-level cache is retired. Every request now reads current server data.
  getPackages: (options?: { force?: boolean; signal?: AbortSignal }): Promise<PackagesResponse> => {
    return apiClient.get('/content/packages', { signal: options?.signal });
  },
  getTerms: (packageId: string, includeSystemContainers = false) =>
    apiClient.get<ContentListApiResponse<TermDto>>(`/content/packages/${packageId}/terms`, {
      params: includeSystemContainers ? { includeSystemContainers: true } : undefined,
    }),
  getPackageCodePage: (packageId: string) => apiClient.get<ContentApiResponse<PackageCodePageDto>>(`/content/packages/${packageId}/code-page`),
  getSections: (termId: string) => apiClient.get<ContentListApiResponse<ContentSectionDto>>(`/content/terms/${termId}/sections`),
  getLessons: (sectionId: string) => apiClient.get<ContentListApiResponse<LessonSummaryDto>>(`/content/sections/${sectionId}/lessons`),
  getLessonDetail: (lessonId: string) => apiClient.get<ContentApiResponse<LessonDetailDto>>(`/content/lessons/${lessonId}`),
  getLessonComments: (lessonId: string, offset = 0, limit = 50) => apiClient.get<ContentApiResponse<LessonCommentDto[]>>(`/content/lessons/${lessonId}/comments?offset=${offset}&limit=${limit}`),
  getLessonResources: (lessonId: string) => apiClient.get<ContentApiResponse<ResourceDto[]>>(`/content/lessons/${lessonId}/resources`),
  getMyLessonComments: (lessonId: string) => apiClient.get<ContentApiResponse<LessonCommentDto[]>>(`/content/lessons/${lessonId}/comments/mine`),
  createLessonComment: (lessonId: string, body: string) =>
    apiClient.post<ContentApiResponse<CreateLessonCommentResponse>>(`/content/lessons/${lessonId}/comments`, { body }),
  getContentSummaryTeachers: (signal?: AbortSignal) =>
    apiClient.get<ContentApiResponse<ContentSummaryTeacherDto[]>>('/admin/content/summary/teachers', { signal }),
  getContentSummary: (scope: 'admin' | 'teacher', options: ContentSummaryRequest = {}) =>
    apiClient.get<ContentApiResponse<ContentSummaryDto>>(`/${scope}/content/summary`, {
      params: {
        teacherId: scope === 'admin' ? options.teacherId : undefined,
        fromUtc: options.fromUtc,
        toUtc: options.toUtc,
      },
      signal: options.signal,
    }),
};
