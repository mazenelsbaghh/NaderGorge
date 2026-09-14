'use client';
import { useCallback, useEffect } from 'react';
import { parseAdminAiRealtimeEnvelope } from '@/lib/admin-ai-agent-client-contract';
import { useAdminAiAgentStore } from '@/features/admin-ai-agent/admin-ai-agent-store';
import { usePlatformEvents } from './usePlatformEvents';

interface AdminAiEventCallbacks {
  onRefresh: () => void | Promise<void>;
  onListRefresh: () => void | Promise<void>;
  onAccessRevoked: () => void;
}

export function useAdminAiAgentEvents(callbacks: AdminAiEventCallbacks) {
  const acceptEvent = useAdminAiAgentStore((state) => state.acceptEvent);
  const setConnection = useAdminAiAgentStore((state) => state.setConnection);
  const receiveEvent = useCallback(
    (payload: unknown) => {
      const envelope = parseAdminAiRealtimeEnvelope(payload);
      if (!envelope) {
        void callbacks.onRefresh();
        return;
      }
      if (
        envelope.type === 'access.revoked' ||
        envelope.type === 'access_revoked'
      ) {
        callbacks.onAccessRevoked();
        return;
      }
      const decision = acceptEvent(envelope);
      if (decision === 'duplicate') return;
      if (
        envelope.type === 'conversation.changed' ||
        envelope.type === 'snapshot_changed'
      ) {
        void callbacks.onListRefresh();
      }
      void callbacks.onRefresh();
    },
    [acceptEvent, callbacks]
  );
  const { isConnected } = usePlatformEvents({ onAdminAiEvent: receiveEvent });

  useEffect(() => {
    setConnection(isConnected ? 'connected' : 'reconnecting');
  }, [isConnected, setConnection]);

  useEffect(() => {
    const reconcileVisibleTab = () => {
      if (document.visibilityState === 'visible') void callbacks.onRefresh();
    };
    document.addEventListener('visibilitychange', reconcileVisibleTab);
    return () =>
      document.removeEventListener('visibilitychange', reconcileVisibleTab);
  }, [callbacks]);
}
