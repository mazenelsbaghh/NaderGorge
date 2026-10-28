import apiClient from './api-client';

export async function exportTeacherCourseSummary(selection: { stage: number; packageId: string }, signal: AbortSignal): Promise<Blob> {
  return (await apiClient.post<Blob>('/teacher/reports/summary/export', selection, { responseType: 'blob', signal })).data;
}
