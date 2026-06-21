'use client';

import { useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { getBackendHubUrl } from '@/lib/backend-url';
import { getStoredAccessToken } from '@/lib/auth-storage';
import { useLiveSupportStore } from '@/stores/live-support-store';

export interface LiveSupportEnvelope { eventId: string; conversationId?: string; sequence?: number; type: string; payload: unknown; }

export function useLiveSupportHub(conversationId?: string) {
  const [connected, setConnected] = useState(false);
  const markEventProcessed = useLiveSupportStore((state) => state.markEventProcessed);
  const recordSequence = useLiveSupportStore((state) => state.recordSequence);
  const setOwnershipLost = useLiveSupportStore((state) => state.setOwnershipLost);
  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder().withUrl(getBackendHubUrl('/hubs/live-support'), { accessTokenFactory: () => getStoredAccessToken() || '' }).withAutomaticReconnect([0, 2000, 5000, 10000]).build();
    let heartbeat: ReturnType<typeof setInterval> | undefined;
    const join = async () => { if (conversationId && connection.state === signalR.HubConnectionState.Connected) await connection.invoke('JoinConversation', conversationId); };
    const durableEvent = (raw: string | LiveSupportEnvelope) => {
      const event = typeof raw === 'string' ? JSON.parse(raw) as LiveSupportEnvelope : raw;
      if (!event.eventId || !markEventProcessed(event.eventId)) return;
      if (event.conversationId && event.sequence) recordSequence(event.conversationId, event.sequence);
      if (event.conversationId && event.type === 'AssignmentReleased') setOwnershipLost(event.conversationId, true);
    };
    connection.on('LiveSupportEvent', durableEvent);
    connection.onreconnected(() => { setConnected(true); void join(); });
    connection.onreconnecting(() => setConnected(false));
    connection.onclose(() => setConnected(false));
    void connection.start().then(() => { setConnected(true); void join(); heartbeat = setInterval(() => void connection.invoke('Heartbeat').catch(() => undefined), 30_000); }).catch(() => setConnected(false));
    return () => { if (heartbeat) clearInterval(heartbeat); connection.off('LiveSupportEvent', durableEvent); if (conversationId) void connection.invoke('LeaveConversation', conversationId).catch(() => undefined); void connection.stop(); };
  }, [conversationId, markEventProcessed, recordSequence, setOwnershipLost]);
  return connected;
}
