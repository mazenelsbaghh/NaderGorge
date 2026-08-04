import apiClient from './api-client';

export interface SystemLogEntry {
  id: string;
  timestamp: string;
  source: 'backend' | 'worker';
  level: 'warning' | 'error' | 'critical';
  category: string;
  message: string;
  exception?: string | null;
}

export async function getSystemLogs(params: {
  level?: string;
  source?: string;
  search?: string;
  from?: string;
  to?: string;
  errorsOnly?: boolean;
  limit?: number;
}) {
  const response = await apiClient.get<{ data: SystemLogEntry[] }>('/admin/system-logs', { params });
  return response.data.data;
}

export async function deleteSystemLogs(ids: string[]) {
  const response = await apiClient.post<{ data: { deletedCount: number } }>('/admin/system-logs/delete', { ids });
  return response.data.data.deletedCount;
}

export async function clearAllSystemLogs() {
  const response = await apiClient.delete<{ data: { deletedCount: number } }>('/admin/system-logs/all');
  return response.data.data.deletedCount;
}

export async function exportSystemLogs(params: {
  level?: string;
  source?: string;
  search?: string;
  from?: string;
  to?: string;
  errorsOnly?: boolean;
}) {
  const response = await apiClient.get<Blob>('/admin/system-logs/export', {
    params,
    responseType: 'blob',
  });
  return response.data;
}
