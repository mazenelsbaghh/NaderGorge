import apiClient from './api-client';
import type { AcademicScopePayload, AcademicScopeSummary } from '@/lib/academic-labels';

export type SalesTargetType = 'Package' | 'Term' | 'ContentSection' | 'Lesson' | 'SpecificVideo' | 'VideoType' | 'PublicExam' | 'Teacher' | 'Platform';
export type DiscountType = 'Percentage' | 'FixedAmount';
export type SalesOwnerType = 'Platform' | 'Teacher';
export type SalesStatus = 'Draft' | 'Active' | 'Disabled' | 'Expired' | 'Archived' | 'Consumed';
export type StackingMode = 'SingleOnly' | 'AllowCouponAndPrintedCode' | 'AllowMultipleWithCap';
export type PrintableCodeBehavior = 'Discount' | 'DirectAccess' | 'PromotionalCredit';

export interface SalesCouponDto {
  id: string;
  code: string;
  name: string;
  discountType: DiscountType;
  discountValue: number;
  targetType: SalesTargetType;
  targetId?: string | null;
  ownerType: SalesOwnerType;
  teacherId?: string | null;
  status: SalesStatus;
  usedCount: number;
  startsAt?: string | null;
  expiresAt?: string | null;
  stackingPolicyId?: string | null;
  globalUsageLimit?: number | null;
  perStudentUsageLimit?: number | null;
  disableReason?: string | null;
  createdAt?: string;
  updatedAt?: string | null;
  recentUsages?: Array<{
    id: string;
    studentId: string;
    studentName: string;
    targetType: SalesTargetType;
    targetId: string;
    grossAmount: number;
    discountAmount: number;
    createdAt: string;
  }>;
  academicScopes?: AcademicScopeSummary[] | null;
}

export interface StackingPolicyDto {
  id: string;
  name: string;
  mode: StackingMode;
  maxDiscountPercentage?: number | null;
  maxDiscountAmount?: number | null;
  isDefault: boolean;
  isActive: boolean;
}

export interface PrintableTemplateDto {
  id: string;
  name: string;
  widthMm: number;
  heightMm: number;
  backgroundColor?: string | null;
  backgroundImageUrl?: string | null;
  layoutJson: string;
  isActive: boolean;
}

export interface PrintableBatchDto {
  id: string;
  name: string;
  behavior: PrintableCodeBehavior;
  targetType: SalesTargetType;
  targetId?: string | null;
  ownerType: SalesOwnerType;
  teacherId?: string | null;
  totalCodes: number;
  usedCount: number;
  status: SalesStatus;
  sampleCodes: Array<{ id: string; code: string; serialNumber: number; qrPayload: string; status: SalesStatus }>;
  academicScopes?: AcademicScopeSummary[] | null;
}

export interface PublicExamProductDto {
  id: string;
  examId: string;
  examTitle: string;
  slug: string;
  isPublished: boolean;
  isPaid: boolean;
  price: number;
  teacherId?: string | null;
  subjectId?: string | null;
  gradeLevel?: string | null;
  isPlatformWide: boolean;
  availableFrom?: string | null;
  availableUntil?: string | null;
  disabledAt?: string | null;
  hasAccess?: boolean;
  hasCompletedAttempt?: boolean;
  latestAttemptId?: string | null;
  latestAttemptIsPassed?: boolean | null;
  latestAttemptScore?: number | null;
  academicScopes?: AcademicScopeSummary[] | null;
}

export interface CreatePublicExamDto {
  title: string;
  description?: string | null;
  slug: string;
  teacherId?: string | null;
  subjectId: string;
  gradeLevel?: string | null;
  isPublished: boolean;
  isPaid: boolean;
  price: number;
  passingScore: number;
  totalScore: number;
  durationMinutes?: number | null;
  isRandomized: boolean;
  availableFrom?: string | null;
  availableUntil?: string | null;
  academicScopes?: AcademicScopePayload[];
}

export interface PublicExamResultsDto {
  productId: string;
  examId: string;
  examTitle: string;
  slug: string;
  price: number;
  isPaid: boolean;
  attemptCount: number;
  passedCount: number;
  averageScore: number;
  attempts: Array<{
    attemptId: string;
    studentId: string;
    studentName: string;
    studentPhone: string;
    startedAt?: string | null;
    submittedAt: string;
    scoreAchieved: number;
    isPassed: boolean;
    isTimeExpired: boolean;
    evaluation: string;
  }>;
  questions: Array<{
    examQuestionId: string;
    text: string;
    points: number;
    totalAnswers: number;
    correctAnswers: number;
    correctPercentage: number;
  }>;
}

function unwrap<T>(response: { data?: { data?: T } }): T {
  const data = response.data?.data;
  if (data === undefined) throw new Error('استجابة الخادم غير مكتملة.');
  return data;
}

export const adminSalesService = {
  async coupons() {
    return unwrap<SalesCouponDto[]>(await apiClient.get('/admin/sales/coupons'));
  },
  async coupon(id: string) {
    return unwrap<SalesCouponDto>(await apiClient.get(`/admin/sales/coupons/${id}`));
  },
  async createCoupon(payload: Record<string, unknown>) {
    return unwrap<SalesCouponDto>(await apiClient.post('/admin/sales/coupons', payload));
  },
  async updateCoupon(id: string, payload: Record<string, unknown>) {
    return unwrap<SalesCouponDto>(await apiClient.put(`/admin/sales/coupons/${id}`, payload));
  },
  async stackingPolicies() {
    return unwrap<StackingPolicyDto[]>(await apiClient.get('/admin/sales/stacking-policies'));
  },
  async saveStackingPolicy(payload: Record<string, unknown>) {
    return unwrap<StackingPolicyDto>(await apiClient.post('/admin/sales/stacking-policies', payload));
  },
  async templates() {
    return unwrap<PrintableTemplateDto[]>(await apiClient.get('/admin/sales/templates'));
  },
  async saveTemplate(payload: Record<string, unknown>) {
    return unwrap<PrintableTemplateDto>(await apiClient.post('/admin/sales/templates', payload));
  },
  async uploadTemplateBackground(file: File) {
    const formData = new FormData();
    formData.append('image', file);
    const response = await apiClient.post('/admin/sales/templates/background-image', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    const data = unwrap<{ url?: string; Url?: string }>(response);
    return data.url ?? data.Url ?? '';
  },
  async batches() {
    return unwrap<PrintableBatchDto[]>(await apiClient.get('/admin/sales/printable-batches'));
  },
  async createBatch(payload: Record<string, unknown>) {
    return unwrap<PrintableBatchDto>(await apiClient.post('/admin/sales/printable-batches', payload));
  },
  async publicExams() {
    return unwrap<PublicExamProductDto[]>(await apiClient.get('/admin/public-exams'));
  },
  async savePublicExam(payload: Record<string, unknown>) {
    return unwrap<PublicExamProductDto>(await apiClient.post('/admin/public-exams', payload));
  },
  async createPublicExam(payload: CreatePublicExamDto) {
    return unwrap<PublicExamProductDto>(await apiClient.post('/admin/public-exams/new', payload));
  },
  async publicExamResults(productId: string) {
    return unwrap<PublicExamResultsDto>(await apiClient.get(`/admin/public-exams/${productId}/results`));
  },
};
