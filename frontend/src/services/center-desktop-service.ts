import apiClient from './api-client';

export interface DesktopApp { version: string; build: string; role: 'host' | 'client'; os: string }
export interface DesktopReceipt {
  receiptId: string; uploadId: string; centerId: string;
  sha256: string; bundleSha256: string; receivedAt: string; createdAt: string;
  size: number; app: DesktopApp;
}
export interface DesktopStatus { configured: boolean; available: boolean; message: string }
export interface DesktopUploads { uploads: DesktopReceipt[]; nextCursor: string }
export interface DesktopEvent {
  id: string; session: string; kind: 'session' | 'error'; time: string;
  version: string; build?: string; role?: string; platform: string; operation: string;
  errors?: { type: string; code?: number }[];
  frames?: { file: string; frame: number; line: number; column: number }[];
}
export interface DesktopDiagnostics {
  receipt: DesktopReceipt; kind: 'database' | 'diagnostics';
  events: DesktopEvent[]; total: number; truncated: boolean;
}
export interface DesktopRelease {
  platform: string; role: 'host' | 'client'; status: 'available' | 'missing' | 'invalid';
  manifest?: {
    releaseId: string; version: string; build: string; platform: string; role: string;
    size: number; sha256: string; downloadPath: string; notes?: string;
  };
}
const options = (signal: AbortSignal) => ({ signal, suppressErrorToast: true });
export async function getDesktopStatus(signal: AbortSignal) {
  return (await apiClient.get<{ data: DesktopStatus }>(`/admin/center-desktop/status`, options(signal))).data.data;
}
export async function getDesktopUploads(signal: AbortSignal, after = '') {
  return (await apiClient.get<{ data: DesktopUploads }>(`/admin/center-desktop/uploads`, {
    ...options(signal), params: { limit: 50, ...(after ? { after } : {}) },
  })).data.data;
}
export async function getDesktopDiagnostics(id: string, signal: AbortSignal) {
  return (await apiClient.get<{ data: DesktopDiagnostics }>(`/admin/center-desktop/uploads/${encodeURIComponent(id)}/diagnostics`, options(signal))).data.data;
}
export async function getDesktopReleases(signal: AbortSignal) {
  return (await apiClient.get<{ data: { releases: DesktopRelease[] } }>(`/admin/center-desktop/releases`, options(signal))).data.data.releases;
}
export async function downloadDesktopUpload(id: string, signal: AbortSignal) {
  return (await apiClient.get<Blob>(`/admin/center-desktop/uploads/${encodeURIComponent(id)}/download`, {
    ...options(signal), responseType: 'blob', timeout: 180_000,
  })).data;
}

export interface DesktopStudent {
  id: string; name: string; code: string; barcode: string; phone: string;
  guardianPhone: string; notes: string; discountPercent: number | null;
  suspended: boolean; groups: string[];
}
interface DesktopLesson { group: string; number: number | null; month: number | null; date: string }
export interface DesktopStudentSearch {
  students: DesktopStudent[]; total: number;
  profile: null | {
    present: number; absent: number; attendanceTotal: number; examTotal: number;
    attendances: { id: string; status: string; lesson: DesktopLesson }[];
    exams: { id: string; score: number | null; maxScore: number | null; absent: boolean; homework: string; lesson: DesktopLesson }[];
  };
}
export async function searchDesktopStudents(id: string, q: string, studentId: string | null, signal: AbortSignal) {
  return (await apiClient.get<{ data: DesktopStudentSearch }>(`/admin/center-desktop/uploads/${encodeURIComponent(id)}/students`, {
    ...options(signal), params: { q, ...(studentId ? { studentId } : {}) }, timeout: 180_000,
  })).data.data;
}
