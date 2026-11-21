import apiClient from './api-client';

export type AssessmentExportKind = 'exam' | 'homework';
export type AssessmentExportSelection = 'grades' | 'absent';

export async function exportAssessment(request: {
  audience: 'admin' | 'teacher'; kind: AssessmentExportKind; id: string; selection: AssessmentExportSelection;
}): Promise<Blob> {
  const resource = request.kind === 'exam' ? 'exams' : 'homework';
  const response = await apiClient.get<Blob>(`/${request.audience}/${resource}/${encodeURIComponent(request.id)}/export`, {
    params: { selection: request.selection }, responseType: 'blob', timeout: 120_000,
  });
  return response.data;
}
