import apiClient from './api-client';
import type { AcademicScopePayload, AcademicScopeSummary } from '@/lib/academic-labels';
import { invalidateMany } from '@/lib/cache-invalidation';

export interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data: T;
}

export interface SharedPackageTeacherInput {
  teacherId: string;
  subjectId?: string;
  allocationMode: number;
  allocationValue: number;
  displayOrder: number;
}

export interface SharedPackageItemInput {
  teacherId: string;
  subjectId?: string;
  contentType: number;
  contentId: string;
  price: number;
}

export interface SaveSharedPackagePayload {
  name: string;
  slug?: string;
  description?: string;
  imageUrl?: string;
  price: number;
  distributionMode: number;
  isPublished: boolean;
  educationStage?: string;
  gradeLevel?: string;
  availableFrom?: string;
  availableUntil?: string;
  teachers: SharedPackageTeacherInput[];
  items: SharedPackageItemInput[];
  academicScopes?: AcademicScopePayload[];
}

export interface SharedPackageListItem {
  id: string;
  name: string;
  slug?: string;
  description?: string;
  imageUrl?: string;
  price: number;
  isPublished?: boolean;
  teacherCount?: number;
  educationStage?: string;
  gradeLevel?: string;
  academicScopes?: AcademicScopeSummary[];
}

export interface PurchasedSharedPackageTeacher {
  teacherId: string;
  teacherName: string;
  teacherProfileImageUrl?: string;
  subjectId?: string;
  subjectName?: string;
  contentCount: number;
  contentName: string;
  contentUrl: string;
}

export interface PurchasedSharedPackage {
  id: string;
  sharedPackageId: string;
  name: string;
  description?: string;
  imageUrl?: string;
  price: number;
  purchasedAt: string;
  teachers: PurchasedSharedPackageTeacher[];
}

export interface SharedPackageTeacherDetail {
  teacherId: string;
  teacherName: string;
  teacherProfileImageUrl?: string;
  subjectId?: string;
  subjectName?: string;
  allocationMode: number | string;
  allocationValue: number;
}

export interface SharedPackageContentItem {
  id: string;
  teacherId: string;
  teacherName?: string;
  subjectId?: string;
  subjectName?: string;
  contentType: string;
  contentTypeValue: number;
  contentId: string;
  price: number;
  contentName: string;
}

export interface SharedPackageDetail extends SharedPackageListItem {
  teachers: SharedPackageTeacherDetail[];
  items: SharedPackageContentItem[];
}

export interface PurchaseSharedPackagePayload {
  selections: { subjectId?: string; teacherId?: string }[];
}

export const sharedPackageService = {
  listAdmin: async (): Promise<SharedPackageListItem[]> => {
    const res = await apiClient.get<ApiResponse<SharedPackageListItem[]>>('/admin/shared-packages');
    return res.data?.data ?? [];
  },

  createAdmin: async (payload: SaveSharedPackagePayload): Promise<ApiResponse<{ id: string }>> => {
    const res = await apiClient.post<ApiResponse<{ id: string }>>('/admin/shared-packages', payload);
    if (res.data.success) invalidateMany(['shared-packages:admin', 'shared-packages:student']);
    return res.data;
  },

  publishAdmin: async (id: string): Promise<ApiResponse<boolean>> => {
    const res = await apiClient.post<ApiResponse<boolean>>(`/admin/shared-packages/${id}/publish`);
    if (res.data.success) invalidateMany(['shared-packages:admin', 'shared-packages:student', 'content:packages']);
    return res.data;
  },

  uploadAdminImage: async (id: string, image: File, onProgress?: (percent: number) => void): Promise<string> => {
    const formData = new FormData();
    formData.append('image', image);
    const res = await apiClient.post<ApiResponse<string>>(`/admin/shared-packages/${id}/image`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
      onUploadProgress: (progressEvent) => {
        if (progressEvent.total && onProgress) {
          onProgress(Math.round((progressEvent.loaded * 100) / progressEvent.total));
        }
      },
    });
    if (res.data.success) invalidateMany(['shared-packages:admin', 'shared-packages:student']);
    return res.data.data;
  },

  listStudent: async (): Promise<SharedPackageListItem[]> => {
    const res = await apiClient.get<ApiResponse<SharedPackageListItem[]>>('/student/shared-packages');
    return res.data?.data ?? [];
  },

  listPurchasedStudent: async (): Promise<PurchasedSharedPackage[]> => {
    const res = await apiClient.get<ApiResponse<PurchasedSharedPackage[]>>('/student/shared-packages/purchased');
    return res.data?.data ?? [];
  },

  detailStudent: async (id: string): Promise<SharedPackageDetail> => {
    const res = await apiClient.get<ApiResponse<SharedPackageDetail>>(`/student/shared-packages/${id}`);
    return res.data.data;
  },

  purchaseStudent: async (id: string, payload?: PurchaseSharedPackagePayload): Promise<ApiResponse<any>> => {
    const res = await apiClient.post<ApiResponse<any>>(`/student/shared-packages/${id}/purchase`, payload ?? { selections: [] });
    if (res.data.success) invalidateMany(['shared-packages:student', 'student:shell', 'reports']);
    return res.data;
  },
};
