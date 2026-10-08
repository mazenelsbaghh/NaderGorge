import api from '@/services/api-client';
import type { McpConnection, McpTool, MimDocument, MimSnapshot, MimSource, MimVideo, MimVideoModel, MimEpisodeVideo } from './contract';

const root = '/admin/mim-studio';
const unwrap = <T>(response: { data: { data: T } }) => response.data.data;
export const mimStudioService = {
  read: (lessonId: string, signal?: AbortSignal) => api.get<{ data: MimSnapshot | null }>(`${root}/lessons/${lessonId}`, { signal }).then(unwrap),
  sources: (lessonId: string, signal?: AbortSignal) => api.get<{ data: MimSource[] }>(`${root}/lessons/${lessonId}/sources`, { signal }).then(unwrap),
  save: (lessonId: string, version: string | null, source: MimSource | undefined, document: MimDocument) =>
    api.put<{ data: MimSnapshot }>(`${root}/lessons/${lessonId}`, { version, sourceVideoId: source?.id ?? null, sourceRevision: source?.sourceRevision ?? 0, document }).then(unwrap),
  generateNext: (lessonId: string, version: string | null, source: MimSource | undefined, sourceText: string | null, writing: { expectedSceneCount: number; targetSceneCount: number; episodeContext: string | null }) =>
    api.post<{ data: MimSnapshot }>(`${root}/lessons/${lessonId}/scenes/next`, {
      version, sourceVideoId: source?.id ?? null, sourceRevision: source?.sourceRevision ?? 0, sourceText, ...writing,
    }, { timeout: 90000 }).then(unwrap),
  video: (lessonId: string, scene: number, signal?: AbortSignal) => api.get<{ data: MimVideo | null }>(`${root}/lessons/${lessonId}/scenes/${scene}/video`, { signal, timeout: 60000 }).then(unwrap),
  episodeVideo: (lessonId: string, signal?: AbortSignal) => api.get<{ data: MimEpisodeVideo }>(`${root}/lessons/${lessonId}/episode-video`, { signal }).then(unwrap),
  assembleEpisode: (lessonId: string, version: string) => api.post<{ data: MimEpisodeVideo }>(`${root}/lessons/${lessonId}/episode-video`, { version }).then(unwrap),
  episodeFile: (lessonId: string, signal?: AbortSignal) => api.get<Blob>(`${root}/lessons/${lessonId}/episode-video/file`, { responseType: 'blob', signal, timeout: 180000 }).then(response => response.data),
  models: (signal?: AbortSignal) => api.get<{ data: MimVideoModel[] }>(`${root}/video-models`, { signal }).then(unwrap),
  quoteVideo: (lessonId: string, scene: number, model: string) => api.post<{ data: MimVideo }>(`${root}/lessons/${lessonId}/scenes/${scene}/video/quote`, { model }, { timeout: 180000 }).then(unwrap),
  reviewVideo: (lessonId: string, scene: number, version: string, confirmedNoGenerationOrCharge: boolean) => api.post<{ data: MimVideo }>(`${root}/lessons/${lessonId}/scenes/${scene}/video/review`, { version, confirmedNoGenerationOrCharge }).then(unwrap),
  submitVideo: (lessonId: string, scene: number, version: string) => api.post<{ data: MimVideo }>(`${root}/lessons/${lessonId}/scenes/${scene}/video`, { version }, { timeout: 60000 }).then(unwrap),
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
