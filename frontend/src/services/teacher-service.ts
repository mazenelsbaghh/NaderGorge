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
}

export interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data: T;
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
    }
  ) =>
    apiClient.put<ApiResponse<void>>(`/admin/teachers/${id}`, data).then((res) => res.data),
};
