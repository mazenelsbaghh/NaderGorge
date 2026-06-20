import apiClient from './api-client';

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

export const teacherService = {
  // Subjects CRUD
  getSubjects: () =>
    apiClient.get<ApiResponse<SubjectDto[]>>('/admin/subjects').then((res) => res.data),
  getSubjectById: (id: string) =>
    apiClient.get<ApiResponse<SubjectDto>>(`/admin/subjects/${id}`).then((res) => res.data),
  createSubject: (data: { name: string; description: string }) =>
    apiClient.post<ApiResponse<string>>('/admin/subjects', data).then((res) => res.data),
  updateSubject: (id: string, data: { name: string; description: string }) =>
    apiClient.put<ApiResponse<void>>(`/admin/subjects/${id}`, data).then((res) => res.data),
  deleteSubject: (id: string) =>
    apiClient.delete<ApiResponse<void>>(`/admin/subjects/${id}`).then((res) => res.data),

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
  }) =>
    apiClient.post<ApiResponse<string>>('/admin/teachers', data).then((res) => res.data),
  updateTeacher: (
    id: string,
    data: {
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
    }
  ) =>
    apiClient.put<ApiResponse<void>>(`/admin/teachers/${id}`, data).then((res) => res.data),

  // Teacher Workspace Surface
  getDashboardStats: () =>
    apiClient.get<ApiResponse<TeacherDashboardStatsDto>>('/teacher/dashboard/stats').then((res) => res.data),
  getStudents: () =>
    apiClient.get<ApiResponse<TeacherStudentDto[]>>('/teacher/students').then((res) => res.data),
  getEssays: () =>
    apiClient.get<ApiResponse<PendingEssayDto[]>>('/teacher/essays').then((res) => res.data),
  gradeEssay: (id: string, data: { score: number; feedback: string }) =>
    apiClient.post<ApiResponse<void>>(`/teacher/essays/${id}/grade`, data).then((res) => res.data),
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
    apiClient.put<ApiResponse<void>>('/teacher/profile', data).then((res) => res.data),
  uploadMyProfileImage: (base64Image: string, fileName: string) =>
    apiClient.post<ApiResponse<string>>('/teacher/profile/upload-image', { base64Image, fileName }).then((res) => res.data),
  uploadMyAiPhoto: (base64Image: string, fileName: string) =>
    apiClient.post<ApiResponse<void>>('/teacher/profile/upload-ai-photo', { base64Image, fileName }).then((res) => res.data),
  getActiveTeacherPhoto: () =>
    apiClient.get<ApiResponse<{ url: string | null }>>('/teacher/profile/active-photo').then((res) => res.data),
  getTeacherActivity: () =>
    apiClient.get<ApiResponse<TeacherActivityDto>>('/teacher/activity').then((res) => res.data),
  getMySubjects: () =>
    apiClient.get<ApiResponse<SubjectDto[]>>('/teacher/subjects').then((res) => res.data),
};
