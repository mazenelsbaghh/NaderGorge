import apiClient from './api-client';
import type { FollowUpAudience, FollowUpFilter, FollowUpOptions, FollowUpReport, FollowUpRow } from './student-follow-up-service';

export interface ReportStudent { id: string; name: string; phone: string }
export interface ReportVideo { id: string; title: string; state: FollowUpRow['state']; progressPercentage: number | null; lastWatchedAtUtc: string | null }
export interface LessonStudentFilter extends FollowUpFilter { studentId?: string; attendance: 'All' | 'present' | 'absent' }
export interface LessonStudentReport extends Omit<FollowUpReport, 'rows'> { rows: Array<FollowUpRow & { videos: ReportVideo[] }> }
interface Envelope<T> { success: boolean; data: T; message?: string }
function unwrap<T>(response: Envelope<T>): T {
  if (!response.success) throw new Error(response.message || 'تعذر تحميل التقرير.');
  return response.data;
}
const path = (audience: FollowUpAudience) => `/${audience}/reports/learning`;
export const lessonStudentReportService = {
  options: async (audience: FollowUpAudience, selection: { teacherId?: string; packageId?: string }, signal: AbortSignal) =>
    unwrap((await apiClient.get<Envelope<FollowUpOptions>>(`${path(audience)}/options`, { params: selection, signal })).data),
  students: async (audience: FollowUpAudience, search: { teacherId: string; packageId: string; search: string }, signal: AbortSignal) =>
    unwrap((await apiClient.post<Envelope<{ students: ReportStudent[]; hasMore: boolean }>>(`${path(audience)}/students`, search, { signal })).data),
  read: async (audience: FollowUpAudience, filter: LessonStudentFilter, signal: AbortSignal) =>
    unwrap((await apiClient.post<Envelope<LessonStudentReport>>(path(audience), filter, { signal })).data),
  export: async (audience: FollowUpAudience, filter: LessonStudentFilter) =>
    (await apiClient.post<Blob>(`${path(audience)}/export`, filter, { responseType: 'blob' })).data,
};

export function reportError(reason: unknown): string {
  const response = (reason as { response?: { data?: { message?: string } } }).response;
  return response?.data?.message || (reason instanceof Error && !response ? reason.message : 'تعذر تحميل التقرير. أعد المحاولة.');
}
