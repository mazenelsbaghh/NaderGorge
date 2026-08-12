import apiClient from './api-client';
import {
  adminAiAgentPaths,
  adminAiRequestConfig,
  type AdminAiConversationSnapshot,
  type AdminAiConversationSummary,
  type AdminAiAuditEvidence,
  type AdminAiExecution,
  type AdminAiProposal,
  type AdminAiSecureGrant,
  type AdminAiSecureInputKind,
  type AdminAiTurn,
} from './admin-ai-agent-contract';

interface DataEnvelope<T> {
  data: T;
}
interface ConversationPage {
  items: AdminAiConversationSummary[];
  nextCursor?: string;
}
interface AuditEvidencePage {
  items: AdminAiAuditEvidence[];
  nextCursor: string | null;
}

const config = adminAiRequestConfig;

export const adminAiAgentService = {
  list: (
    signal: AbortSignal,
    cursor?: string,
    status?: 'Active' | 'Archived'
  ) =>
    apiClient
      .get<
        DataEnvelope<ConversationPage>
      >(adminAiAgentPaths.conversations, { ...config(signal), params: { cursor, status } })
      .then((response) => response.data.data),
  create: (signal: AbortSignal, idempotencyKey: string, title?: string) =>
    apiClient
      .post<
        DataEnvelope<AdminAiConversationSummary>
      >(adminAiAgentPaths.conversations, { title }, config(signal, idempotencyKey))
      .then((response) => response.data.data),
  snapshot: (
    signal: AbortSignal,
    conversationId: string,
    beforeSequence?: number
  ) =>
    apiClient
      .get<DataEnvelope<AdminAiConversationSnapshot>>(
        adminAiAgentPaths.snapshot(conversationId),
        {
          ...config(signal),
          params: { beforeSequence, pageSize: 50 },
        }
      )
      .then((response) => response.data.data),
  rename: (
    signal: AbortSignal,
    conversationId: string,
    title: string,
    expectedVersion: number,
    idempotencyKey: string
  ) =>
    apiClient
      .patch<
        DataEnvelope<AdminAiConversationSummary>
      >(adminAiAgentPaths.conversation(conversationId), { title, expectedVersion }, config(signal, idempotencyKey))
      .then((response) => response.data.data),
  archive: (
    signal: AbortSignal,
    conversationId: string,
    expectedVersion: number,
    idempotencyKey: string
  ) =>
    apiClient
      .post<
        DataEnvelope<AdminAiConversationSummary>
      >(adminAiAgentPaths.archiveConversation(conversationId), { expectedVersion }, config(signal, idempotencyKey))
      .then((response) => response.data.data),
  restore: (
    signal: AbortSignal,
    conversationId: string,
    expectedVersion: number,
    idempotencyKey: string
  ) =>
    apiClient
      .post<
        DataEnvelope<AdminAiConversationSummary>
      >(adminAiAgentPaths.restoreConversation(conversationId), { expectedVersion }, config(signal, idempotencyKey))
      .then((response) => response.data.data),
  send: (
    signal: AbortSignal,
    conversationId: string,
    message: string,
    expectedConversationVersion: number,
    idempotencyKey: string
  ) =>
    apiClient
      .post<
        DataEnvelope<AdminAiTurn>
      >(adminAiAgentPaths.turns(conversationId), { message, expectedConversationVersion }, config(signal, idempotencyKey))
      .then((response) => response.data.data),
  cancelTurn: (
    signal: AbortSignal,
    conversationId: string,
    turnId: string,
    expectedVersion: number,
    idempotencyKey: string
  ) =>
    apiClient
      .post<
        DataEnvelope<AdminAiTurn>
      >(adminAiAgentPaths.cancelTurn(conversationId, turnId), { expectedVersion }, config(signal, idempotencyKey))
      .then((response) => response.data.data),
  proposal: (signal: AbortSignal, proposalId: string) =>
    apiClient
      .get<
        DataEnvelope<AdminAiProposal>
      >(adminAiAgentPaths.proposal(proposalId), config(signal))
      .then((response) => response.data.data),
  confirmProposal: (
    signal: AbortSignal,
    proposalId: string,
    expectedVersion: number,
    idempotencyKey: string,
    typedPhrase?: string
  ) =>
    apiClient
      .post<
        DataEnvelope<AdminAiExecution>
      >(adminAiAgentPaths.confirmProposal(proposalId), { expectedVersion, ...(typedPhrase === undefined ? {} : { typedPhrase }) }, config(signal, idempotencyKey))
      .then((response) => response.data.data),
  cancelProposal: (
    signal: AbortSignal,
    proposalId: string,
    expectedVersion: number,
    idempotencyKey: string
  ) =>
    apiClient
      .post<
        DataEnvelope<AdminAiProposal>
      >(adminAiAgentPaths.cancelProposal(proposalId), { expectedVersion }, config(signal, idempotencyKey))
      .then((response) => response.data.data),
  issueSecureGrant: (
    signal: AbortSignal,
    proposalId: string,
    inputKind: AdminAiSecureInputKind,
    expectedProposalVersion: number,
    idempotencyKey: string
  ) =>
    apiClient
      .post<
        DataEnvelope<AdminAiSecureGrant>
      >(adminAiAgentPaths.secureGrant(proposalId), { inputKind, expectedProposalVersion }, config(signal, idempotencyKey))
      .then((response) => response.data.data),
  submitSecureInput: (
    signal: AbortSignal,
    grantId: string,
    input:
      | { kind: Exclude<AdminAiSecureInputKind, 'PrivateFile'>; value: string }
      | { kind: 'PrivateFile'; privateObjectToken: string },
    idempotencyKey: string
  ) =>
    apiClient
      .post<
        DataEnvelope<AdminAiSecureGrant>
      >(adminAiAgentPaths.secureSubmit(grantId), input, config(signal, idempotencyKey))
      .then((response) => response.data.data),
  actionEvidence: (signal: AbortSignal, cursor?: string) =>
    apiClient
      .get<DataEnvelope<AuditEvidencePage>>(adminAiAgentPaths.actionEvidence, {
        ...config(signal),
        params: { cursor, pageSize: 25 },
      })
      .then((response) => response.data.data),
};

export function createAdminAiIntentKey(): string {
  return crypto.randomUUID();
}
