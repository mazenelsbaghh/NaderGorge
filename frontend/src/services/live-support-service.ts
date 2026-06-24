import apiClient from './api-client';

export type LiveSupportConversationStatus = 'Waiting' | 'Assigned' | 'Active' | 'Closed' | 'Abandoned';
export type LiveSupportParticipantType = 'Student' | 'Guest';
export type LiveSupportMessageType = 'Text' | 'Image' | 'Pdf' | 'Audio' | 'System';
export type LiveSupportAIMode = 'AiActive' | 'HumanQueued' | 'HumanAssigned' | 'AiResolved' | 'Failed' | 'Closed';
export type LiveSupportAITurnState = 'Queued' | 'Processing' | 'ProviderCompleted' | 'Completed' | 'Failed' | 'DiscardedAfterHandoff' | 'DiscardedAfterDisable' | 'Cancelled';
export type LiveSupportPendingDecisionKind = 'Action' | 'Handoff' | 'AccountCreation' | 'Resolution';

export interface LiveSupportAvailability {
  isAvailable: boolean;
  availableStaffCount: number;
  nextAvailableAt?: string | null;
  code: string;
  message: string;
}

export interface LiveSupportAISummary {
  handoffSafeSummary?: string | null;
  handoffReasonCode?: string | null;
  policyVersion?: number | null;
  verificationStatus?: string | null;
  attemptedActionKeys: string[];
  failedTurnErrors: string[];
}

export interface LiveSupportConversation {
  id: string;
  status: LiveSupportConversationStatus;
  participantType: LiveSupportParticipantType;
  subject?: string;
  queuePosition?: number;
  currentOwnerName?: string;
  currentOwnerUserId?: string;
  linkedStudentUserId?: string;
  createdAt: string;
  queuedAt?: string;
  assignedAt?: string;
  closedAt?: string;
  version: number;
  canSend: boolean;
  canRate: boolean;
  isAiActive?: boolean;
  isAiTyping?: boolean;
  aiSummary?: LiveSupportAISummary | null;
}

export interface LiveSupportMessage {
  id: string;
  conversationId: string;
  senderType: 'Student' | 'Guest' | 'Staff' | 'Admin' | 'System' | 'AI';
  clientMessageId: string;
  type: LiveSupportMessageType;
  content: string;
  sentAt: string;
}

export interface LiveSupportMessagePage {
  items: LiveSupportMessage[];
  nextCursor?: string;
  lastEventSequence: number;
  missedEvents: Array<{ at: string; type: string; summary: string; safeDetails?: string }>;
}
export interface LiveSupportAttachment { id: string; fileName: string; contentType: string; sizeBytes: number; downloadUrl: string; }

export interface LiveSupportActionDefinition {
  key: string;
  category: 'Identity' | 'Account' | 'Devices' | 'Packages' | 'Balance' | 'Watch' | 'Academic' | 'Gamification' | 'CRM' | 'Notes';
  labelAr: string;
  danger: 'low' | 'medium' | 'high' | 'financial';
  reasonRequired: boolean;
  confirmationVersion: string;
  refreshSections: string[];
}

export interface LiveSupportStaffBootstrap {
  isCheckedIn: boolean;
  isEnabled: boolean;
  activeLoad: number;
  capacity: number;
  waitingCount: number;
  conversations: LiveSupportConversation[];
}

export interface LiveSupportScheduleWindow {
  dayOfWeek: number;
  startLocalTime: string;
  endLocalTime: string;
}

export interface LiveSupportStaffConfig {
  userId: string;
  staffName: string;
  isEnabled: boolean;
  maxActiveConversations: number;
  activeLoad: number;
  isCheckedIn: boolean;
  version: number;
  schedule: LiveSupportScheduleWindow[];
}

export interface LiveSupportAdminConfig {
  featureEnabled: boolean;
  staff: LiveSupportStaffConfig[];
}

export interface LiveSupportStudentSearchResult { userId: string; fullName: string; maskedPhone: string; studentCode?: string; }
export interface LiveSupportStudentContext {
  userId: string; fullName: string; phoneNumber: string; isActive: boolean; studentCode?: string;
  governorate?: string; schoolName?: string; educationStage?: string; gradeLevel?: string;
  balance: number; points: number; level?: string; crmStatus?: string; crmPriority?: string;
  devices: Array<{ id: string; name?: string; type?: string; os?: string; browser?: string; lastUsedAt: string; isActive: boolean }>;
  grants: Array<{ id: string; grantType: string; packageId?: string; grantedAt: string; expiresAt?: string; isActive: boolean }>;
  notes: Array<{ id: string; content: string; isPinned: boolean; createdAt: string }>;
  watchEvents: number; examAttempts: number; homeworkSubmissions: number;
}
export type LiveSupportStudentContextSectionKey = 'basic' | 'metrics' | 'study' | 'devices' | 'notes' | 'crm';
export interface LiveSupportStudentContextSections {
  basic: Pick<LiveSupportStudentContext, 'fullName' | 'phoneNumber' | 'isActive' | 'studentCode' | 'governorate' | 'schoolName' | 'educationStage' | 'gradeLevel'>;
  metrics: { balance: number; points: number; examAttempts: number; devicesCount: number };
  study: { activeGrants: number; watchEvents: number; homeworkSubmissions: number };
  devices: Pick<LiveSupportStudentContext, 'devices'>;
  notes: Pick<LiveSupportStudentContext, 'notes'>;
  crm: { status?: string; priority?: string };
}
export interface LiveSupportAdminConversation { id: string; participantName: string; participantType: LiveSupportParticipantType; status: LiveSupportConversationStatus; ownerName?: string; createdAt: string; assignedAt?: string; firstResponseAt?: string; closedAt?: string; waitSeconds?: number; handleSeconds?: number; subject?: string; aiTurnStatus?: string; aiTurnFailureCode?: string; }
export interface LiveSupportStaffPerformance { staffUserId: string; staffName: string; participatedConversations: number; closedConversations: number; ratingCount: number; averageRating?: number; }
export interface LiveSupportAdminDashboard { waitingCount: number; activeCount: number; closedToday: number; conversations: LiveSupportAdminConversation[]; staffPerformance: LiveSupportStaffPerformance[]; }
export interface LiveSupportConversationTimeline { conversation: LiveSupportAdminConversation; items: Array<{ at: string; type: string; actorName?: string; summary: string; safeDetails?: string }>; ratingStars?: number; ratingComment?: string; }

export interface LiveSupportAIVerificationSession {
  sessionId: string;
  status: string;
  nextQuestionKey?: string | null;
  promptText?: string | null;
  attemptCount: number;
  maxAttempts: number;
}

export interface LiveSupportAIPendingDecision {
  id: string;
  kind: LiveSupportPendingDecisionKind;
  actionKey: string;
  safeProposalJson: string;
  status: 'PendingConfirmation' | 'Confirmed' | 'Cancelled' | 'Expired' | 'Invalidated' | 'Executing' | 'Succeeded' | 'Failed';
  expiresAt: string;
  failureCode?: string | null;
}

export interface LiveSupportAIParticipantSnapshot {
  conversationId: string;
  status: LiveSupportConversationStatus;
  aiMode?: LiveSupportAIMode | null;
  lastSequence: number;
  canSend: boolean;
  aiTurnState?: LiveSupportAITurnState | null;
  pendingDecision?: LiveSupportAIPendingDecision | null;
  verification?: LiveSupportAIVerificationSession | null;
  queuePosition?: number | null;
  messages: LiveSupportMessage[];
}

export interface LiveSupportRegisterGuestPayload {
  decisionId: string;
  idempotencyKey?: string;
  fullName: string;
  phoneNumber: string;
  password: string;
  dateOfBirth: string;
  gender: 'Male' | 'Female';
  governorate: string;
  address: string;
  educationStage: string;
  gradeLevel: string;
  schoolName: string;
  parentPhoneNumber: string;
}

interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data: T;
}

export function getLiveSupportApiError(error: unknown, fallback: string) {
  const response = (error as { response?: { data?: { message?: unknown } } } | null)?.response;
  return typeof response?.data?.message === 'string' ? response.data.message : fallback;
}

export const liveSupportService = {
  getAvailability: () =>
    apiClient.get<ApiResponse<LiveSupportAvailability>>('/live-support/availability').then((response) => response.data.data),

  createGuestSession: (payload: { displayName: string; phoneNumber: string }) =>
    apiClient.post<ApiResponse<{ id: string; displayName: string; expiresAt: string }>>('/live-support/guest/session', payload).then((response) => response.data.data),

  listParticipantConversations: () =>
    apiClient.get<ApiResponse<LiveSupportConversation[]>>('/live-support/participant/conversations').then((response) => response.data.data),

  createConversation: (payload: { subject?: string; previousConversationId?: string }) =>
    apiClient.post<ApiResponse<LiveSupportConversation>>('/live-support/participant/conversations', payload).then((response) => response.data.data),

  getParticipantConversation: (conversationId: string) =>
    apiClient.get<ApiResponse<LiveSupportConversation>>(`/live-support/participant/conversations/${conversationId}`).then((response) => response.data.data),

  getMessagePage: (conversationId: string, cursor?: string, afterSequence?: number) =>
    apiClient.get<ApiResponse<LiveSupportMessagePage>>(`/live-support/participant/conversations/${conversationId}/messages`, { params: { pageSize: 50, cursor, afterSequence } }).then((response) => response.data.data),

  getMessages: (conversationId: string) =>
    liveSupportService.getMessagePage(conversationId).then((page) => page.items),

  getParticipantAISnapshot: (conversationId: string) =>
    apiClient.get<ApiResponse<LiveSupportAIParticipantSnapshot>>(`/live-support/participant/conversations/${conversationId}/ai/snapshot`).then((response) => response.data.data),

  uploadAttachment: (conversationId: string, file: File) => {
    const body = new FormData(); body.append('file', file);
    return apiClient.post<ApiResponse<LiveSupportAttachment>>(`/live-support/participant/conversations/${conversationId}/attachments`, body).then((response) => response.data.data);
  },

  sendParticipantMessage: (conversationId: string, payload: { clientMessageId: string; type: LiveSupportMessageType; content?: string; attachmentId?: string }) =>
    apiClient.post<ApiResponse<{ message: LiveSupportMessage; replayed: boolean }>>(`/live-support/participant/conversations/${conversationId}/messages`, payload).then((response) => response.data.data.message),

  abandonConversation: (conversationId: string) =>
    apiClient.post<ApiResponse<LiveSupportConversation>>(`/live-support/participant/conversations/${conversationId}/abandon`).then((response) => response.data.data),

  submitRating: (conversationId: string, payload: { stars: number; comment?: string }) =>
    apiClient.post<ApiResponse<{ id: string }>>(`/live-support/participant/conversations/${conversationId}/rating`, payload).then((response) => response.data.data),

  getStaffBootstrap: () =>
    apiClient.get<ApiResponse<LiveSupportStaffBootstrap>>('/live-support/staff/bootstrap').then((response) => response.data.data),

  sendStaffMessage: (conversationId: string, payload: { clientMessageId: string; content: string }) =>
    apiClient.post<ApiResponse<{ message: LiveSupportMessage; replayed: boolean }>>(`/live-support/staff/conversations/${conversationId}/messages`, payload).then((response) => response.data.data.message),

  getStaffMessages: (conversationId: string) =>
    apiClient.get<ApiResponse<LiveSupportMessage[]>>(`/live-support/staff/conversations/${conversationId}/messages`, { params: { pageSize: 100 } }).then((response) => response.data.data),

  closeConversation: (conversationId: string, reason: string) =>
    apiClient.post<ApiResponse<LiveSupportConversation>>(`/live-support/staff/conversations/${conversationId}/close`, { reason }).then((response) => response.data.data),

  transferConversation: (conversationId: string, reason: string, targetStaffUserId?: string) =>
    apiClient.post<ApiResponse<LiveSupportConversation>>(`/live-support/staff/conversations/${conversationId}/transfer`, { reason, targetStaffUserId }).then((response) => response.data.data),

  getAdminConfig: () =>
    apiClient.get<ApiResponse<LiveSupportAdminConfig>>('/live-support/admin/config').then((response) => response.data.data),

  getAdminDashboard: () =>
    apiClient.get<ApiResponse<LiveSupportAdminDashboard>>('/live-support/admin/dashboard').then((response) => response.data.data),

  getAdminTimeline: (conversationId: string) =>
    apiClient.get<ApiResponse<LiveSupportConversationTimeline>>(`/live-support/admin/conversations/${conversationId}/timeline`).then((response) => response.data.data),

  intervene: (conversationId: string, operation: 'close' | 'reassign' | 'queue' | 'abandon', reason: string, targetStaffUserId?: string) =>
    apiClient.post<ApiResponse<LiveSupportConversation>>(`/live-support/admin/conversations/${conversationId}/intervene`, { operation, reason, targetStaffUserId }).then((response) => response.data.data),

  setFeatureEnabled: (enabled: boolean) =>
    apiClient.put('/live-support/admin/feature', { enabled }),

  updateStaffConfig: (staffUserId: string, payload: { enabled: boolean; capacity: number; expectedVersion: number; schedule: LiveSupportScheduleWindow[] }) =>
    apiClient.put<ApiResponse<LiveSupportStaffConfig>>(`/live-support/admin/staff/${staffUserId}`, payload).then((response) => response.data.data),

  searchStudents: (conversationId: string, query: string) =>
    apiClient.get<ApiResponse<LiveSupportStudentSearchResult[]>>(`/live-support/staff/conversations/${conversationId}/students/search`, { params: { query } }).then((response) => response.data.data),

  changeStudentLink: (conversationId: string, studentUserId: string | null, reason: string, expectedVersion: number) =>
    apiClient.put<ApiResponse<LiveSupportConversation>>(`/live-support/staff/conversations/${conversationId}/student-link`, { studentUserId, reason, expectedVersion }).then((response) => response.data.data),

  getStudentContext: (conversationId: string) =>
    apiClient.get<ApiResponse<LiveSupportStudentContext>>(`/live-support/staff/conversations/${conversationId}/student-context`).then((response) => response.data.data),

  getStudentContextSection: <K extends LiveSupportStudentContextSectionKey>(conversationId: string, section: K) =>
    apiClient.get<ApiResponse<{ section: K; data: LiveSupportStudentContextSections[K] }>>(`/live-support/staff/conversations/${conversationId}/student-context/${section}`).then((response) => response.data.data.data),

  getActionCatalog: (conversationId: string) =>
    apiClient.get<ApiResponse<LiveSupportActionDefinition[]>>(`/live-support/staff/conversations/${conversationId}/actions`).then((response) => response.data.data),

  executeStudentAction: <TPayload extends Record<string, unknown>, TResult>(conversationId: string, actionKey: string, idempotencyKey: string, confirmationVersion: string, payload: TPayload) =>
    apiClient.post<ApiResponse<TResult>>(`/live-support/staff/conversations/${conversationId}/actions/${actionKey}`, { confirmationVersion, payload }, { headers: { 'Idempotency-Key': idempotencyKey } }).then((response) => response.data.data),

  confirmAIAction: (conversationId: string, proposalId: string, idempotencyKey = crypto.randomUUID()) =>
    apiClient.post<ApiResponse<{ decisionId: string; executionId: string; status: string }>>(`/live-support/participant/conversations/${conversationId}/ai/decisions/${proposalId}/confirm`, { idempotencyKey }).then((response) => response.data.data),

  cancelAIAction: (conversationId: string, proposalId: string, idempotencyKey = crypto.randomUUID()) =>
    apiClient.post<ApiResponse<{ decisionId: string; status: string }>>(`/live-support/participant/conversations/${conversationId}/ai/decisions/${proposalId}/cancel`, { idempotencyKey }).then((response) => response.data.data),

  confirmAIHandoff: (conversationId: string) =>
    apiClient.post<ApiResponse<{ success: boolean; message: string }>>(`/live-support/participant/conversations/${conversationId}/ai/handoff/confirm`).then((response) => response.data.data),

  cancelAIHandoff: (conversationId: string) =>
    apiClient.post<ApiResponse<{ success: boolean; message: string }>>(`/live-support/participant/conversations/${conversationId}/ai/handoff/cancel`).then((response) => response.data.data),

  aiVerificationLookup: (conversationId: string, payload: { lookupKey: string; value: string; idempotencyKey?: string }) =>
    apiClient.post<ApiResponse<LiveSupportAIVerificationSession>>(`/live-support/participant/conversations/${conversationId}/ai/verification/lookup`, { ...payload, idempotencyKey: payload.idempotencyKey ?? crypto.randomUUID() }).then((response) => response.data.data),

  aiVerificationAnswer: (conversationId: string, payload: { sessionId: string; answer: string; idempotencyKey?: string }) =>
    apiClient.post<ApiResponse<LiveSupportAIVerificationSession>>(`/live-support/participant/conversations/${conversationId}/ai/verification/${payload.sessionId}/answer`, { answer: payload.answer, idempotencyKey: payload.idempotencyKey ?? crypto.randomUUID() }).then((response) => response.data.data),

  confirmAIRegistration: (conversationId: string, payload: LiveSupportRegisterGuestPayload) =>
    apiClient.post<ApiResponse<{ userId: string; status: string }>>(`/live-support/participant/conversations/${conversationId}/ai/decisions/${payload.decisionId}/register`, { ...payload, idempotencyKey: payload.idempotencyKey ?? crypto.randomUUID() }).then((response) => response.data.data),

  getActivePendingAction: (conversationId: string) =>
    apiClient.get<ApiResponse<LiveSupportAIPendingDecision | null>>(`/live-support/participant/conversations/${conversationId}/ai/pending-action`).then((response) => response.data.data),

  getActiveVerificationSession: (conversationId: string) =>
    apiClient.get<ApiResponse<LiveSupportAIVerificationSession | null>>(`/live-support/participant/conversations/${conversationId}/ai/verification/session`).then((response) => response.data.data),
};
