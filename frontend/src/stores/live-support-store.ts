import { create } from 'zustand';
import { removeConversationDraft, updateConversationDraft } from '@/lib/conversation-drafts';

interface LiveSupportClientState {
  selectedConversationId?: string;
  drafts: Record<string, string>;
  processedEventIds: string[];
  lastSequenceByConversation: Record<string, number>;
  ownershipLost: Record<string, boolean>;
  selectConversation: (conversationId?: string) => void;
  setDraft: (conversationId: string, draft: string) => void;
  clearDraft: (conversationId: string) => void;
  markEventProcessed: (eventId: string) => boolean;
  recordSequence: (conversationId: string, sequence: number) => void;
  setOwnershipLost: (conversationId: string, lost: boolean) => void;
  reset: () => void;
}

const MAX_PROCESSED_EVENT_IDS = 500;

export const useLiveSupportStore = create<LiveSupportClientState>((set, get) => ({
  drafts: {},
  processedEventIds: [],
  lastSequenceByConversation: {},
  ownershipLost: {},
  selectConversation: (selectedConversationId) => set({ selectedConversationId }),
  setDraft: (conversationId, draft) => set((state) => ({ drafts: updateConversationDraft(state.drafts, conversationId, draft) })),
  clearDraft: (conversationId) => set((state) => ({ drafts: removeConversationDraft(state.drafts, conversationId) })),
  markEventProcessed: (eventId) => {
    if (get().processedEventIds.includes(eventId)) return false;
    set((state) => ({ processedEventIds: [...state.processedEventIds, eventId].slice(-MAX_PROCESSED_EVENT_IDS) }));
    return true;
  },
  recordSequence: (conversationId, sequence) => set((state) => ({ lastSequenceByConversation: { ...state.lastSequenceByConversation, [conversationId]: Math.max(sequence, state.lastSequenceByConversation[conversationId] ?? 0) } })),
  setOwnershipLost: (conversationId, lost) => set((state) => ({ ownershipLost: { ...state.ownershipLost, [conversationId]: lost } })),
  reset: () => set({ selectedConversationId: undefined, drafts: {}, processedEventIds: [], lastSequenceByConversation: {}, ownershipLost: {} }),
}));
