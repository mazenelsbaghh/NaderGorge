import apiClient from './api-client';

export type FollowUpAudience = 'admin' | 'teacher';
export type FollowUpState = 'All' | 'Incomplete' | 'NotStarted' | 'Started' | 'Completed';
export interface FollowUpFilter {
  teacherId: string; packageId: string; lessonIds: string[];
  stage?: number; grade?: number; state: FollowUpState; minimumDays: number;
  search: string; page: number; pageSize: number;
}
export interface FollowUpOptions {
  teachers: Array<{ id: string; name: string }>;
  packages: Array<{ id: string; name: string }>;
  lessons: Array<{ id: string; title: string; termTitle: string; order: number }>;
}
export interface FollowUpRow {
  studentId: string; studentName: string; phone: string; parentPhone: string | null;
  stage: string; grade: string; lessonId: string; lessonTitle: string; packageName: string;
  state: 'NotStarted' | 'Started' | 'Completed'; completedVideos: number; totalVideos: number;
  progressPercentage: number | null; availableSinceUtc: string | null; daysWaiting: number | null;
  availabilitySource: 'VideoOpened' | 'RecordedPlayback' | 'Unknown'; lastWatchedAtUtc: string | null;
}
export interface FollowUpReport {
  rows: FollowUpRow[]; totalRows: number; totalStudents: number;
  completed: number; started: number; notStarted: number; overdue: number;
  unknownAvailability: number; playbackDatedAvailability: number;
  page: number; pageSize: number; generatedAtUtc: string;
}
interface Envelope<T> { success: boolean; data: T; message?: string }
function data<T>(value: Envelope<T>): T {
  if (!value.success) throw new Error(value.message || 'تعذر تحميل متابعة الطلاب.');
  return value.data;
}
const path = (audience: FollowUpAudience) => `/${audience}/student-follow-up`;
export const studentFollowUpService = {
  options: async (audience: FollowUpAudience, teacherId?: string, packageId?: string, signal?: AbortSignal) =>
    data((await apiClient.get<Envelope<FollowUpOptions>>(`${path(audience)}/options`, {
      params: { teacherId: teacherId || undefined, packageId: packageId || undefined }, signal,
    })).data),
  read: async (audience: FollowUpAudience, filter: FollowUpFilter, signal?: AbortSignal) =>
    data((await apiClient.post<Envelope<FollowUpReport>>(path(audience), filter, { signal })).data),
  export: async (audience: FollowUpAudience, filter: FollowUpFilter) =>
    (await apiClient.post<Blob>(`${path(audience)}/export`, filter, { responseType: 'blob' })).data,
};
