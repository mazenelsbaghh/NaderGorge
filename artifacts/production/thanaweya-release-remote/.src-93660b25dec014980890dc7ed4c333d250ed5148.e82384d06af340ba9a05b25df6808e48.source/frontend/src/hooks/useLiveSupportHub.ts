'use client';

import { useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { getBackendHubUrl } from '@/lib/backend-url';
import { getAccessToken } from '@/lib/auth-memory';
import { useLiveSupportStore } from '@/stores/live-support-store';
import { recordRealtimeMetric } from '@/lib/realtime-observability';
import { decideLiveSupportSequence, parseLiveSupportEnvelope, type LiveSupportClientEnvelope } from '@/lib/live-support-client-contract';

export type LiveSupportEnvelope = LiveSupportClientEnvelope;

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
      .withUrl(getBackendHubUrl('/hubs/live-support'), { accessTokenFactory: () => getAccessToken() || '' })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build();
    let heartbeat: ReturnType<typeof setInterval> | undefined;
    const join = async () => { if (conversationId && connection.state === signalR.HubConnectionState.Connected) await connection.invoke('JoinConversation', conversationId); };
    const durableEvent = (raw: string | LiveSupportEnvelope) => {
      const event = parseLiveSupportEvent(raw);
      if (!event) {
        recordRealtimeMetric('invalidEvent');
        recordRealtimeMetric('snapshotReconciliation');
        snapshotCallback.current?.();
        return;
      }
      if (!markEventProcessed(event.eventId)) return;
      if (event.conversationId && typeof event.sequence === 'number') {
        const previous = useLiveSupportStore.getState().lastSequenceByConversation[event.conversationId] ?? 0;
        const sequenceDecision = decideLiveSupportSequence(previous, event.sequence);
        if (sequenceDecision === 'duplicate') return;
        if (sequenceDecision === 'reconcile') {
          recordRealtimeMetric('snapshotReconciliation');
          snapshotCallback.current?.();
          return;
        }
        recordSequence(event.conversationId, event.sequence);
      }
      if (event.conversationId && ['AssignmentReleased', 'Transferred', 'Closed', 'AIHandoffCompleted'].includes(event.type)) setOwnershipLost(event.conversationId, true);
      if (event.conversationId === conversationId) snapshotCallback.current?.();
    };
    connection.on('LiveSupportEvent', durableEvent);
    connection.onreconnected(() => {
      setConnected(true);
      recordRealtimeMetric('reconnect');
      recordRealtimeMetric('snapshotReconciliation');
      void join().catch(() => {
        recordRealtimeMetric('snapshotReconciliation');
        snapshotCallback.current?.();
      });
      snapshotCallback.current?.();
    });
    connection.onreconnecting(() => setConnected(false));
    connection.onclose(() => setConnected(false));
    void connection.start().then(() => {
      setConnected(true);
      void join().catch(() => {
        recordRealtimeMetric('snapshotReconciliation');
        snapshotCallback.current?.();
      });
      heartbeat = setInterval(() => void connection.invoke('Heartbeat').catch(() => {
        recordRealtimeMetric('snapshotReconciliation');
      }), 30_000);
    }).catch(() => setConnected(false));
    return () => { if (heartbeat) clearInterval(heartbeat); connection.off('LiveSupportEvent', durableEvent); if (conversationId) void connection.invoke('LeaveConversation', conversationId).catch(() => undefined); void connection.stop(); };
  }, [conversationId, markEventProcessed, recordSequence, setOwnershipLost]);
  return connected;
}

export function parseLiveSupportEvent(raw: string | LiveSupportEnvelope): LiveSupportEnvelope | undefined {
  return parseLiveSupportEnvelope(raw);
}
