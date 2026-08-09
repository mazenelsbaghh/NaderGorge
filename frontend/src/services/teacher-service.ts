import apiClient from './api-client';
import { invalidateMany } from '@/lib/cache-invalidation';
import type { CreatePublicExamDto, PublicExamProductDto } from './admin-sales-service';
import type { ModerationCommunityCommentDto, ModerationCommunityPostDto, ModerateCommunityCommentResponse, ModerateCommunityPostResponse, ModerationLessonCommentDto, ModerateLessonCommentResponse } from './admin-service';

export interface SubjectDto {
  id: string;
  name: string;
  description: string;
}

export interface TeacherDto {
  id: string;
  userId: string;
  fullName: string;
  phoneNumber: string;
  bio: string;
  specialization: string;
  commissionRate: number;
  profileImageUrl?: string;
  contactInfo: string;
  subjectIds: string[];
  subjectNames: string[];
  assistantPhoneNumbers?: string;
  facebookUrl?: string;
  youtubeUrl?: string;
  telegramUrl?: string;
  introVideoUrl?: string;
  showOnLanding: boolean;
  isVisibleToStudents: boolean;
  isContentVisibleToStudents: boolean;
  isActive: boolean;
}

export interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data: T;
}

export interface TeacherDashboardStatsDto {
  activeStudentsCount: number;
  packagesCount: number;
  examsCount: number;
  pendingEssaysCount: number;
  packageSales: TeacherPackageSalesBreakdownDto[];
}

export interface TeacherPackageSalesBreakdownDto {
  packageId: string;
  packageName: string;
  packageBuyers: number;
  termBuyers: number;
  sectionBuyers: number;
  lessonBuyers: number;
  purchasedStudents: number;
  giftStudents: number;
}

export interface TeacherActiveStudentDto {
  studentId: string;
  studentName: string;
  lastActivityAt: string | null;
  lastWatchedVideoTitle: string;
  packageName: string;
}

export interface TeacherMostWatchedVideoDto {
  videoId: string;
  videoTitle: string;
  lessonTitle: string;
  totalWatchCount: number;
  totalTimeWatchedSeconds: number;
  averagePlaybackRate: number;
}

export interface TeacherInactiveStudentAlertDto {
  studentId: string;
  studentName: string;
  lastActivityAt: string | null;
  packageName: string;
  daysInactive: number;
}

export interface TeacherActivityDto {
  activeStudents: TeacherActiveStudentDto[];
  mostWatchedVideos: TeacherMostWatchedVideoDto[];
  inactiveStudentAlerts: TeacherInactiveStudentAlertDto[];
}

export interface TeacherStudentDto {
  id: string;
  fullName: string;
  phoneNumber: string;
  activatedPackageName: string;
  activatedAt: string;
  studentCode?: string | null;
  secondaryPhone?: string | null;
  parentPhone?: string | null;
  motherPhone?: string | null;
  governorate?: string | null;
  district?: string | null;
  address?: string | null;
  educationStage: string;
  gradeLevel: string;
  studyTrack?: string | null;
  schoolName?: string | null;
  schoolType?: string | null;
  activePackageCount: number;
  activeGrantCount: number;
  lastActivationAt?: string | null;
}

export interface PendingEssayDto {
  id: string;
  studentName: string;
  questionText: string;
  examTitle: string;
  submittedAt: string;
  status: string;
  answerText: string;
  audioUrl?: string;
  aiInitialScore?: number;
  aiFeedback?: string;
  maxPoints: number;
}

export interface TeacherProfileDto {
  id: string;
  userId: string;
  bio: string;
  specialization: string;
  profileImageUrl?: string;
  contactInfo: string;
  assistantPhoneNumbers?: string;
  facebookUrl?: string;
  youtubeUrl?: string;
  telegramUrl?: string;
}

export interface TeacherStaffMemberDto {
  id: string;
  userId: string;
  fullName: string;
  phoneNumber: string;
  isActive: boolean;
  createdAt: string;
  notes?: string | null;
  permissionKeys: string[];
}

export interface TeacherWorkspaceContextDto {
  isOwner: boolean;
  permissionKeys: string[];
  availablePermissionKeys: string[];
}

export const teacherService = {
  // Subjects CRUD
  getSubjects: () =>
    apiClient.get<ApiResponse<SubjectDto[]>>('/admin/subjects').then((res) => res.data),
  getSubjectById: (id: string) =>
    apiClient.get<ApiResponse<SubjectDto>>(`/admin/subjects/${id}`).then((res) => res.data),
  createSubject: async (data: { name: string; description: string }) => {
    const res = await apiClient.post<ApiResponse<string>>('/admin/subjects', data);
    if (res.data.success) invalidateMany(['teacher:subjects', 'content:packages']);
    return res.data;
  },
  updateSubject: async (id: string, data: { name: string; description: string }) => {
    const res = await apiClient.put<ApiResponse<void>>(`/admin/subjects/${id}`, data);
    if (res.data.success) invalidateMany(['teacher:subjects', 'content:packages']);
    return res.data;
  },
  deleteSubject: async (id: string) => {
    const res = await apiClient.delete<ApiResponse<void>>(`/admin/subjects/${id}`);
    if (res.data.success) invalidateMany(['teacher:subjects', 'content:packages']);
    return res.data;
  },

  // Teachers CRUD
  getTeachers: () =>
    apiClient.get<ApiResponse<TeacherDto[]>>('/admin/teachers').then((res) => res.data),
  getTeacherById: (id: string) =>
    apiClient.get<ApiResponse<TeacherDto>>(`/admin/teachers/${id}`).then((res) => res.data),
  createTeacher: (data: {
    userId: string;
    bio: string;
    specialization: string;
    commissionRate: number;
    profileImageUrl?: string;
    contactInfo: string;
    subjectIds: string[];
    assistantPhoneNumbers?: string;
    facebookUrl?: string;
    youtubeUrl?: string;
    telegramUrl?: string;
    introVideoUrl?: string;
    showOnLanding: boolean;
  }) =>
    apiClient.post<ApiResponse<string>>('/admin/teachers', data).then((res) => {
      if (res.data.success) invalidateMany(['teachers', 'content:packages']);
      return res.data;
    }),
  updateTeacher: (
    id: string,
    data: {
      fullName?: string;
      phoneNumber?: string;
      newPassword?: string;
      bio: string;
      specialization: string;
      commissionRate: number;
      profileImageUrl?: string;
      contactInfo: string;
      subjectIds: string[];
      assistantPhoneNumbers?: string;
      facebookUrl?: string;
      youtubeUrl?: string;
      telegramUrl?: string;
      introVideoUrl?: string;
      showOnLanding: boolean;
      isVisibleToStudents?: boolean;
      isContentVisibleToStudents?: boolean;
    }
  ) =>
    apiClient.put<ApiResponse<void>>(`/admin/teachers/${id}`, data).then((res) => {
      if (res.data.success) invalidateMany(['teachers', 'teacher:profile', 'content:packages']);
      return res.data;
    }),

  // Teacher Workspace Surface
  getDashboardStats: () =>
    apiClient.get<ApiResponse<TeacherDashboardStatsDto>>('/teacher/dashboard/stats').then((res) => res.data),
  getStudents: () =>
    apiClient.get<ApiResponse<TeacherStudentDto[]>>('/teacher/students').then((res) => res.data),
  getEssays: () =>
    apiClient.get<ApiResponse<PendingEssayDto[]>>('/teacher/essays').then((res) => res.data),
  gradeEssay: (id: string, data: { score: number; feedback: string }) =>
    apiClient.post<ApiResponse<void>>(`/teacher/essays/${id}/grade`, data).then((res) => {
      if (res.data.success) invalidateMany(['teacher:essays', 'student:exams', 'assessments']);
      return res.data;
    }),
  getMyProfile: () =>
    apiClient.get<ApiResponse<TeacherProfileDto>>('/teacher/profile').then((res) => res.data),
  updateMyProfile: (data: {
    bio: string;
    specialization: string;
    contactInfo: string;
    profileImageUrl?: string;
    assistantPhoneNumbers?: string;
    facebookUrl?: string;
    youtubeUrl?: string;
    telegramUrl?: string;
  }) =>
    apiClient.put<ApiResponse<void>>('/teacher/profile', data).then((res) => {
      if (res.data.success) invalidateMany(['teacher:profile', 'teachers']);
      return res.data;
    }),
  uploadMyProfileImage: (base64Image: string, fileName: string) =>
    apiClient.post<ApiResponse<string>>('/teacher/profile/upload-image', { base64Image, fileName }).then((res) => {
      if (res.data.success) invalidateMany(['teacher:profile', 'teachers']);
      return res.data;
    }),
  uploadMyAiPhoto: (base64Image: string, fileName: string) =>
    apiClient.post<ApiResponse<void>>('/teacher/profile/upload-ai-photo', { base64Image, fileName }).then((res) => {
      if (res.data.success) invalidateMany(['teacher:profile']);
      return res.data;
    }),
  getActiveTeacherPhoto: () =>
    apiClient.get<ApiResponse<{ url: string | null }>>('/teacher/profile/active-photo').then((res) => res.data),
  getTeacherActivity: () =>
    apiClient.get<ApiResponse<TeacherActivityDto>>('/teacher/activity').then((res) => res.data),
  getWorkspaceContext: () =>
    apiClient.get<ApiResponse<TeacherWorkspaceContextDto>>('/teacher/context').then((res) => res.data),
  getMySubjects: () =>
    apiClient.get<ApiResponse<SubjectDto[]>>('/teacher/subjects').then((res) => res.data),
  getContentSubscribers: async (
    contentType: 'package' | 'term' | 'section' | 'lesson',
    id: string,
    page = 1,
    pageSize = 20,
    search = ''
  ) => {
    const res = await apiClient.get<ApiResponse<import('./admin-service').ContentSubscribersPagedResult>>(
      `/teacher/content/${contentType}/${id}/subscribers`,
      { params: { page, pageSize, ...(search ? { search } : {}) } }
    );
    return res.data?.data;
  },
  exportContentSubscribersCsv: async (
    contentType: 'package' | 'term' | 'section' | 'lesson',
    id: string,
    contentName: string
  ) => {
    const res = await apiClient.get(`/teacher/content/${contentType}/${id}/subscribers/export`, { responseType: 'blob' });
    const blobUrl = URL.createObjectURL(new Blob([res.data], { type: 'text/csv;charset=utf-8;' }));
    const downloadLink = document.createElement('a');
    downloadLink.href = blobUrl;
    downloadLink.download = `subscribers_${contentName}_${new Date().toISOString().split('T')[0]}.csv`;
    document.body.appendChild(downloadLink);
    downloadLink.click();
    document.body.removeChild(downloadLink);
    URL.revokeObjectURL(blobUrl);
  },
  getMyStaff: () =>
    apiClient.get<ApiResponse<TeacherStaffMemberDto[]>>('/teacher/staff').then((res) => res.data),
  createMyStaff: (data: { fullName: string; phoneNumber: string; password: string; notes?: string; permissionKeys?: string[] }) =>
    apiClient.post<ApiResponse<TeacherStaffMemberDto>>('/teacher/staff', data).then((res) => {
      if (res.data.success) invalidateMany(['teacher:staff', 'employees']);
      return res.data;
    }),
  setMyStaffStatus: (staffMemberId: string, isActive: boolean) =>
    apiClient.patch<ApiResponse<TeacherStaffMemberDto>>(`/teacher/staff/${staffMemberId}/status`, { isActive }).then((res) => {
      if (res.data.success) invalidateMany(['teacher:staff', 'employees', 'session']);
      return res.data;
    }),
  setMyStaffPermissions: (staffMemberId: string, permissionKeys: string[]) =>
    apiClient.patch<ApiResponse<TeacherStaffMemberDto>>(`/teacher/staff/${staffMemberId}/permissions`, { permissionKeys }).then((res) => {
      if (res.data.success) invalidateMany(['teacher:staff', 'employees', 'session']);
      return res.data;
    }),
  getPublicExams: async () => {
    const res = await apiClient.get<ApiResponse<PublicExamProductDto[]>>('/teacher/public-exams');
    return res.data?.data ?? [];
  },
  createPublicExam: async (payload: Omit<CreatePublicExamDto, 'teacherId'>) => {
    const res = await apiClient.post<ApiResponse<PublicExamProductDto>>('/teacher/public-exams/new', {
      ...payload,
      teacherId: '00000000-0000-0000-0000-000000000000',
    });
    if (res.data.success) invalidateMany(['teacher:public-exams', 'public-exams', 'assessments']);
    return res.data?.data;
  },
  getCommunityPostsForModeration: async (status?: string) => {
    const res = await apiClient.get<ApiResponse<ModerationCommunityPostDto[]>>('/teacher/community/posts', {
      params: status && status !== 'All' ? { status } : {},
    });
    return res.data?.data ?? [];
  },
  getPendingCommunityComments: async () => {
    const res = await apiClient.get<ApiResponse<ModerationCommunityCommentDto[]>>('/teacher/community/comments/pending');
    return res.data?.data ?? [];
  },
  approveCommunityPost: async (postId: string) => {
    const res = await apiClient.post<ApiResponse<ModerateCommunityPostResponse>>(`/teacher/community/posts/${postId}/approve`, {});
    if (res.data.success) invalidateMany(['teacher:community', 'community:posts']);
    return res.data?.data;
  },
  rejectCommunityPost: async (postId: string) => {
    const res = await apiClient.post<ApiResponse<ModerateCommunityPostResponse>>(`/teacher/community/posts/${postId}/reject`, {});
    if (res.data.success) invalidateMany(['teacher:community', 'community:posts']);
    return res.data?.data;
  },
  approveCommunityComment: async (commentId: string) => {
    const res = await apiClient.post<ApiResponse<ModerateCommunityCommentResponse>>(`/teacher/community/comments/${commentId}/approve`, {});
    if (res.data.success) invalidateMany(['teacher:community', 'community:posts']);
    return res.data?.data;
  },
  rejectCommunityComment: async (commentId: string, reason: string) => {
    const res = await apiClient.post<ApiResponse<ModerateCommunityCommentResponse>>(`/teacher/community/comments/${commentId}/reject`, { reason });
    if (res.data.success) invalidateMany(['teacher:community', 'community:posts']);
    return res.data?.data;
  },
  getLessonCommentsForModeration: async (lessonId: string, status?: string) => {
    const res = await apiClient.get<ApiResponse<ModerationLessonCommentDto[]>>(`/teacher/lessons/${lessonId}/comments`, {
      params: status && status !== 'All' ? { status } : undefined,
    });
    return res.data?.data ?? [];
  },
  approveLessonComment: async (commentId: string) => {
    const res = await apiClient.post<ApiResponse<ModerateLessonCommentResponse>>(`/teacher/comments/${commentId}/approve`, {});
    if (res.data.success) invalidateMany(['teacher:content', 'content:lesson-comments']);
    return res.data?.data;
  },
  rejectLessonComment: async (commentId: string) => {
    const res = await apiClient.post<ApiResponse<ModerateLessonCommentResponse>>(`/teacher/comments/${commentId}/reject`, {});
    if (res.data.success) invalidateMany(['teacher:content', 'content:lesson-comments']);
    return res.data?.data;
  },
  getAllLessonComments: async (status?: string) => {
    const res = await apiClient.get<ApiResponse<ModerationLessonCommentDto[]>>('/teacher/comments', { params: status && status !== 'All' ? { status } : undefined });
    return res.data?.data ?? [];
  },
  replyToLessonComment: async (commentId: string, body: string) => {
    const res = await apiClient.post<ApiResponse<ModerationLessonCommentDto>>(`/teacher/comments/${commentId}/reply`, { body });
    return res.data?.data;
  },
};
