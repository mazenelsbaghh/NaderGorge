export type AdminAiConversationStatus = 'Active' | 'Archived';
export type AdminAiTurnStatus =
  | 'Queued'
  | 'Planning'
  | 'Retrieving'
  | 'Answering'
  | 'WaitingClarification'
  | 'ProposalReady'
  | 'Completed'
  | 'CancelRequested'
  | 'Cancelled'
  | 'Failed'
  | 'AccessRevoked';
export type AdminAiProposalStatus =
  | 'PendingSecureInput'
  | 'PendingConfirmation'
  | 'Confirming'
  | 'Executing'
  | 'Succeeded'
  | 'PartiallySucceeded'
  | 'Cancelled'
  | 'Expired'
  | 'Invalidated'
  | 'Rejected'
  | 'Failed'
  | 'RecoveryRequired';
export type AdminAiRisk =
  | 'Ordinary'
  | 'Destructive'
  | 'Financial'
  | 'Permission'
  | 'Security'
  | 'AccountDisable'
  | 'Credential'
  | 'Bulk'
  | 'ExternalSideEffect';
export type AdminAiRefreshScope =
  | 'identity'
  | 'content'
  | 'commercial'
  | 'finance'
  | 'hr'
  | 'support'
  | 'reporting'
  | 'other';
export type AdminAiRouteKey =
  | 'admin.student.details'
  | 'admin.teacher.details'
  | 'admin.content.lesson'
  | 'admin.assessment.exam'
  | 'admin.finance.transaction'
  | 'admin.hr.employee'
  | 'admin.support.conversation';

export const ADMIN_AI_ERROR_CODES = [
  'ADMIN_AI_DISABLED',
  'ADMIN_AI_BASELINE_UNAVAILABLE',
  'UNAUTHORIZED',
  'ADMIN_REQUIRED',
  'ACCESS_REVOKED',
  'CONVERSATION_NOT_FOUND',
  'TRANSCRIPT_FORBIDDEN',
  'CONVERSATION_ARCHIVED',
  'VERSION_CONFLICT',
  'ACTIVE_TURN_EXISTS',
  'ACTIVE_TURN_LIMIT',
  'TURN_NOT_CANCELLABLE',
  'TURN_CANCELLED',
  'CAPABILITY_NOT_ALLOWED',
  'CAPABILITY_BASELINE_CHANGED',
  'REQUEST_AMBIGUOUS',
  'RESULT_LIMIT_EXCEEDED',
  'PROHIBITED_DATA_REQUEST',
  'PROPOSAL_NOT_CONFIRMABLE',
  'PROPOSAL_EXPIRED',
  'PROPOSAL_STALE',
  'PROPOSAL_INVALIDATED',
  'STRONG_CONFIRMATION_REQUIRED',
  'CONFIRMATION_PHRASE_MISMATCH',
  'CONFIRMATION_CHALLENGE_LOCKED',
  'SECURE_INPUT_REQUIRED',
  'SECURE_INPUT_EXPIRED',
  'IDEMPOTENCY_PAYLOAD_CONFLICT',
  'EXECUTION_ALREADY_STARTED',
  'EXECUTION_RECOVERY_REQUIRED',
  'RATE_LIMITED',
  'AI_PROVIDER_TIMEOUT',
  'AI_PROVIDER_FAILURE',
  'AI_INVALID_DECISION',
  'QUEUE_UNAVAILABLE',
  'DEPENDENCY_FAILURE',
  'UNKNOWN_SAFE_FAILURE',
] as const;
export type AdminAiErrorCode = (typeof ADMIN_AI_ERROR_CODES)[number];
export interface AdminAiApiError {
  code: AdminAiErrorCode;
  messageAr: string;
  retryAfterSeconds: number | null;
  traceId: string;
  currentVersion: number | null;
}

export function parseAdminAiApiError(
  raw: unknown
): AdminAiApiError | undefined {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return undefined;
  const value = raw as Record<string, unknown>;
  const allowed = new Set([
    'code',
    'messageAr',
    'retryAfterSeconds',
    'traceId',
    'currentVersion',
  ]);
  if (Object.keys(value).some((key) => !allowed.has(key))) return undefined;
  if (
    typeof value.code !== 'string' ||
    !ADMIN_AI_ERROR_CODES.includes(value.code as AdminAiErrorCode)
  )
    return undefined;
  if (typeof value.messageAr !== 'string' || value.messageAr.length > 500)
    return undefined;
  if (typeof value.traceId !== 'string' || value.traceId.length > 64)
    return undefined;
  if (
    value.retryAfterSeconds !== null &&
    (!Number.isSafeInteger(value.retryAfterSeconds) ||
      Number(value.retryAfterSeconds) < 1)
  )
    return undefined;
  if (
    value.currentVersion !== null &&
    (!Number.isSafeInteger(value.currentVersion) ||
      Number(value.currentVersion) < 1)
  )
    return undefined;
  return value as unknown as AdminAiApiError;
}

export function adminAiRequestConfig(
  signal: AbortSignal,
  idempotencyKey?: string
) {
  return {
    signal,
    suppressErrorToast: true,
    headers: idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : undefined,
  };
}

/**
 * The Admin AI API previously returned closed DTOs directly, whereas the
 * platform convention wraps them in `{ data }`. Supporting both shapes keeps
 * a rolling release from creating a conversation that the browser cannot
 * select or display.
 */
export function unwrapAdminAiPayload<T>(payload: unknown): T {
  if (
    payload &&
    typeof payload === 'object' &&
    !Array.isArray(payload) &&
    Object.prototype.hasOwnProperty.call(payload, 'data')
  ) {
    return (payload as { data: T }).data;
  }
  return payload as T;
}
export interface AdminAiConversationSummary {
  id: string;
  title: string;
  status: AdminAiConversationStatus;
  lastActivityAt: string;
  version: number;
}
export interface AdminAiDrillDown {
  labelAr: string;
  routeKey: AdminAiRouteKey;
  routeParams: Record<string, string>;
}
export interface AdminAiEvidence {
  capabilityKey: string;
  capabilityVersion: string;
  scope: string[];
  filters: string[];
  resultCount: number;
  isComplete: boolean;
  isTruncated: boolean;
  dataAsOf: string;
  drillDown: AdminAiDrillDown[];
}
export interface AdminAiGroundedAnswer {
  facts: string[];
  calculations: string[];
  inferences: string[];
  limitations: string[];
  suggestions: string[];
  evidence: AdminAiEvidence[];
}
export interface AdminAiMessage {
  id: string;
  sequence: number;
  role: 'Admin' | 'Assistant' | 'Status';
  content: string;
  answer?: AdminAiGroundedAnswer | null;
  turnId?: string | null;
  createdAt: string;
}
export interface AdminAiTurn {
  id: string;
  conversationId?: string;
  status: AdminAiTurnStatus;
  step?: number;
  reads?: number;
  safeProgressLabelAr?: string;
  failureCode?: string | null;
  queuedAt: string;
  startedAt?: string | null;
  completedAt?: string | null;
  canCancel?: boolean;
  canRetry?: boolean;
  version: number;
}
export interface AdminAiFieldChange {
  labelAr: string;
  currentValue: unknown;
  requestedValue: unknown;
  displayKind:
    | 'Text'
    | 'Number'
    | 'Date'
    | 'Money'
    | 'Boolean'
    | 'Status'
    | 'Count'
    | 'Reference';
}
export interface AdminAiBulkSummary {
  selectionRuleAr: string;
  candidateCount: number;
  excludedCount: number;
  representativeItems: string[];
  semantics: 'Atomic' | 'Partial';
  partialFailureBehaviorAr: string;
}
export interface AdminAiExecutionItem {
  safeReference: string;
  status:
    | 'Succeeded'
    | 'Skipped'
    | 'ValidationFailed'
    | 'AuthorizationFailed'
    | 'Stale'
    | 'DependencyFailed'
    | 'SystemFailed';
  safeMessageAr: string;
}
export interface AdminAiExecution {
  id: string;
  proposalId: string;
  status:
    | 'Claimed'
    | 'Executing'
    | 'Succeeded'
    | 'PartiallySucceeded'
    | 'Rejected'
    | 'Failed'
    | 'RecoveryRequired';
  safeSummaryAr: string;
  affectedCount: number | null;
  succeededCount: number | null;
  skippedCount: number | null;
  failedCount: number | null;
  items: AdminAiExecutionItem[];
  refreshScopes: AdminAiRefreshScope[];
  failureCode: string | null;
  traceId: string;
  startedAt: string | null;
  completedAt: string | null;
}
export interface AdminAiProposal {
  id: string;
  conversationId: string;
  turnId: string;
  capabilityKey: string;
  capabilityLabelAr: string;
  targetLabelAr: string;
  targetDrillDown: AdminAiDrillDown | null;
  changes: AdminAiFieldChange[];
  effectSummaryAr: string;
  consequenceAr: string | null;
  primaryRisk: AdminAiRisk;
  riskFlags: AdminAiRisk[];
  confirmationType: 'Explicit' | 'TypedStrong';
  strongConfirmationPhrase: string | null;
  validationSummary: string[];
  bulk: AdminAiBulkSummary | null;
  requiresSecureInput: boolean;
  secureInputKind: AdminAiSecureInputKind | null;
  status: AdminAiProposalStatus;
  expiresAt: string;
  execution: AdminAiExecution | null;
  version: number;
}
export interface AdminAiConversationSnapshot {
  conversation: AdminAiConversationSummary;
  messages: AdminAiMessage[];
  activeTurns?: AdminAiTurn[];
  turns?: AdminAiTurn[];
  proposals?: AdminAiProposal[];
  nextBeforeSequence?: number | null;
  nextCursor?: string;
  latestSequence?: number;
  sequence?: number;
  baselineVersion?: string;
  sensitivePolicyVersion?: string;
  serverTime?: string;
}
export type AdminAiSecureInputKind =
  | 'Password'
  | 'PrivateFile'
  | 'ProtectedToken'
  | 'VerificationAnswer';
export interface AdminAiSecureGrant {
  id: string;
  proposalId: string;
  inputKind: AdminAiSecureInputKind;
  status:
    | 'Issued'
    | 'Submitted'
    | 'Consumed'
    | 'Cancelled'
    | 'Expired'
    | 'Purged';
  safeMetadata: {
    fileName: string | null;
    mimeType: string | null;
    sizeBytes: number | null;
  };
  expiresAt: string;
}
export interface AdminAiAuditEvidence {
  eventId: string;
  eventType: string;
  actorAdminUserId: string | null;
  capabilityKey: string | null;
  safeTargetReference: string | null;
  risk: AdminAiRisk | null;
  confirmationStatus: string | null;
  resultStatus: string | null;
  safeSummaryAr: string;
  traceId: string;
  occurredAt: string;
}

export const adminAiAgentPaths = {
  conversations: '/admin/ai-agent/conversations',
  conversation: (id: string) =>
    `/admin/ai-agent/conversations/${encodeURIComponent(id)}`,
  archiveConversation: (id: string) =>
    `/admin/ai-agent/conversations/${encodeURIComponent(id)}/archive`,
  restoreConversation: (id: string) =>
    `/admin/ai-agent/conversations/${encodeURIComponent(id)}/restore`,
  snapshot: (id: string) =>
    `/admin/ai-agent/conversations/${encodeURIComponent(id)}/snapshot`,
  turns: (id: string) =>
    `/admin/ai-agent/conversations/${encodeURIComponent(id)}/turns`,
  cancelTurn: (conversationId: string, turnId: string) =>
    `/admin/ai-agent/conversations/${encodeURIComponent(conversationId)}/turns/${encodeURIComponent(turnId)}/cancel`,
  proposal: (id: string) =>
    `/admin/ai-agent/proposals/${encodeURIComponent(id)}`,
  confirmProposal: (id: string) =>
    `/admin/ai-agent/proposals/${encodeURIComponent(id)}/confirm`,
  cancelProposal: (id: string) =>
    `/admin/ai-agent/proposals/${encodeURIComponent(id)}/cancel`,
  secureGrant: (id: string) =>
    `/admin/ai-agent/proposals/${encodeURIComponent(id)}/secure-input-grants`,
  secureSubmit: (id: string) =>
    `/admin/ai-agent/secure-input-grants/${encodeURIComponent(id)}/submit`,
  actionEvidence: '/admin/ai-agent/action-evidence',
} as const;

export const ADMIN_AI_ROUTE_BUILDERS: Record<
  AdminAiRouteKey,
  (p: Record<string, string>) => string | null
> = {
  'admin.student.details': (p) =>
    p.id ? `/admin/students/${encodeURIComponent(p.id)}` : null,
  'admin.teacher.details': (p) =>
    p.id ? `/admin/teachers/${encodeURIComponent(p.id)}` : null,
  'admin.content.lesson': (p) =>
    p.id ? `/admin/content/lessons/${encodeURIComponent(p.id)}` : null,
  'admin.assessment.exam': (p) =>
    p.id ? `/admin/exams/${encodeURIComponent(p.id)}` : null,
  'admin.finance.transaction': (p) =>
    p.id ? `/admin/finance/transactions/${encodeURIComponent(p.id)}` : null,
  'admin.hr.employee': (p) =>
    p.id ? `/admin/hr/employees/${encodeURIComponent(p.id)}` : null,
  'admin.support.conversation': (p) =>
    p.id ? `/admin/live-support/${encodeURIComponent(p.id)}` : null,
};
