import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { useAuthStore } from '@/stores/auth-store';
import toast from 'react-hot-toast';

export interface NotificationPayload {
  id: string;
  title: string;
  message: string;
  createdAt: string;
}

export interface BalancePayload {
  newBalance: number;
  formattedBalance: string;
}

export interface CodeActivatedPayload {
  codeType: string;
  referenceId: string;
  message: string;
}

export interface LessonPublishedPayload {
  lessonId: string;
  packageId: string;
  title: string;
  order: number;
}

export interface VideoReadyPayload {
  lessonId: string;
  videoId: string;
  title: string;
  provider: string;
  providerVideoId: string;
}

export interface VideoProcessingStartedPayload {
  lessonId: string;
  videoId: string;
  status: string;
}

export interface ResourceReadyPayload {
  lessonId: string;
  resourceId: string;
  title: string;
  fileUrl: string;
}

export interface ExtraWatchRequestPayload {
  videoId: string;
  status: string;
  allowedWatchCount: number;
}

export interface AiJobProgressPayload {
  jobId: string;
  progress: number;
  status: string;
  message: string;
}

export interface PlatformEventHandlers {
  onNotificationCreated?: (payload: NotificationPayload) => void;
  onBalanceChanged?: (payload: BalancePayload) => void;
  onCodeActivated?: (payload: CodeActivatedPayload) => void;
  onLessonPublished?: (payload: LessonPublishedPayload) => void;
  onVideoReady?: (payload: VideoReadyPayload) => void;
  onVideoProcessingStarted?: (payload: VideoProcessingStartedPayload) => void;
  onResourceReady?: (payload: ResourceReadyPayload) => void;
  onExtraWatchRequestUpdated?: (payload: ExtraWatchRequestPayload) => void;
  onAiJobProgress?: (payload: AiJobProgressPayload) => void;
}

// Module-level connection to share across components
let sharedConnection: signalR.HubConnection | null = null;
let connectionPromise: Promise<signalR.HubConnection> | null = null;
let activeHooksCount = 0;
let latestAccessToken: string | null = null;
const connectionStatusListeners = new Set<(isConnected: boolean) => void>();

// Centralized listener registry
const listeners = {
  NotificationCreated: new Set<(payload: NotificationPayload) => void>(),
  BalanceChanged: new Set<(payload: BalancePayload) => void>(),
  CodeActivated: new Set<(payload: CodeActivatedPayload) => void>(),
  LessonPublished: new Set<(payload: LessonPublishedPayload) => void>(),
  VideoReady: new Set<(payload: VideoReadyPayload) => void>(),
  VideoProcessingStarted: new Set<(payload: VideoProcessingStartedPayload) => void>(),
  ResourceReady: new Set<(payload: ResourceReadyPayload) => void>(),
  ExtraWatchRequestUpdated: new Set<(payload: ExtraWatchRequestPayload) => void>(),
  AiJobProgress: new Set<(payload: AiJobProgressPayload) => void>(),
};

export const usePlatformEvents = (handlers?: PlatformEventHandlers) => {
  const { accessToken, isAuthenticated } = useAuthStore();
  const [isConnected, setIsConnected] = useState(
    sharedConnection ? sharedConnection.state === signalR.HubConnectionState.Connected : false
  );

  // Synchronize local connection status with module-level updates
  useEffect(() => {
    const handleStatusChange = (status: boolean) => {
      setIsConnected(status);
    };
    connectionStatusListeners.add(handleStatusChange);
    setIsConnected(sharedConnection ? sharedConnection.state === signalR.HubConnectionState.Connected : false);
    return () => {
      connectionStatusListeners.delete(handleStatusChange);
    };
  }, []);

  // We wrap handlers in a ref to prevent recreating hook effects when they change
  const handlersRef = useRef(handlers);
  useEffect(() => {
    handlersRef.current = handlers;
  }, [handlers]);

  const joinPackage = useCallback(async (packageId: string) => {
    if (sharedConnection && sharedConnection.state === signalR.HubConnectionState.Connected) {
      try {
        await sharedConnection.invoke('JoinPackage', packageId);
      } catch (err) {
        console.error('Failed to join package group:', err);
      }
    }
  }, []);

  const leavePackage = useCallback(async (packageId: string) => {
    if (sharedConnection && sharedConnection.state === signalR.HubConnectionState.Connected) {
      try {
        await sharedConnection.invoke('LeavePackage', packageId);
      } catch (err) {
        console.error('Failed to leave package group:', err);
      }
    }
  }, []);

  const joinLesson = useCallback(async (lessonId: string) => {
    if (sharedConnection && sharedConnection.state === signalR.HubConnectionState.Connected) {
      try {
        await sharedConnection.invoke('JoinLesson', lessonId);
      } catch (err) {
        console.error('Failed to join lesson group:', err);
      }
    }
  }, []);

  const leaveLesson = useCallback(async (lessonId: string) => {
    if (sharedConnection && sharedConnection.state === signalR.HubConnectionState.Connected) {
      try {
        await sharedConnection.invoke('LeaveLesson', lessonId);
      } catch (err) {
        console.error('Failed to leave lesson group:', err);
      }
    }
  }, []);

  useEffect(() => {
    if (!isAuthenticated || !accessToken) {
      return;
    }

    latestAccessToken = accessToken;
    activeHooksCount++;

    // Define local wrapper functions that read latest handlersRef.current
    const onNotificationCreated = (payload: NotificationPayload) => {
      handlersRef.current?.onNotificationCreated?.(payload);
    };
    const onBalanceChanged = (payload: BalancePayload) => {
      handlersRef.current?.onBalanceChanged?.(payload);
    };
    const onCodeActivated = (payload: CodeActivatedPayload) => {
      handlersRef.current?.onCodeActivated?.(payload);
    };
    const onLessonPublished = (payload: LessonPublishedPayload) => {
      handlersRef.current?.onLessonPublished?.(payload);
    };
    const onVideoReady = (payload: VideoReadyPayload) => {
      handlersRef.current?.onVideoReady?.(payload);
    };
    const onVideoProcessingStarted = (payload: VideoProcessingStartedPayload) => {
      handlersRef.current?.onVideoProcessingStarted?.(payload);
    };
    const onResourceReady = (payload: ResourceReadyPayload) => {
      handlersRef.current?.onResourceReady?.(payload);
    };
    const onExtraWatchRequestUpdated = (payload: ExtraWatchRequestPayload) => {
      handlersRef.current?.onExtraWatchRequestUpdated?.(payload);
    };
    const onAiJobProgress = (payload: AiJobProgressPayload) => {
      handlersRef.current?.onAiJobProgress?.(payload);
    };

    // Add wrappers to registry sets
    listeners.NotificationCreated.add(onNotificationCreated);
    listeners.BalanceChanged.add(onBalanceChanged);
    listeners.CodeActivated.add(onCodeActivated);
    listeners.LessonPublished.add(onLessonPublished);
    listeners.VideoReady.add(onVideoReady);
    listeners.VideoProcessingStarted.add(onVideoProcessingStarted);
    listeners.ResourceReady.add(onResourceReady);
    listeners.ExtraWatchRequestUpdated.add(onExtraWatchRequestUpdated);
    listeners.AiJobProgress.add(onAiJobProgress);

    const initConnection = async () => {
      if (!sharedConnection) {
        const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5245/api';
        const hubUrl = apiBaseUrl.replace('/api', '') + '/hubs/platform';

        sharedConnection = new signalR.HubConnectionBuilder()
          .withUrl(hubUrl, {
            accessTokenFactory: () => latestAccessToken || '',
            skipNegotiation: false,
            transport: signalR.HttpTransportType.WebSockets
          })
          .withAutomaticReconnect()
          .build();

        // Register status change callbacks
        sharedConnection.onreconnecting((error) => {
          console.warn('Platform SignalR reconnecting:', error);
          connectionStatusListeners.forEach(listener => listener(false));
        });

        sharedConnection.onreconnected((connectionId) => {
          console.log('Platform SignalR reconnected:', connectionId);
          connectionStatusListeners.forEach(listener => listener(true));
        });

        sharedConnection.onclose((error) => {
          console.error('Platform SignalR closed:', error);
          connectionStatusListeners.forEach(listener => listener(false));
        });

        // Register universal connection event listeners exactly once
        sharedConnection.on('NotificationCreated', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as NotificationPayload;
            listeners.NotificationCreated.forEach(handler => handler(payload));
            toast.success(payload.message, { icon: '🔔' });
          } catch (e) {
            console.error('Error handling NotificationCreated event:', e);
          }
        });

        sharedConnection.on('BalanceChanged', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as BalancePayload;
            listeners.BalanceChanged.forEach(handler => handler(payload));
          } catch (e) {
            console.error('Error handling BalanceChanged event:', e);
          }
        });

        sharedConnection.on('CodeActivated', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as CodeActivatedPayload;
            listeners.CodeActivated.forEach(handler => handler(payload));
          } catch (e) {
            console.error('Error handling CodeActivated event:', e);
          }
        });

        sharedConnection.on('LessonPublished', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as LessonPublishedPayload;
            listeners.LessonPublished.forEach(handler => handler(payload));
          } catch (e) {
            console.error('Error handling LessonPublished event:', e);
          }
        });

        sharedConnection.on('VideoReady', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as VideoReadyPayload;
            listeners.VideoReady.forEach(handler => handler(payload));
          } catch (e) {
            console.error('Error handling VideoReady event:', e);
          }
        });

        sharedConnection.on('VideoProcessingStarted', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as VideoProcessingStartedPayload;
            listeners.VideoProcessingStarted.forEach(handler => handler(payload));
          } catch (e) {
            console.error('Error handling VideoProcessingStarted event:', e);
          }
        });

        sharedConnection.on('ResourceReady', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as ResourceReadyPayload;
            listeners.ResourceReady.forEach(handler => handler(payload));
          } catch (e) {
            console.error('Error handling ResourceReady event:', e);
          }
        });

        sharedConnection.on('ExtraWatchRequestUpdated', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as ExtraWatchRequestPayload;
            listeners.ExtraWatchRequestUpdated.forEach(handler => handler(payload));
          } catch (e) {
            console.error('Error handling ExtraWatchRequestUpdated event:', e);
          }
        });

        sharedConnection.on('AiJobProgress', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as AiJobProgressPayload;
            listeners.AiJobProgress.forEach(handler => handler(payload));
          } catch (e) {
            console.error('Error handling AiJobProgress event:', e);
          }
        });

        connectionPromise = sharedConnection.start()
          .then(() => {
            setIsConnected(true);
            connectionStatusListeners.forEach(listener => listener(true));
            return sharedConnection!;
          })
          .catch((err) => {
            console.error('Platform SignalR Connection Error:', err);
            setIsConnected(false);
            connectionStatusListeners.forEach(listener => listener(false));
            sharedConnection = null;
            connectionPromise = null;
            throw err;
          });
      } else {
        if (sharedConnection.state === signalR.HubConnectionState.Connected) {
          setIsConnected(true);
          connectionStatusListeners.forEach(listener => listener(true));
        } else {
          try {
            await connectionPromise;
            const status = (sharedConnection as any)?.state === signalR.HubConnectionState.Connected;
            setIsConnected(status);
            connectionStatusListeners.forEach(listener => listener(status));
          } catch {
            setIsConnected(false);
            connectionStatusListeners.forEach(listener => listener(false));
          }
        }
      }
    };

    void initConnection();

    return () => {
      activeHooksCount--;

      // Remove wrappers from registry sets
      listeners.NotificationCreated.delete(onNotificationCreated);
      listeners.BalanceChanged.delete(onBalanceChanged);
      listeners.CodeActivated.delete(onCodeActivated);
      listeners.LessonPublished.delete(onLessonPublished);
      listeners.VideoReady.delete(onVideoReady);
      listeners.VideoProcessingStarted.delete(onVideoProcessingStarted);
      listeners.ResourceReady.delete(onResourceReady);
      listeners.ExtraWatchRequestUpdated.delete(onExtraWatchRequestUpdated);
      listeners.AiJobProgress.delete(onAiJobProgress);

      if (activeHooksCount <= 0 && sharedConnection) {
        const conn = sharedConnection;
        sharedConnection = null;
        connectionPromise = null;
        latestAccessToken = null;
        void conn.stop().then(() => {
          connectionStatusListeners.forEach(listener => listener(false));
        });
      }
    };
  }, [accessToken, isAuthenticated]);

  return {
    isConnected,
    joinPackage,
    leavePackage,
    joinLesson,
    leaveLesson
  };
};
