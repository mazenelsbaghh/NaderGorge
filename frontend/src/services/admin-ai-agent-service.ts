import apiClient from './api-client';
import { createClientId } from '@/lib/client-id';
import {
  adminAiAgentPaths,
  adminAiRequestConfig,
  unwrapAdminAiPayload,
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
      .then((response) =>
        unwrapAdminAiPayload<ConversationPage>(response.data)
      ),
  create: (signal: AbortSignal, idempotencyKey: string, title?: string) =>
    apiClient
      .post<
        DataEnvelope<AdminAiConversationSummary>
      >(adminAiAgentPaths.conversations, { title }, config(signal, idempotencyKey))
      .then((response) =>
        unwrapAdminAiPayload<AdminAiConversationSummary>(response.data)
      ),
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
      .then((response) =>
        unwrapAdminAiPayload<AdminAiConversationSnapshot>(response.data)
      ),
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
      .then((response) =>
        unwrapAdminAiPayload<AdminAiConversationSummary>(response.data)
      ),
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
      .then((response) =>
        unwrapAdminAiPayload<AdminAiConversationSummary>(response.data)
      ),
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
      .then((response) =>
        unwrapAdminAiPayload<AdminAiConversationSummary>(response.data)
      ),
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
      .then((response) => unwrapAdminAiPayload<AdminAiTurn>(response.data)),
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
      .then((response) => unwrapAdminAiPayload<AdminAiTurn>(response.data)),
  proposal: (signal: AbortSignal, proposalId: string) =>
    apiClient
      .get<
        DataEnvelope<AdminAiProposal>
      >(adminAiAgentPaths.proposal(proposalId), config(signal))
      .then((response) => unwrapAdminAiPayload<AdminAiProposal>(response.data)),
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
      .then((response) =>
        unwrapAdminAiPayload<AdminAiExecution>(response.data)
      ),
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
      .then((response) => unwrapAdminAiPayload<AdminAiProposal>(response.data)),
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
      .then((response) =>
        unwrapAdminAiPayload<AdminAiSecureGrant>(response.data)
      ),
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
      .then((response) =>
        unwrapAdminAiPayload<AdminAiSecureGrant>(response.data)
      ),
  actionEvidence: (signal: AbortSignal, cursor?: string) =>
    apiClient
      .get<DataEnvelope<AuditEvidencePage>>(adminAiAgentPaths.actionEvidence, {
        ...config(signal),
        params: { cursor, pageSize: 25 },
      })
      .then((response) =>
        unwrapAdminAiPayload<AuditEvidencePage>(response.data)
      ),
};

export function createAdminAiIntentKey(): string {
  return createClientId();
}
