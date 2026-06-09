import apiClient from './api-client';

export type MediaStage = 'Preparation' | 'Filming' | 'Editing' | 'Uploading' | 'Review' | 'Approved' | 'Published';

export type SocialPlatform = 'YouTube' | 'Facebook' | 'Instagram' | 'TikTok' | 'Telegram';

export type SocialPlanStatus = 'Draft' | 'Scripting' | 'Scheduled' | 'Published';

export interface MediaPipelineDto {
  id: string;
  title: string;
  description?: string;
  stage: MediaStage;
  assignedAgentId?: string;
  assignedAgentName?: string;
  assetFolderUrl?: string;
  editingErrorCount: number;
  publishedAt?: string;
  createdAt: string;
}

export interface MediaPipelineListResponse {
  items: MediaPipelineDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CreateMediaPipelinePayload {
  title: string;
  description?: string;
  assignedAgentId?: string;
  assetFolderUrl?: string;
}

export interface UpdateMediaPipelinePayload {
  title: string;
  description?: string;
  assignedAgentId?: string;
  assetFolderUrl?: string;
  editingErrorCount: number;
  stage: MediaStage;
  supervisorId?: string;
}

export interface SocialMediaPlanDto {
  id: string;
  title: string;
  description?: string;
  script?: string;
  platform: SocialPlatform;
  status: SocialPlanStatus;
  scheduledDate: string;
  mediaProductionPipelineId?: string;
  mediaProductionPipelineTitle?: string;
  mediaProductionPipelineStage?: MediaStage;
  createdAt: string;
}

export interface CreateSocialPlanPayload {
  title: string;
  description?: string;
  script?: string;
  platform: SocialPlatform;
  status: SocialPlanStatus;
  scheduledDate: string;
  mediaProductionPipelineId?: string;
}

export interface MediaKpisDto {
  totalPublished: number;
  averageEditingDays: number;
  editorLeaderboard: EditorPerformanceDto[];
}

export interface EditorPerformanceDto {
  editorId: string;
  editorName: string;
  totalProduced: number;
  totalErrors: number;
}

interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data: T;
}

export const mediaService = {
  getPipelines: (params: { search?: string; stage?: MediaStage; assigneeId?: string; page?: number; pageSize?: number }) => {
    const query = new URLSearchParams();
    if (params.search) query.append('search', params.search);
    if (params.stage) query.append('stage', params.stage);
    if (params.assigneeId) query.append('assigneeId', params.assigneeId);
    if (params.page) query.append('page', params.page.toString());
    if (params.pageSize) query.append('pageSize', params.pageSize.toString());

    return apiClient.get<ApiResponse<MediaPipelineListResponse>>(`/admin/media/pipelines?${query.toString()}`)
      .then(r => r.data.data);
  },

  createPipeline: (payload: CreateMediaPipelinePayload) =>
    apiClient.post<ApiResponse<MediaPipelineDto>>('/admin/media/pipelines', payload)
      .then(r => r.data),

  updatePipeline: (id: string, payload: UpdateMediaPipelinePayload) =>
    apiClient.put<ApiResponse<void>>(`/admin/media/pipelines/${id}`, payload)
      .then(r => r.data),

  getSocialPlans: (params?: { startDate?: string; endDate?: string }) => {
    const query = new URLSearchParams();
    if (params?.startDate) query.append('startDate', params.startDate);
    if (params?.endDate) query.append('endDate', params.endDate);
    return apiClient.get<ApiResponse<SocialMediaPlanDto[]>>(`/admin/media/social-plans?${query.toString()}`)
      .then(r => r.data.data);
  },

  createSocialPlan: (payload: CreateSocialPlanPayload) =>
    apiClient.post<ApiResponse<SocialMediaPlanDto>>('/admin/media/social-plans', payload)
      .then(r => r.data),

  getMediaKpis: () =>
    apiClient.get<ApiResponse<MediaKpisDto>>('/admin/media/reports/kpis')
      .then(r => r.data.data)
};
