import { create } from 'zustand';
import {
  decideAdminAiSequence,
  type AdminAiRealtimeEnvelope,
} from '../../lib/admin-ai-agent-client-contract.ts';

type ConnectionState =
  | 'disconnected'
  | 'connecting'
  | 'connected'
  | 'reconnecting';
type ResponsiveView = 'history' | 'conversation';

interface AdminAiAgentState {
  selectedConversationId?: string;
  draft: string;
  responsiveView: ResponsiveView;
  connection: ConnectionState;
  lastSequenceByConversation: Record<string, number>;
  recentEventIds: string[];
  inFlightIntents: Record<string, string>;
  selectConversation: (id?: string) => void;
  setDraft: (draft: string) => void;
  setResponsiveView: (view: ResponsiveView) => void;
  setConnection: (connection: ConnectionState) => void;
  beginIntent: (name: string, key: string) => boolean;
  finishIntent: (name: string, key: string) => void;
  acceptEvent: (
    event: AdminAiRealtimeEnvelope
  ) => 'accept' | 'duplicate' | 'reconcile';
  clearSecurityBoundary: () => void;
}

const emptyState = {
  selectedConversationId: undefined,
  draft: '',
  responsiveView: 'history' as const,
  connection: 'disconnected' as const,
  lastSequenceByConversation: {},
  recentEventIds: [],
  inFlightIntents: {},
};

export const useAdminAiAgentStore = create<AdminAiAgentState>((set, get) => ({
  ...emptyState,
  selectConversation: (selectedConversationId) =>
    set({
      selectedConversationId,
      responsiveView: selectedConversationId ? 'conversation' : 'history',
    }),
  setDraft: (draft) => set({ draft: draft.slice(0, 8000) }),
  setResponsiveView: (responsiveView) => set({ responsiveView }),
  setConnection: (connection) => set({ connection }),
  beginIntent: (name, key) => {
    if (get().inFlightIntents[name]) return false;
    set((state) => ({
      inFlightIntents: { ...state.inFlightIntents, [name]: key },
    }));
    return true;
  },
  finishIntent: (name, key) =>
    set((state) => {
      if (state.inFlightIntents[name] !== key) return state;
      const inFlightIntents = { ...state.inFlightIntents };
      delete inFlightIntents[name];
      return { inFlightIntents };
    }),
  acceptEvent: (event) => {
    const state = get();
    if (state.recentEventIds.includes(event.eventId)) return 'duplicate';
    const decision = decideAdminAiSequence(
      state.lastSequenceByConversation[event.conversationId] ?? 0,
      event.sequence
    );
    if (decision !== 'duplicate')
      set({
        lastSequenceByConversation: {
          ...state.lastSequenceByConversation,
          [event.conversationId]: event.sequence,
        },
        recentEventIds: [...state.recentEventIds, event.eventId].slice(-200),
      });
    return decision;
  },
  clearSecurityBoundary: () => set(emptyState),
}));
