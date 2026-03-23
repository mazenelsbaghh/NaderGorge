import apiClient from './api-client';

export interface AdminUserListDto {
  id: string;
  phoneNumber: string;
  status: string;
  fullName: string;
  grade: string;
  track: string;
  createdAt: string;
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

export const adminService = {
  // Users
  listUsers: async (page = 1, pageSize = 20, search = '') => {
    const res = await apiClient.get<PagedResult<AdminUserListDto>>('/api/admin/users', {
      params: { page, pageSize, search }
    });
    return res.data;
  },
  
  updateUserStatus: async (id: string, status: string) => {
    const res = await apiClient.put(`/api/admin/users/${id}/status`, { status });
    return res.data;
  },

  getUserDevices: async (id: string) => {
    const res = await apiClient.get<DeviceDto[]>(`/api/admin/users/${id}/devices`);
    return res.data;
  },

  removeDevice: async (id: string) => {
    const res = await apiClient.delete(`/api/admin/devices/${id}`);
    return res.data;
  },

  // Codes
  bulkGenerateCodes: async (payload: { packageId?: string; lessonId?: string; count: number; codeLength: number }) => {
    const res = await apiClient.post('/api/admin/codes/bulk-generate', payload);
    return res.data;
  },

  listCodeGroups: async () => {
    const res = await apiClient.get<CodeGroupDto[]>('/api/admin/codes/groups');
    return res.data;
  },

  getCodeGroupDetails: async (id: string) => {
    const res = await apiClient.get<CodeDetailDto[]>(`/api/admin/codes/groups/${id}/details`);
    return res.data;
  },

  // Questions
  listQuestions: async (page = 1, pageSize = 20, search = '') => {
    const res = await apiClient.get<PagedResult<QuestionBankItemDto>>('/api/admin/questions', {
      params: { page, pageSize, search }
    });
    return res.data;
  },

  createQuestion: async (payload: { text: string; defaultPoints: number; tags: string; options: { text: string; isCorrect: boolean }[] }) => {
    const res = await apiClient.post<{ id: string }>('/api/admin/questions', payload);
    return res.data;
  },

  // Content Creators (Simplified)
  createPackage: async (payload: any) => {
    const res = await apiClient.post<{ id: string }>('/api/admin/packages', payload);
    return res.data;
  },
  createSection: async (payload: any) => {
    const res = await apiClient.post<{ id: string }>('/api/admin/sections', payload);
    return res.data;
  },
  createLesson: async (payload: any) => {
    const res = await apiClient.post<{ id: string }>('/api/admin/lessons', payload);
    return res.data;
  },
  createVideo: async (payload: any) => {
    const res = await apiClient.post<{ id: string }>('/api/admin/videos', payload);
    return res.data;
  },

  // Overrides
  manualUnlockLesson: async (lessonId: string, studentId: string) => {
    const res = await apiClient.post(`/api/exams/admin/lessons/${lessonId}/students/${studentId}/unlock`);
    return res.data;
  },

  resetWatchLimit: async (lessonVideoId: string, studentId: string) => {
    const res = await apiClient.post('/api/admin/overrides/reset-watch', { lessonVideoId, studentId });
    return res.data;
  }
};
