import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { useAuthStore } from '@/stores/auth-store';
import toast from 'react-hot-toast';
import { getBackendHubUrl } from '@/lib/backend-url';

export interface SignalRMessage {
  id: string;
  roomId: string;
  senderUserId: string;
  senderName: string;
  content: string;
  type: string;
  mediaUrl?: string;
  mediaMetadata?: string;
  isPinned: boolean;
  createdAt: string;
  readBy: string[];
}

export interface TypingIndicator {
  roomId: string;
  userId: string;
  userName: string;
}

export const useSignalR = (
  onMessageReceived?: (message: SignalRMessage) => void,
  onUserTyping?: (roomId: string, userId: string, userName: string) => void
) => {
  const { accessToken, isAuthenticated } = useAuthStore();
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);

  // Keep references to latest callbacks to avoid breaking connection on room switch
  const onMessageReceivedRef = useRef(onMessageReceived);
  const onUserTypingRef = useRef(onUserTyping);

  useEffect(() => {
    onMessageReceivedRef.current = onMessageReceived;
    onUserTypingRef.current = onUserTyping;
  });

  useEffect(() => {
    if (!isAuthenticated || !accessToken) {
      if (connectionRef.current) {
        void connectionRef.current.stop();
        connectionRef.current = null;
        setIsConnected(false);
      }
      return;
    }

    const hubUrl = getBackendHubUrl('/hubs/chat');

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => accessToken,
        skipNegotiation: false,
        transport: signalR.HttpTransportType.WebSockets
      })
      .withAutomaticReconnect()
      .build();

    connection.on('ReceiveMessage', (message: SignalRMessage) => {
      onMessageReceivedRef.current?.(message);
    });

    connection.on('UserTyping', (roomId: string, userId: string, userName: string) => {
      onUserTypingRef.current?.(roomId, userId, userName);
    });

    connection.on('ReceiveNotification', (notification: { Title: string; Body: string; RoomId: string }) => {
      toast(notification.Body, {
        icon: '💬',
        duration: 4000,
        position: 'top-left'
      });
    });

    connection.on('Error', (errorMsg: string) => {
      toast.error(errorMsg);
    });

    connection.start()
      .then(() => {
        setIsConnected(true);
        connectionRef.current = connection;
      })
      .catch((err) => {
        console.error('SignalR Connection Error: ', err);
        setIsConnected(false);
      });

    return () => {
      if (connectionRef.current) {
        void connectionRef.current.stop();
        connectionRef.current = null;
        setIsConnected(false);
      }
    };
  }, [accessToken, isAuthenticated]);

  const sendMessage = useCallback(async (roomId: string, content: string, mediaUrl?: string, mediaMetadata?: string) => {
    if (connectionRef.current && isConnected) {
      try {
        await connectionRef.current.invoke('SendMessage', roomId, content, mediaUrl || null, mediaMetadata || null);
      } catch (err) {
        console.error('Error invoking SendMessage: ', err);
        toast.error('تعذر إرسال الرسالة');
      }
    } else {
      toast.error('أنت غير متصل بالدردشة حالياً');
    }
  }, [isConnected]);

  const sendTyping = useCallback(async (roomId: string) => {
    if (connectionRef.current && isConnected) {
      try {
        await connectionRef.current.invoke('Typing', roomId);
      } catch (err) {
        console.warn('Error invoking Typing: ', err);
      }
    }
  }, [isConnected]);

  return {
    isConnected,
    sendMessage,
    sendTyping
  };
};
