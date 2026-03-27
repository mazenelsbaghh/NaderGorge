import apiClient from './api-client';

export interface ApiResponse<T = any> {
  data: T;
  isSuccess: boolean;
  message: string;
}

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
  address?: string;
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

export interface CodeGroupDto {
  id: string;
  createdAt: string;
  packageId?: string;
  lessonId?: string;
  codeCount: number;
  usedCount: number;
}

export interface CodeDetailDto {
  code: string;
  isUsed: boolean;
  usedAt?: string;
  usedByUserId?: string;
}

export interface QuestionOptionDto {
  id: string;
  text: string;
  isCorrect: boolean;
}

export interface QuestionBankItemDto {
  id: string;
  text: string;
  defaultPoints: number;
  tags: string;
  options: QuestionOptionDto[];
}

export interface StudentProfileExtendedDto {
  id: string;
  fullName: string;
  email: string;
  phone: string;
  parentPhone?: string;
  grade?: string;
  schoolName?: string;
  isActive: boolean;
  createdAt: string;
  gamification?: {
    totalPoints: number;
    globalRank: number;
    level: number;
    title: string;
    recentBadges: string[];
  };
  packages: any[];
  devices: any[];
  overrides: any[];
  auditTrail: any[];
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
  
  updateUserStatus: async (id: string, status: string) => {
    const res = await apiClient.put(`/admin/users/${id}/status`, { status });
    return res.data?.data;
  },

  updateUserRoles: async (id: string, roles: string[]) => {
    const res = await apiClient.put(`/admin/users/${id}/roles`, { roles });
    return res.data?.data;
  },

  getStudentProfile: async (id: string) => {
    const res = await apiClient.get(`/admin/users/students/${id}/profile`);
    return res.data?.data !== undefined ? res.data?.data : res.data;
  },

  getUserDevices: async (id: string) => {
    const res = await apiClient.get<ApiResponse<DeviceDto[]>>(`/admin/users/${id}/devices`);
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
    const res = await apiClient.post('/admin/codes/bulk-generate', payload);
    return res.data?.data;
  },

  listCodeGroups: async () => {
    const res = await apiClient.get<ApiResponse<CodeGroupDto[]>>('/admin/codes/groups');
    return res.data?.data;
  },

  getCodeGroupDetails: async (id: string) => {
    const res = await apiClient.get<ApiResponse<CodeDetailDto[]>>(`/admin/codes/groups/${id}/details`);
    return res.data?.data;
  },

  // Questions
  listQuestions: async (page = 1, pageSize = 20, search = '') => {
    const res = await apiClient.get<ApiResponse<PagedResult<QuestionBankItemDto>>>('/admin/questions', {
      params: { page, pageSize, search }
    });
    return res.data?.data;
  },

  createQuestion: async (payload: { text: string; defaultPoints: number; tags: string; options: { text: string; isCorrect: boolean }[] }) => {
    const res = await apiClient.post<ApiResponse<{ id: string }>>('/admin/questions', payload);
    return res.data?.data;
  },

  // Content Creators (Simplified)
  createPackage: async (payload: any) => {
    const res = await apiClient.post<ApiResponse<{ id: string }>>('/admin/packages', payload);
    return res.data?.data;
  },
  createTerm: async (payload: { packageId: string; title: string; order: number }) => {
    const res = await apiClient.post<ApiResponse<string>>('/admin/terms', payload);
    return res.data?.data;
  },
  updateTerm: async (id: string, payload: { title: string; order: number }) => {
    const res = await apiClient.put<ApiResponse>(`/admin/terms/${id}`, payload);
    return res.data;
  },
  deleteTerm: async (id: string) => {
    const res = await apiClient.delete<ApiResponse>(`/admin/terms/${id}`);
    return res.data;
  },
  createSection: async (payload: any) => {
    const res = await apiClient.post<ApiResponse<{ id: string }>>('/admin/sections', payload);
    return res.data?.data;
  },
  createLesson: async (payload: any) => {
    const res = await apiClient.post<ApiResponse<{ id: string }>>('/admin/lessons', payload);
    return res.data?.data;
  },
  createVideo: async (payload: any) => {
    const res = await apiClient.post<ApiResponse<{ id: string }>>('/admin/videos', payload);
    return res.data?.data;
  },
  attachHomework: async (lessonId: string, payload: { title: string; instructions: string; isMandatory: boolean; requiredPointsToPass: number; questions: { text: string; order: number; maxPoints: number }[] }) => {
    const res = await apiClient.post<ApiResponse<{ id: string }>>(`/admin/content/lessons/${lessonId}/homework`, payload);
    return res.data?.data;
  },

  // Overrides
  manualUnlockLesson: async (lessonId: string, studentId: string) => {
    const res = await apiClient.post(`/exams/admin/lessons/${lessonId}/students/${studentId}/unlock`);
    return res.data?.data;
  },

  resetWatchLimit: async (lessonVideoId: string, studentId: string) => {
    const res = await apiClient.post('/admin/overrides/reset-watch', { lessonVideoId, studentId });
    return res.data?.data;
  }
};
