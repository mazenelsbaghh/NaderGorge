import apiClient from './api-client';

export interface TeacherSummaryFilter { stage?: number; grade?: number; packageId?: string }
export interface TeacherSummaryReport {
  columns: Array<{ key: string; label: string; type: string }>;
  rows: Array<Record<string, string>>;
  totalStudents: number; page: number; pageSize: number;
}
export async function readTeacherCourseSummary(selection: TeacherSummaryFilter & { page: number }, signal: AbortSignal): Promise<TeacherSummaryReport> {
  const response = await apiClient.post<{ success: boolean; data: TeacherSummaryReport; message?: string }>('/teacher/reports/summary', selection, { signal });
  if (!response.data.success) throw new Error(response.data.message || 'تعذر عرض التقرير.');
  return response.data.data;
}
export async function exportTeacherCourseSummary(selection: TeacherSummaryFilter, signal: AbortSignal): Promise<Blob> {
  return (await apiClient.post<Blob>('/teacher/reports/summary/export', selection, { responseType: 'blob', signal })).data;
}
