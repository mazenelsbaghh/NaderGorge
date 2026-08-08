'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { getBackendHubUrl } from '@/lib/backend-url';
import { getAccessToken } from '@/lib/auth-memory';
import { useLiveSupportStore } from '@/stores/live-support-store';
import { recordRealtimeMetric } from '@/lib/realtime-observability';
import { decideLiveSupportSequence, parseLiveSupportEnvelope, type LiveSupportClientEnvelope } from '@/lib/live-support-client-contract';

export type LiveSupportEnvelope = LiveSupportClientEnvelope;

export function useLiveSupportHub(conversationId?: string, onSnapshotRequired?: () => void, onParticipantTypingChanged?: (preview: string | null) => void) {
  const [connected, setConnected] = useState(false);
  const markEventProcessed = useLiveSupportStore((state) => state.markEventProcessed);
  const recordSequence = useLiveSupportStore((state) => state.recordSequence);
  const setOwnershipLost = useLiveSupportStore((state) => state.setOwnershipLost);
  const snapshotCallback = useRef(onSnapshotRequired);
  const participantTypingCallback = useRef(onParticipantTypingChanged);
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const typingTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const pendingTypingDraftRef = useRef('');
  const lastTypingSentAtRef = useRef(0);
  useEffect(() => { snapshotCallback.current = onSnapshotRequired; }, [onSnapshotRequired]);
  useEffect(() => { participantTypingCallback.current = onParticipantTypingChanged; }, [onParticipantTypingChanged]);
  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .configureLogging(signalR.LogLevel.None)
      .withUrl(getBackendHubUrl('/hubs/live-support'), {
        accessTokenFactory: () => getAccessToken() || '',
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build();
    connectionRef.current = connection;
    let disposed = false;
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
      if (event.conversationId && ['AssignmentReleased', 'Transferred', 'Closed', 'Abandoned', 'AIHandoffCompleted'].includes(event.type)) setOwnershipLost(event.conversationId, true);
      // Staff receive events for every conversation assigned to them, including
      // conversations that are not currently open in the workspace. Reconcile
      // the bootstrap for all of those events so the queue never stays stale.
      if (event.conversationId) snapshotCallback.current?.();
    };
    connection.on('LiveSupportEvent', durableEvent);
    connection.on('ParticipantTypingChanged', (event: { conversationId: string; preview?: string | null }) => {
      if (event.conversationId === conversationId) participantTypingCallback.current?.(event.preview ?? null);
    });
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
    // React can replace a conversation view while the WebSocket handshake is
    // still in progress.  Calling stop() during that handshake makes SignalR
    // log "Failed to start the HttpConnection before stop() was called".
    // Defer disposal until start settles instead.
    const startPromise = connection.start()
      .then(() => {
        if (disposed) return;
        setConnected(true);
        void join().catch(() => {
          recordRealtimeMetric('snapshotReconciliation');
          snapshotCallback.current?.();
        });
        heartbeat = setInterval(() => void connection.invoke('Heartbeat').catch(() => {
          recordRealtimeMetric('snapshotReconciliation');
        }), 30_000);
      })
      .catch(() => {
        if (!disposed) setConnected(false);
      });
    return () => {
      disposed = true;
      if (heartbeat) clearInterval(heartbeat);
      if (typingTimerRef.current) clearTimeout(typingTimerRef.current);
      typingTimerRef.current = null;
      pendingTypingDraftRef.current = '';
      lastTypingSentAtRef.current = 0;
      connection.off('LiveSupportEvent', durableEvent);
      connection.off('ParticipantTypingChanged');
      connectionRef.current = null;
      void startPromise.finally(async () => {
        if (connection.state === signalR.HubConnectionState.Connected && conversationId) {
          await connection.invoke('LeaveConversation', conversationId).catch(() => undefined);
        }
        if (connection.state !== signalR.HubConnectionState.Disconnected) {
          await connection.stop().catch(() => undefined);
        }
      });
    };
  }, [conversationId, markEventProcessed, recordSequence, setOwnershipLost]);
  const sendTyping = useCallback((draft: string) => {
    if (!conversationId || !draft.trim() || connectionRef.current?.state !== signalR.HubConnectionState.Connected) return;
    pendingTypingDraftRef.current = draft;
    if (typingTimerRef.current) return;
    const elapsed = Date.now() - lastTypingSentAtRef.current;
    typingTimerRef.current = setTimeout(() => {
      typingTimerRef.current = null;
      const connection = connectionRef.current;
      const pendingDraft = pendingTypingDraftRef.current;
      if (!pendingDraft.trim() || connection?.state !== signalR.HubConnectionState.Connected) return;
      lastTypingSentAtRef.current = Date.now();
      void connection.invoke('Typing', conversationId, pendingDraft).catch(() => undefined);
    }, Math.max(0, 800 - elapsed));
  }, [conversationId]);
  return { connected, sendTyping };
}

export function parseLiveSupportEvent(raw: string | LiveSupportEnvelope): LiveSupportEnvelope | undefined {
  return parseLiveSupportEnvelope(raw);
}
