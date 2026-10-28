import apiClient from './api-client';
import type { StudentProfileExtendedDto } from './admin-service';

export interface TeacherStudentProfile {
  profile: StudentProfileExtendedDto;
  canViewFinance: boolean;
  canViewReports: boolean;
}
export async function getTeacherStudentProfile(studentId: string, signal: AbortSignal): Promise<TeacherStudentProfile> {
  const response = await apiClient.get<{ success: boolean; data: TeacherStudentProfile; message?: string }>(`/teacher/students/${studentId}/profile`, { signal });
  if (!response.data.success) throw new Error(response.data.message || 'تعذر تحميل ملف الطالب.');
  return response.data.data;
}
