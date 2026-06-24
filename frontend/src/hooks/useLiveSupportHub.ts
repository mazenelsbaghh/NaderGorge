'use client';

import { useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { getBackendHubUrl } from '@/lib/backend-url';
import { getStoredAccessToken } from '@/lib/auth-storage';
import { useLiveSupportStore } from '@/stores/live-support-store';

export interface LiveSupportEnvelope { eventId: string; conversationId?: string; sequence?: number; type: string; payload: unknown; }

export function useLiveSupportHub(conversationId?: string, onSnapshotRequired?: () => void) {
  const [connected, setConnected] = useState(false);
  const markEventProcessed = useLiveSupportStore((state) => state.markEventProcessed);
  const recordSequence = useLiveSupportStore((state) => state.recordSequence);
  const setOwnershipLost = useLiveSupportStore((state) => state.setOwnershipLost);
  const snapshotCallback = useRef(onSnapshotRequired);
  useEffect(() => { snapshotCallback.current = onSnapshotRequired; }, [onSnapshotRequired]);
  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .configureLogging(signalR.LogLevel.None)
      .withUrl(getBackendHubUrl('/hubs/live-support'), { accessTokenFactory: () => getStoredAccessToken() || '' })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build();
    let heartbeat: ReturnType<typeof setInterval> | undefined;
    const join = async () => { if (conversationId && connection.state === signalR.HubConnectionState.Connected) await connection.invoke('JoinConversation', conversationId); };
    const durableEvent = (raw: string | LiveSupportEnvelope) => {
      const event = typeof raw === 'string' ? JSON.parse(raw) as LiveSupportEnvelope : raw;
      if (!event.eventId || !markEventProcessed(event.eventId)) return;
      if (event.conversationId && event.sequence) {
        const previous = useLiveSupportStore.getState().lastSequenceByConversation[event.conversationId] ?? 0;
        recordSequence(event.conversationId, event.sequence);
        if (previous > 0 && event.sequence > previous + 1) snapshotCallback.current?.();
      }
      if (event.conversationId && ['AssignmentReleased', 'Transferred', 'Closed', 'AIHandoffCompleted'].includes(event.type)) setOwnershipLost(event.conversationId, true);
      if (event.conversationId === conversationId) snapshotCallback.current?.();
    };
    connection.on('LiveSupportEvent', durableEvent);
    connection.onreconnected(() => { setConnected(true); void join(); snapshotCallback.current?.(); });
    connection.onreconnecting(() => setConnected(false));
    connection.onclose(() => setConnected(false));
    void connection.start().then(() => { setConnected(true); void join(); heartbeat = setInterval(() => void connection.invoke('Heartbeat').catch(() => undefined), 30_000); }).catch(() => setConnected(false));
    return () => { if (heartbeat) clearInterval(heartbeat); connection.off('LiveSupportEvent', durableEvent); if (conversationId) void connection.invoke('LeaveConversation', conversationId).catch(() => undefined); void connection.stop(); };
  }, [conversationId, markEventProcessed, recordSequence, setOwnershipLost]);
  return connected;
}
