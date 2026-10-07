import api from '@/services/api-client';
import type { McpConnection, McpTool, MimDocument, MimSnapshot, MimSource } from './contract';

const root = '/admin/mim-studio';
const unwrap = <T>(response: { data: { data: T } }) => response.data.data;
export const mimStudioService = {
  read: (lessonId: string, signal?: AbortSignal) => api.get<{ data: MimSnapshot | null }>(`${root}/lessons/${lessonId}`, { signal }).then(unwrap),
  sources: (lessonId: string, signal?: AbortSignal) => api.get<{ data: MimSource[] }>(`${root}/lessons/${lessonId}/sources`, { signal }).then(unwrap),
  save: (lessonId: string, version: string | null, source: MimSource, document: MimDocument) =>
    api.put<{ data: MimSnapshot }>(`${root}/lessons/${lessonId}`, { version, sourceVideoId: source.id, sourceRevision: source.sourceRevision, document }).then(unwrap),
  connection: (signal?: AbortSignal) => api.get<{ data: McpConnection }>(`${root}/connection`, { signal }).then(unwrap),
  connect: () => api.post<{ data: { authorizationUrl: string } }>(`${root}/connection/start`, {}, { timeout: 50000 }).then(unwrap),
  complete: (code: string, state: string, issuer: string | null) => api.post<{ data: McpConnection }>(`${root}/connection/complete`, { code, state, issuer }, { timeout: 50000 }).then(unwrap),
  disconnect: () => api.delete(`${root}/connection`),
  tools: () => api.get<{ data: McpTool[] }>(`${root}/connection/tools`, { timeout: 90000 }).then(unwrap),
};

export function studioError(error: unknown, fallback = 'تعذر إكمال الطلب. حاول مرة أخرى.'): string {
  const message = (error as { response?: { data?: { message?: unknown } } })?.response?.data?.message;
  return typeof message === 'string' ? message : fallback;
}
