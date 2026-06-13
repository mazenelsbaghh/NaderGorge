import apiClient from './api-client';
import { getSurfaceName } from '@/packages/surface-runtime/config';

export interface ApiResponse<T = any> {
  data: T;
  success: boolean;
  message: string;
}

export type ContentImageType = 'package' | 'term' | 'section';

export interface AdminUserListDto {
  id: string;
  phoneNumber: string;
  status: string;
  fullName: string;
  grade: string;
  track: string;
  createdAt: string;
  roles: string[];
  studentCode?: string;
  dateOfBirth?: string;
  gender?: string;
  educationStage?: string;
  isFatherAlive?: boolean;
  isMotherAlive?: boolean;
  governorate?: string;
  district?: string;                    // NEW
  address?: string;
  secondaryPhone?: string;              // NEW
  secondaryParentPhone?: string;        // NEW
  parentPhone?: string;
  motherPhone?: string;
  schoolName?: string;
  schoolType?: string;
  nationality?: string;
  fatherDateOfBirth?: string;
  motherDateOfBirth?: string;
  suspensionReason?: string;
  currentBalance?: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface DeviceDto {
  id: string;
  fingerprint: string;
  browser: string;
  os: string;
  lastUsedAt: string;
  isActive: boolean;
}

export interface UserAuditLogDto {
  id: string;
  action: string;
  entityType: string;
  entityId?: string;
  oldValues?: string;
  newValues?: string;
  ipAddress?: string;
  createdAt: string;
}

export interface CodeGroupDto {
  id: string;
  name: string;
  createdAt: string;
  packageId?: string;
  lessonId?: string;
  codeCount: number;
  usedCount: number;
  teacherId: string;
}

export interface CodeDetailDto {
  code: string;
  serialNumber: number;
  isUsed: boolean;
  usedAt?: string;
  usedByUserId?: string;
  usedByStudentName?: string | null;
  usedByStudentPhone?: string | null;
}

const CODE_GROUPS_CACHE_TTL_MS = 10_000;
let codeGroupsInFlight: Promise<CodeGroupDto[] | undefined> | null = null;
let codeGroupsCache: CodeGroupDto[] | undefined;
let codeGroupsCacheAt = 0;

export interface QuestionOptionDto {
  id: string;
  text: string;
  isCorrect: boolean;
}

export interface QuestionBankItemDto {
  id: string;
  text: string;
  type: number;
  defaultPoints: number;
  tags: string;
  audioUrl?: string; // New: Optional audio
  writtenCorrection?: string; // New: Text correction
  hintText?: string; // New: Hint text available during practice
  baseText?: string; // For FindTheMistake
  mistakeStartIndex?: number; // For FindTheMistake
  mistakeEndIndex?: number; // For FindTheMistake
  options: QuestionOptionDto[];
  createdByTeacherId: string;
  subjectId: string;
}

export interface StudentPackageDto {
  id: string;
  accessGrantId: string;
  name: string;
  enrolledAt: string;
  expiresAt?: string | null;
  progress: number;
  isActive: boolean;
  purchaseMethod: string;
  price: number;
}

export interface StudentDeviceProfileDto {
  id: string;
  deviceName: string;
  lastActiveAt: string;
  isActive: boolean;
}

export interface StudentVideoOverrideDto {
  id: string;
  videoId: string;
  videoTitle?: string;
  originalLimit?: number;
  newLimit?: number;
  currentViews?: number;
  overrideBy?: string;
  createdAt?: string;
  addedViews?: number;
  reason?: string;
}

export interface StudentAuditLogDto {
  id: string;
  adminName: string;
  action: string;
  date: string;
  details: string | Record<string, unknown>;
  entityType?: string;
  entityId?: string;
  oldValues?: string;
  newValues?: string;
  ipAddress?: string;
}

export interface BalanceTransactionDto {
  id: string;
  amount: number;
  balanceAfter: number;
  transactionType: string;
  description: string;
  createdAt: string;
  adminName: string;
}

export interface AdminWatchRequestDto {
  id: string;
  userId: string;
  studentName: string;
  studentPhone: string;
  lessonVideoId: string;
  videoTitle: string;
  status: number;
  createdAt: string;
  resolvedAt?: string | null;
  reason?: string | null;
}

export interface StudentProfileExtendedDto {
  id: string;
  fullName: string;
  phone: string;
  parentPhone?: string;
  secondaryPhone?: string;
  secondaryParentPhone?: string;
  district?: string;
  grade?: string;
  schoolName?: string;
  isActive: boolean;
  createdAt: string;

  // Personal fields
  dateOfBirth?: string;
  gender?: string;
  governorate?: string;
  address?: string;
  studentCode?: string;
  isProfileComplete?: boolean;

  // Academic fields
  educationStage?: string;
  studyTrack?: string;

  // Parent/Family fields (V2)
  nationality?: string;
  motherPhone?: string;
  fatherDateOfBirth?: string;
  motherDateOfBirth?: string;
  schoolType?: string;
  isFatherAlive?: boolean;
  isMotherAlive?: boolean;

  gamification?: {
    totalPoints: number;
    globalRank: number;
    level: number;
    title: string;
    recentBadges: string[];
  };
  packages: StudentPackageDto[];
  devices: StudentDeviceProfileDto[];
  overrides: StudentVideoOverrideDto[];
  watchTracking: {
    totalWatchedSeconds: number;
    watchedVideosCount: number;
    activities: Array<{
      lessonVideoId: string;
      videoTitle: string;
      lessonId: string;
      lessonTitle: string;
      packageName?: string;
      termTitle?: string;
      watchCount: number;
      maxWatchCount: number;
      watchedSeconds: number;
      isLocked: boolean;
      lastWatchedAt: string;
    }>;
  };
  currentBalance: number;
  balanceTransactions: BalanceTransactionDto[];
  auditTrail: StudentAuditLogDto[];
  notes: Array<{
    id: string;
    content: string;
    adminName: string;
    isPinned: boolean;
    createdAt: string;
  }>;
}

export interface StudentExamResultSummaryDto {
  studentId: string;
  studentName: string;
  studentPhone: string;
  startedAt?: string;
  submittedAt?: string;
  scoreAchieved: number;
  evaluation: string;
  isPassed: boolean;
  isTimeExpired: boolean;
}

export interface ExamQuestionSummaryDto {
  examQuestionId: string;
  text: string;
  type: string;
  points: number;
  baseText?: string | null;
}

export interface ExamDashboardDto {
  examId: string;
  title: string;
  description: string;
  questionCount: number;
  totalScore: number;
  passingScore: number;
  durationMinutes?: number;
  timePerQuestionSeconds?: number;
  attempts: StudentExamResultSummaryDto[];
  questions: ExamQuestionSummaryDto[];
}

export interface ModerationLessonCommentDto {
  id: string;
  lessonId: string;
  lessonTitle: string;
  studentId: string;
  studentName: string;
  body: string;
  status: string;
  createdAt: string;
  reviewedAt?: string | null;
  reviewedByName?: string | null;
}

export interface ModerateLessonCommentResponse {
  id: string;
  status: string;
  reviewedAt?: string | null;
  reviewedByUserId?: string | null;
}

export interface ModerationCommunityPostDto {
  id: string;
  studentId: string;
  studentName: string;
  body: string;
  status: string;
  createdAt: string;
  reviewedAt?: string | null;
  reviewedByName?: string | null;
  commentCount: number;
  likeCount: number;
}

export interface ModerateCommunityPostResponse {
  id: string;
  status: string;
  reviewedAt?: string | null;
  reviewedByUserId?: string | null;
}

export interface ModerationCommunityCommentDto {
  id: string;
  postId: string;
  studentId: string;
  studentName: string;
  body: string;
  status: string;
  createdAt: string;
  reviewedAt?: string | null;
  reviewedByName?: string | null;
  rejectionReason?: string | null;
}

export interface ModerateCommunityCommentResponse {
  commentId: string;
  status: string;
  rejectionReason?: string | null;
}

export interface EssaySubmissionDto {
  id: string;
  studentId: string;
  questionId: string;
  answerText: string;
  aiInitialScore?: number;
  aiFeedback?: string;
  status: string;
}

export interface LessonCockpitCommentSummaryDto {
  total: number;
  pending: number;
  approved: number;
  rejected: number;
}

export interface LessonCockpitDto {
  lessonId: string;
  title: string;
  summary: string;
  examId?: string | null;
  videos: any[];
  resources: any[];
  homework: any[];
  commentsSummary: LessonCockpitCommentSummaryDto;
}

export type PackageCodeProfileStatus = 'Draft' | 'Published' | 'Fallback';

export interface PackageCodeProfileDto {
  packageId: string;
  packageName: string;
  status: PackageCodeProfileStatus;
  isUsingFallback: boolean;
  heroEyebrow: string;
  heroTitle: string;
  heroDescription: string;
  offerTitle: string;
  offerDescription: string;
  activationTitle: string;
  activationDescription: string;
  supportTitle: string;
  supportDescription: string;
  themeAccentKey: string;
  publishedAt?: string | null;
  lastUpdatedAt?: string | null;
}

export interface PackageCodeProfilePayload {
  status: PackageCodeProfileStatus;
  heroEyebrow?: string;
  heroTitle?: string;
  heroDescription?: string;
  offerTitle?: string;
  offerDescription?: string;
  activationTitle?: string;
  activationDescription?: string;
  supportTitle?: string;
  supportDescription?: string;
  themeAccentKey?: string;
}


export interface AdminCreateUserPayload {
  fullName: string;
  phoneNumber: string;
  password: string;
  role: string;
  packageIds?: string[];
}

export interface AdminCreateUserResult {
  id: string;
  fullName: string;
  phoneNumber: string;
  role: string;
}

export interface AdminPackageListItemDto {
  id: string;
  name: string;
  teacherId: string;
  subjectId: string;
}

export const adminService = {
  // Users
  listUsers: async (
    page = 1,
    pageSize = 20,
    search = '',
    educationStage?: string,
    gradeLevel?: string,
    studyTrack?: string,
    gender?: string,
    governorate?: string
  ) => {
    const res = await apiClient.get<ApiResponse<PagedResult<AdminUserListDto>>>('/admin/users', {
      params: {
        page,
        pageSize,
        ...(search ? { search } : {}),
        ...(educationStage ? { educationStage } : {}),
        ...(gradeLevel ? { gradeLevel } : {}),
        ...(studyTrack ? { studyTrack } : {}),
        ...(gender ? { gender } : {}),
        ...(governorate ? { governorate } : {})
      }
    });
    return res.data?.data;
  },

  createUser: async (payload: AdminCreateUserPayload) => {
    const res = await apiClient.post<ApiResponse<AdminCreateUserResult>>('/admin/users', {
      ...payload,
      packageIds: payload.packageIds ?? []
    });
    return res.data;
  },

  listAllPackages: async (): Promise<AdminPackageListItemDto[]> => {
    const res = await apiClient.get<ApiResponse<AdminPackageListItemDto[]>>('/admin/packages/list');
    return res.data?.data ?? [];
  },


  updateUserStatus: async (id: string, status: string) => {
    const res = await apiClient.put(`/admin/users/${id}/status`, { status });
    return res.data?.data;
  },

  updateUserRoles: async (id: string, roles: string[]) => {
    const res = await apiClient.put(`/admin/users/${id}/roles`, { roles });
    return res.data?.data;
  },

  uploadTeacherPhoto: async (teacherId: string, base64Image: string, fileName: string) => {
    const res = await apiClient.post<ApiResponse>('/admin/teacher-photos/upload', {
      teacherId,
      base64Image,
      fileName
    });
    return res.data;
  },

  uploadTeacherProfileImage: async (teacherId: string, base64Image: string, fileName: string) => {
    const res = await apiClient.post<ApiResponse<string>>('/admin/teachers/upload-profile-image', {
      teacherId,
      base64Image,
      fileName
    });
    return res.data;
  },

  uploadFormCoverImage: async (base64Image: string, fileName: string) => {
    const res = await apiClient.post<{ success: boolean; data: string; message?: string }>('/admin/forms/cover/upload', {
      base64Image,
      fileName
    });
    return res.data;
  },

  getStudentProfile: async (id: string) => {
    const res = await apiClient.get(`/admin/users/students/${id}/profile`);
    return res.data?.data !== undefined ? res.data?.data : res.data;
  },

  getUserDevices: async (id: string) => {
    const res = await apiClient.get<ApiResponse<DeviceDto[]>>(`/admin/users/${id}/devices`);
    return res.data?.data;
  },

  getUserAuditLogs: async (id: string) => {
    const res = await apiClient.get<ApiResponse<UserAuditLogDto[]>>(`/admin/users/${id}/audit-logs`);
    return res.data?.data;
  },

  disconnectDevice: async (userId: string, deviceId: string) => {
    const res = await apiClient.delete(`/admin/users/students/${userId}/devices/${deviceId}`);
    return res;
  },

  disconnectAllDevices: async (userId: string) => {
    const res = await apiClient.delete(`/admin/users/students/${userId}/devices`);
    return res;
  },

  removeDevice: async (id: string) => {
    const res = await apiClient.delete(`/admin/devices/${id}`);
    return res.data?.data;
  },

  toggleStudentStatus: async (userId: string, isActive: boolean, reason: string) => {
    const res = await apiClient.patch(`/admin/users/students/${userId}/status`, { isActive, reason });
    return res;
  },

  overrideVideoLimit: async (userId: string, videoId: string, addedViews: number, reason: string) => {
    const res = await apiClient.post(`/admin/users/students/${userId}/overrides`, { videoId, addedViews, reason });
    return res;
  },

  adjustGamification: async (userId: string, points: number, reason: string) => {
    const res = await apiClient.post(`/admin/users/students/${userId}/gamification/adjust`, { points, reason });
    return res;
  },

  // Codes
  bulkGenerateCodes: async (payload: { packageId?: string; lessonId?: string; count: number; codeLength: number }) => {
    const isTeacher = getSurfaceName() === 'teacher';
    const path = isTeacher ? '/teacher/codes/bulk-generate' : '/admin/codes/bulk-generate';
    const res = await apiClient.post(path, payload);
    return res.data?.data;
  },

  listCodeGroups: async (options?: { force?: boolean }) => {
    const force = options?.force ?? false;
    const isCacheFresh =
      !force &&
      codeGroupsCache !== undefined &&
      Date.now() - codeGroupsCacheAt < CODE_GROUPS_CACHE_TTL_MS;

    if (isCacheFresh) {
      return codeGroupsCache;
    }

    if (!force && codeGroupsInFlight) {
      return codeGroupsInFlight;
    }

    const isTeacher = getSurfaceName() === 'teacher';
    const path = isTeacher ? '/teacher/codes/groups' : '/admin/codes/groups';

    codeGroupsInFlight = apiClient
      .get<ApiResponse<CodeGroupDto[]>>(path)
      .then((res) => {
        codeGroupsCache = res.data?.data;
        codeGroupsCacheAt = Date.now();
        return codeGroupsCache;
      })
      .finally(() => {
        codeGroupsInFlight = null;
      });

    return codeGroupsInFlight;
  },

  getCodeGroupDetails: async (id: string) => {
    const isTeacher = getSurfaceName() === 'teacher';
    const path = isTeacher ? `/teacher/codes/groups/${id}/details` : `/admin/codes/groups/${id}/details`;
    const res = await apiClient.get<ApiResponse<CodeDetailDto[]>>(path);
    return res.data?.data;
  },

  // Questions
  listQuestions: async (page = 1, pageSize = 20, search = '') => {
    const res = await apiClient.get<ApiResponse<PagedResult<QuestionBankItemDto>>>('/admin/questions', {
      params: { page, pageSize, search }
    });
    return res.data?.data;
  },

  createQuestion: async (payload: {
    text: string;
    type?: number;
    defaultPoints: number;
    tags: string;
    hintText?: string;
    writtenCorrection?: string;
    baseText?: string;
    mistakeStartIndex?: number;
    mistakeEndIndex?: number;
    options: { text: string; isCorrect: boolean }[];
    subjectId: string;
    teacherId?: string;
  }) => {
    const res = await apiClient.post<ApiResponse<{ id: string }>>('/admin/questions', {
      ...payload,
      type: payload.type || 0 // Default to MCQ if omitted
    });
    return res.data?.data;
  },

  uploadQuestionAudio: async (questionId: string, file: File) => {
    const formData = new FormData();
    formData.append('audio', file);
    const res = await apiClient.post<ApiResponse<string>>(`/admin/questions/${questionId}/audio`, formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
    return res.data?.data;
  },

  getPendingEssays: async () => {
    const res = await apiClient.get<ApiResponse<EssaySubmissionDto[]>>('/admin/essays/pending');
    return res.data?.data;
  },

  gradeEssay: async (essaySubmissionId: string, teacherScore: number, teacherFeedback?: string) => {
    const res = await apiClient.post<ApiResponse<boolean>>(`/admin/essays/${essaySubmissionId}/grade`, {
      essaySubmissionId,
      teacherScore,
      teacherFeedback
    });
    return res.data?.data;
  },

  // Content Creators (Simplified)
  createPackage: async (payload: {
    name: string;
    description: string;
    price: number;
    subjectId: string;
    targetGrade: string;
    teacherId?: string;
  }) => {
    const res = await apiClient.post<ApiResponse<{ id: string }>>('/admin/packages', payload);
    return res.data?.data;
  },
  uploadContentImage: async (
    contentType: ContentImageType,
    id: string,
    image: File,
    onProgress?: (percent: number) => void
  ) => {
    const formData = new FormData();
    formData.append('image', image);
    const res = await apiClient.post<ApiResponse<string>>(
      `/admin/content/${contentType}/${id}/image`,
      formData,
      {
        headers: { 'Content-Type': 'multipart/form-data' },
        onUploadProgress: (progressEvent) => {
          if (progressEvent.total && onProgress) {
            const percent = Math.round((progressEvent.loaded * 100) / progressEvent.total);
            onProgress(percent);
          }
        },
      }
    );
    return res.data.data;
  },
  getPackageById: async (id: string) => {
    const res = await apiClient.get<ApiResponse<any>>(`/admin/packages/${id}`);
    return res.data?.data;
  },
  updatePackage: async (id: string, payload: any) => {
    const res = await apiClient.put<ApiResponse<any>>(`/admin/packages/${id}`, payload);
    return res.data?.data;
  },
  getPackageCodeProfile: async (id: string) => {
    const res = await apiClient.get<ApiResponse<PackageCodeProfileDto>>(`/admin/packages/${id}/code-profile`);
    return res.data?.data;
  },
  upsertPackageCodeProfile: async (id: string, payload: PackageCodeProfilePayload) => {
    const res = await apiClient.put<ApiResponse<any>>(`/admin/packages/${id}/code-profile`, payload);
    return res.data?.data;
  },
  resetPackageCodeProfile: async (id: string) => {
    const res = await apiClient.delete<ApiResponse>(`/admin/packages/${id}/code-profile`);
    return res.data;
  },
  createTerm: async (payload: { packageId: string; title: string; order: number; price: number }) => {
    const res = await apiClient.post<ApiResponse<string>>('/admin/terms', payload);
    return res.data?.data;
  },
  updateTerm: async (id: string, payload: { title: string; order: number; price: number }) => {
    const res = await apiClient.put<ApiResponse>(`/admin/terms/${id}`, payload);
    return res.data;
  },
  deleteTerm: async (id: string) => {
    const res = await apiClient.delete<ApiResponse>(`/admin/terms/${id}`);
    return res.data;
  },
  getTermById: async (id: string) => {
    const res = await apiClient.get<ApiResponse<any>>(`/admin/terms/${id}`);
    return res.data?.data;
  },
  getSectionById: async (id: string) => {
    const res = await apiClient.get<ApiResponse<any>>(`/admin/sections/${id}`);
    return res.data?.data;
  },
  createSection: async (payload: any) => {
    const res = await apiClient.post<ApiResponse<{ id: string }>>('/admin/sections', payload);
    return res.data?.data;
  },
  createLesson: async (payload: any) => {
    const res = await apiClient.post<ApiResponse<{ id: string }>>('/admin/lessons', payload);
    return res.data?.data;
  },
  getLessonCockpit: async (id: string) => {
    const res = await apiClient.get<ApiResponse<LessonCockpitDto>>(`/admin/lessons/${id}/cockpit`);
    return res;
  },
  getCommunityPostsForModeration: async (status?: string) => {
    const res = await apiClient.get<ApiResponse<ModerationCommunityPostDto[]>>('/admin/community/posts', {
      params: status && status !== 'All' ? { status } : undefined,
    });
    return res.data?.data ?? [];
  },
  approveCommunityPost: async (postId: string) => {
    const res = await apiClient.post<ApiResponse<ModerateCommunityPostResponse>>(`/admin/community/posts/${postId}/approve`, {});
    return res.data?.data;
  },
  rejectCommunityPost: async (postId: string) => {
    const res = await apiClient.post<ApiResponse<ModerateCommunityPostResponse>>(`/admin/community/posts/${postId}/reject`, {});
    return res.data?.data;
  },
  getPendingCommunityComments: async () => {
    const res = await apiClient.get<ApiResponse<ModerationCommunityCommentDto[]>>('/admin/community/comments/pending');
    return res.data?.data ?? [];
  },
  approveCommunityComment: async (commentId: string) => {
    const res = await apiClient.post<ApiResponse<ModerateCommunityCommentResponse>>(`/admin/community/comments/${commentId}/approve`, {});
    return res.data?.data;
  },
  rejectCommunityComment: async (commentId: string, reason: string) => {
    const res = await apiClient.post<ApiResponse<ModerateCommunityCommentResponse>>(`/admin/community/comments/${commentId}/reject`, { reason });
    return res.data?.data;
  },
  getLessonCommentsForModeration: async (lessonId: string, status?: string) => {
    const res = await apiClient.get<ApiResponse<ModerationLessonCommentDto[]>>(`/admin/lessons/${lessonId}/comments`, {
      params: status && status !== 'All' ? { status } : undefined,
    });
    return res.data?.data ?? [];
  },
  approveLessonComment: async (commentId: string) => {
    const res = await apiClient.post<ApiResponse<ModerateLessonCommentResponse>>(`/admin/comments/${commentId}/approve`, {});
    return res.data?.data;
  },
  rejectLessonComment: async (commentId: string) => {
    const res = await apiClient.post<ApiResponse<ModerateLessonCommentResponse>>(`/admin/comments/${commentId}/reject`, {});
    return res.data?.data;
  },
  createVideo: async (payload: any) => {
    const res = await apiClient.post<ApiResponse<{ id: string }>>('/admin/videos', payload);
    return res.data?.data;
  },
  updateVideo: async (videoId: string, payload: any) => {
    const res = await apiClient.put<ApiResponse>(`/admin/videos/${videoId}`, payload);
    return res.data;
  },
  deleteVideo: async (videoId: string) => {
    const res = await apiClient.delete<ApiResponse>(`/admin/videos/${videoId}`);
    return res.data;
  },
  triggerVideoAiAnalysis: async (videoId: string) => {
    const res = await apiClient.post<ApiResponse>(`/admin/videos/${videoId}/analyze-ai`);
    return res.data;
  },
  generateVideoMindmaps: async (videoId: string) => {
    const res = await apiClient.post<ApiResponse>(`/admin/videos/${videoId}/generate-mindmaps`);
    return res.data;
  },
  cancelVideoAiAnalysis: async (videoId: string) => {
    const res = await apiClient.post<ApiResponse>(`/admin/videos/${videoId}/cancel-ai`);
    return res.data;
  },
  cancelMindmapGeneration: async (videoId: string) => {
    const res = await apiClient.post<ApiResponse>(`/admin/videos/${videoId}/cancel-mindmap`);
    return res.data;
  },
  regenerateChapterMindmap: async (chapterId: string) => {
    const res = await apiClient.post<ApiResponse>(`/admin/chapters/${chapterId}/regenerate-mindmap`);
    return res.data;
  },
  createResource: async (payload: { lessonId: string; title: string; fileUrl: string; resourceType: string }) => {
    const res = await apiClient.post<ApiResponse<{ id: string }>>('/admin/resources', payload);
    return res.data?.data;
  },
  attachHomework: async (lessonId: string, payload: { title: string; instructions: string; isMandatory: boolean; totalScore: number; requiredPointsToPass: number; questions: { text: string; order: number; maxPoints: number }[] }) => {
    const res = await apiClient.post<ApiResponse<{ id: string }>>(`/admin/content/lessons/${lessonId}/homework`, payload);
    return res.data?.data;
  },
  linkLessonExam: async (lessonId: string, examId: string | null) => {
    const res = await apiClient.put<ApiResponse>(`/admin/lessons/${lessonId}/exam`, { examId });
    return res.data;
  },
  createInlineExam: async (payload: { title: string; description: string; passingScore: number; totalScore: number; durationMinutes?: number; timePerQuestionSeconds?: number; displayQuestionCount?: number; target: { type: string; id: string }; questions: { text: string; type: string; points: number; order: number; options: { text: string; isCorrect: boolean }[]; audioUrl?: string; writtenCorrection?: string; hintText?: string; baseText?: string; mistakeStartIndex?: number | null; mistakeEndIndex?: number | null }[] }) => {
    const res = await apiClient.post<ApiResponse<{ id: string }>>('/admin/exams/inline', payload);
    return res.data?.data;
  },
  addQuestionsToExam: async (examId: string, payload: { questions: { text: string; type: string; points: number; order: number; options: { text: string; isCorrect: boolean }[]; audioUrl?: string; writtenCorrection?: string; hintText?: string; baseText?: string; mistakeStartIndex?: number | null; mistakeEndIndex?: number | null }[] }) => {
    const res = await apiClient.post<ApiResponse>(`/admin/exams/${examId}/questions`, payload);
    return res.data;
  },

  getExamDashboard: async (examId: string) => {
    const res = await apiClient.get<ApiResponse<ExamDashboardDto>>(`/admin/exams/${examId}/dashboard`);
    return res.data?.data;
  },

  deleteExamQuestion: async (examId: string, examQuestionId: string) => {
    const res = await apiClient.delete<ApiResponse>(`/admin/exams/${examId}/questions/${examQuestionId}`);
    return res.data;
  },

  // Overrides
  manualUnlockLesson: async (lessonId: string, studentId: string) => {
    const res = await apiClient.post(`/exams/admin/lessons/${lessonId}/students/${studentId}/unlock`);
    return res.data?.data;
  },

  resetWatchLimit: async (lessonVideoId: string, studentId: string) => {
    const res = await apiClient.post('/admin/overrides/reset-watch', { lessonVideoId, studentId });
    return res.data?.data;
  },

  setWatchCount: async (lessonVideoId: string, studentId: string, newWatchCount: number) => {
    const res = await apiClient.put('/admin/overrides/set-watch-count', { lessonVideoId, studentId, newWatchCount });
    return res.data?.data;
  },

  adjustBalance: async (studentId: string, amount: number, reason: string) => {
    const res = await apiClient.post(`/admin/users/students/${studentId}/balance/adjust`, { amount, reason });
    return res.data?.data;
  },

  updateStudentProfile: async (studentId: string, data: Record<string, unknown>) => {
    const res = await apiClient.put(`/admin/users/students/${studentId}/profile`, data);
    return res.data?.data;
  },

  adminResetPassword: async (studentId: string, newPassword: string) => {
    const res = await apiClient.post(`/admin/users/students/${studentId}/reset-password`, { newPassword });
    return res.data?.data;
  },

  addStudentNote: async (studentId: string, content: string, isPinned: boolean) => {
    const res = await apiClient.post(`/admin/users/students/${studentId}/notes`, { content, isPinned });
    return res.data?.data;
  },

  deleteStudentNote: async (studentId: string, noteId: string) => {
    const res = await apiClient.delete(`/admin/users/students/${studentId}/notes/${noteId}`);
    return res.data?.data;
  },

  getWatchRequests: async () => {
    const res = await apiClient.get<ApiResponse<AdminWatchRequestDto[]>>('/admin/watch-requests');
    return res.data;
  },

  approveWatchRequest: async (id: string, reason?: string) => {
    const res = await apiClient.post<ApiResponse<boolean>>(`/admin/watch-requests/${id}/approve`, { reason });
    return res.data;
  },

  rejectWatchRequest: async (id: string, reason?: string) => {
    const res = await apiClient.post<ApiResponse<boolean>>(`/admin/watch-requests/${id}/reject`, { reason });
    return res.data;
  },

  cancelStudentPackage: async (userId: string, accessGrantId: string, refundBalance: boolean) => {
    const res = await apiClient.post<ApiResponse>(`/admin/users/students/${userId}/packages/${accessGrantId}/cancel`, { refundBalance });
    return res.data;
  },

  getPlatformSettings: async () => {
    const res = await apiClient.get<ApiResponse<any[]>>('/admin/settings');
    return res.data?.data;
  },

  updatePlatformSettings: async (settings: Record<string, string>) => {
    const res = await apiClient.put<ApiResponse<boolean>>('/admin/settings', { settings });
    return res.data;
  },

  listRoles: async () => {
    const res = await apiClient.get<ApiResponse<any[]>>('/admin/roles');
    return res.data?.data;
  },

  createRole: async (payload: { name: string; permissions: string[] }) => {
    const res = await apiClient.post<ApiResponse<any>>('/admin/roles', payload);
    return res.data;
  },

  updateRole: async (id: string, payload: { name: string; permissions: string[] }) => {
    const res = await apiClient.put<ApiResponse<any>>(`/admin/roles/${id}`, payload);
    return res.data;
  },

  deleteRole: async (id: string) => {
    const res = await apiClient.delete<ApiResponse<any>>(`/admin/roles/${id}`);
    return res.data;
  }
};
