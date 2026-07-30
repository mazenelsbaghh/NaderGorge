'use client';

import { useEffect, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import toast from 'react-hot-toast';
import { getBackendHubUrl } from '@/lib/backend-url';
import { getAccessToken } from '@/lib/auth-memory';
import { parseLiveSupportEnvelope } from '@/lib/live-support-client-contract';
import { playLiveSupportSound, useLiveSupportPreferences } from '@/hooks/useLiveSupportPreferences';
import { useAuthStore } from '@/stores/auth-store';

type MessagePayload = { senderType?: string };

export function useStaffLiveSupportNotifications() {
  const userId = useAuthStore((state) => state.user?.id);
  const roles = useAuthStore((state) => state.user?.roles ?? []);
  const { preferences } = useLiveSupportPreferences();
  const preferencesRef = useRef(preferences);
  const processedEventIds = useRef(new Set<string>());

  useEffect(() => { preferencesRef.current = preferences; }, [preferences]);

  useEffect(() => {
    const isStaff = roles.some((role) => ['admin', 'assistant', 'staff'].includes(role.toLowerCase()));
    if (!userId || !isStaff) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(getBackendHubUrl('/hubs/live-support'), {
        accessTokenFactory: () => getAccessToken() || '',
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build();

    const onEvent = (raw: string) => {
      if (window.location.pathname.startsWith('/assistant/live-support')) return;
      const event = parseLiveSupportEnvelope(raw);
      if (!event || event.type !== 'MessageSent' || processedEventIds.current.has(event.eventId)) return;
      processedEventIds.current.add(event.eventId);
      const senderType = (event.payload as MessagePayload | null)?.senderType;
      if (senderType !== 'Student' && senderType !== 'Guest') return;

      const current = preferencesRef.current;
      if (current.soundEnabled) playLiveSupportSound(current.sound);
      if (current.notificationsEnabled) {
        toast('رسالة جديدة في الدعم المباشر', { icon: '💬', id: `live-support:${event.eventId}` });
        if (typeof Notification !== 'undefined' && Notification.permission === 'granted') {
          new Notification('رسالة جديدة في الدعم المباشر', { body: 'لديك رسالة جديدة من طالب.' });
        }
      }
    };

    connection.on('LiveSupportEvent', onEvent);
    void connection.start().catch(() => undefined);
    return () => {
      connection.off('LiveSupportEvent', onEvent);
      void connection.stop();
    };
  }, [roles, userId]);
}
