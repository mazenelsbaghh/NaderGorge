export const liveSupportAIDecisionTypes = [
  'reply',
  'propose_action',
  'request_verification',
  'propose_account_creation',
  'request_resolution',
  'handoff',
] as const;

export type LiveSupportAIDecisionType = typeof liveSupportAIDecisionTypes[number];
export type LiveSupportAIMode = 'AiActive' | 'AiResolved' | 'HumanQueued' | 'HumanAssigned';
export type LiveSupportAIPendingDecisionKind = 'Action' | 'Handoff' | 'AccountCreation' | 'Resolution';

export const liveSupportAIPaths = {
  snapshot: (conversationId: string) => `/live-support/participant/conversations/${conversationId}/snapshot`,
  actionConfirm: (conversationId: string, decisionId: string) =>
    `/live-support/participant/conversations/${conversationId}/ai/actions/${decisionId}/confirm`,
  actionCancel: (conversationId: string, decisionId: string) =>
    `/live-support/participant/conversations/${conversationId}/ai/actions/${decisionId}/cancel`,
  handoffConfirm: (conversationId: string, decisionId: string) =>
    `/live-support/participant/conversations/${conversationId}/ai/handoff/${decisionId}/confirm`,
  handoffCancel: (conversationId: string, decisionId: string) =>
    `/live-support/participant/conversations/${conversationId}/ai/handoff/${decisionId}/cancel`,
  registrationConfirm: (conversationId: string, decisionId: string) =>
    `/live-support/participant/conversations/${conversationId}/ai/registration/${decisionId}/confirm`,
} as const;
