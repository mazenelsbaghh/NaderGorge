import apiClient from './api-client';
import { invalidateMany } from '@/lib/cache-invalidation';
import type { LiveSupportWhatsAppTemplate } from './live-support-service';

export type AssessmentKind = 'homework' | 'exam';
export interface AssessmentResultParameter { source: string; literal?: string | null }
export interface AssessmentParentNotificationSettings {
  enabled: boolean; templateId: string | null; templateFingerprint: string | null;
  parameters: AssessmentResultParameter[];
}
export const disabledParentNotification: AssessmentParentNotificationSettings = {
  enabled: false, templateId: null, templateFingerprint: null, parameters: [],
};
export interface RevisionOption { id: string; text: string; isCorrect: boolean }
export interface RevisionQuestion {
  id: string; bankQuestionId: string; order: number; type: number; text: string; points: number;
  audioUrl: string | null; imageUrl: string | null; writtenCorrection: string | null; hintText: string | null;
  baseText: string | null; mistakeStartIndex: number | null; mistakeEndIndex: number | null;
  options: RevisionOption[]; correctAnswerKey: string | null;
}
export interface AssessmentDefinition {
  schemaVersion: number; kind: AssessmentKind; assessmentId: string; title: string; description: string | null;
  totalScore: number; passingScore: number | null; durationMinutes: number | null;
  isMandatory: boolean; isRandomized: boolean; isActive: boolean; displayQuestionCount: number | null;
  questions: RevisionQuestion[];
  parentNotification?: AssessmentParentNotificationSettings;
}
export interface RevisionPolicy {
  previousAttempts: 'Preserve' | 'Regrade';
  removedQuestions: 'KeepPreviousGrade' | 'Exclude' | 'AwardFullPoints';
  addedQuestions: 'FutureAttemptsOnly' | 'RequestCompletion';
  manualGrades: 'Preserve' | 'ReturnForReview';
  scoreDecrease: 'Allow' | 'Prevent';
}
export const defaultRevisionPolicy: RevisionPolicy = {
  previousAttempts: 'Preserve', removedQuestions: 'KeepPreviousGrade',
  addedQuestions: 'FutureAttemptsOnly', manualGrades: 'Preserve', scoreDecrease: 'Prevent',
};
export interface AssessmentEditorResponse { definition: AssessmentDefinition; attemptCount: number; revisionToken: string }
export interface RevisionPreview {
  revisionToken: string; attemptCount: number; addedQuestions: number; removedQuestions: number; policy: RevisionPolicy;
  attempts: { attemptId: string; previousScore: number; revisedScore: number | null; requiresCompletion: boolean; requiresReview: boolean }[];
}
interface ApiEnvelope<T> { success: boolean; data: T; message?: string }
const endpoint = (kind: AssessmentKind, id: string) => `/admin/${kind === 'exam' ? 'exams' : 'homework'}/${id}`;
function unwrap<T>(response: { data: ApiEnvelope<T> }): T {
  if (!response.data.success) throw new Error(response.data.message || 'تعذر تنفيذ الطلب.');
  return response.data.data;
}
export const assessmentRevisionService = {
  notificationTemplates: async (kind: AssessmentKind) => unwrap(await apiClient.get<ApiEnvelope<LiveSupportWhatsAppTemplate[]>>(
    `/admin/${kind === 'exam' ? 'exams' : 'homework'}/notification-templates`)),
  load: async (kind: AssessmentKind, id: string) => unwrap(await apiClient.get<ApiEnvelope<AssessmentEditorResponse>>(`${endpoint(kind, id)}/editor`)),
  preview: async (definition: AssessmentDefinition, policy: RevisionPolicy) => unwrap(await apiClient.post<ApiEnvelope<RevisionPreview>>(
    `${endpoint(definition.kind, definition.assessmentId)}/revision-preview`, { definition, policy })),
  save: async (request: { definition: AssessmentDefinition; policy: RevisionPolicy; revisionToken: string; operationId: string; confirmPreviousAttempts: boolean; subjectId?: string }) => {
    const saved = unwrap(await apiClient.put<ApiEnvelope<AssessmentEditorResponse>>(
      `${endpoint(request.definition.kind, request.definition.assessmentId)}/definition`, request));
    invalidateMany(['assessments', 'student:homeworks', 'student:exams']);
    return saved;
  },
};
