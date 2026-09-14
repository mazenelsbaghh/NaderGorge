export type ConversationDrafts = Record<string, string>;

export function updateConversationDraft(drafts: ConversationDrafts, conversationId: string, value: string): ConversationDrafts {
  return { ...drafts, [conversationId]: value };
}

export function removeConversationDraft(drafts: ConversationDrafts, conversationId: string): ConversationDrafts {
  const next = { ...drafts };
  delete next[conversationId];
  return next;
}
