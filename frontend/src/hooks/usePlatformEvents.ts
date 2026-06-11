import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { useAuthStore } from '@/stores/auth-store';
import toast from 'react-hot-toast';
import { invalidateMany } from '@/lib/cache-invalidation';

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

export interface PackagePublishedPayload {
  packageId: string;
}

export interface LessonUpdatedPayload {
  lessonId: string;
  packageId: string;
  title: string;
}

export interface VideoFailedPayload {
  lessonId: string;
  videoId: string;
  error: string;
}

export interface ExamSubmittedPayload {
  examId: string;
  attemptId: string;
}

export interface HomeworkSubmittedPayload {
  homeworkId: string;
}

export interface CommunityPostCreatedPayload {
  postId: string;
  authorId: string;
}

export interface AiJobCompletedPayload {
  jobId: string;
  lessonVideoId: string;
}

export interface AiJobFailedPayload {
  jobId: string;
  error: string;
}

export interface PackageArchivedPayload {
  packageId: string;
}

export interface PackageAccessGrantedPayload {
  userId: string;
  packageId: string;
}

export interface TermPublishedPayload {
  termId: string;
  packageId: string;
  title: string;
}

export interface SectionPublishedPayload {
  sectionId: string;
  termId: string;
  packageId: string;
  title: string;
}

export interface LessonLockedPayload {
  lessonId: string;
  reason: string | null;
}

export interface LessonUnlockedPayload {
  lessonId: string;
}

export interface ResourceProcessingStartedPayload {
  lessonId: string;
  title: string;
}

export interface CodeGroupCreatedPayload {
  codeGroupId: string;
  name: string;
  codeType: string;
  totalCodes: number;
  createdAt: string;
}

export interface CodeGroupExportReadyPayload {
  codeGroupId: string;
  name: string;
  totalCodes: number;
  codes: string[];
}

export interface PurchaseCompletedPayload {
  studentId: string;
  contentType: string;
  contentId: string;
  price: number;
}

export interface PurchaseFailedPayload {
  studentId: string;
  contentType: string;
  contentId: string;
  reason: string;
}

export interface NotificationReadPayload {
  notificationId: string;
  userId: string;
}

export interface NotificationsClearedPayload {
  userId: string;
}

export interface ExamGradedPayload {
  examId: string;
  attemptId: string;
  isPassed: boolean;
  score: number;
  evaluation: string;
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
  onPackagePublished?: (payload: PackagePublishedPayload) => void;
  onLessonUpdated?: (payload: LessonUpdatedPayload) => void;
  onVideoFailed?: (payload: VideoFailedPayload) => void;
  onExamSubmitted?: (payload: ExamSubmittedPayload) => void;
  onHomeworkSubmitted?: (payload: HomeworkSubmittedPayload) => void;
  onCommunityPostCreated?: (payload: CommunityPostCreatedPayload) => void;
  onAiJobCompleted?: (payload: AiJobCompletedPayload) => void;
  onAiJobFailed?: (payload: AiJobFailedPayload) => void;
  onPackageArchived?: (payload: PackageArchivedPayload) => void;
  onPackageAccessGranted?: (payload: PackageAccessGrantedPayload) => void;
  onTermPublished?: (payload: TermPublishedPayload) => void;
  onSectionPublished?: (payload: SectionPublishedPayload) => void;
  onLessonLocked?: (payload: LessonLockedPayload) => void;
  onLessonUnlocked?: (payload: LessonUnlockedPayload) => void;
  onResourceProcessingStarted?: (payload: ResourceProcessingStartedPayload) => void;
  onCodeGroupCreated?: (payload: CodeGroupCreatedPayload) => void;
  onCodeGroupExportReady?: (payload: CodeGroupExportReadyPayload) => void;
  onPurchaseCompleted?: (payload: PurchaseCompletedPayload) => void;
  onPurchaseFailed?: (payload: PurchaseFailedPayload) => void;
  onNotificationRead?: (payload: NotificationReadPayload) => void;
  onNotificationsCleared?: (payload: NotificationsClearedPayload) => void;
  onExamGraded?: (payload: ExamGradedPayload) => void;
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
  PackagePublished: new Set<(payload: PackagePublishedPayload) => void>(),
  LessonUpdated: new Set<(payload: LessonUpdatedPayload) => void>(),
  VideoFailed: new Set<(payload: VideoFailedPayload) => void>(),
  ExamSubmitted: new Set<(payload: ExamSubmittedPayload) => void>(),
  HomeworkSubmitted: new Set<(payload: HomeworkSubmittedPayload) => void>(),
  CommunityPostCreated: new Set<(payload: CommunityPostCreatedPayload) => void>(),
  AiJobCompleted: new Set<(payload: AiJobCompletedPayload) => void>(),
  AiJobFailed: new Set<(payload: AiJobFailedPayload) => void>(),
  PackageArchived: new Set<(payload: PackageArchivedPayload) => void>(),
  PackageAccessGranted: new Set<(payload: PackageAccessGrantedPayload) => void>(),
  TermPublished: new Set<(payload: TermPublishedPayload) => void>(),
  SectionPublished: new Set<(payload: SectionPublishedPayload) => void>(),
  LessonLocked: new Set<(payload: LessonLockedPayload) => void>(),
  LessonUnlocked: new Set<(payload: LessonUnlockedPayload) => void>(),
  ResourceProcessingStarted: new Set<(payload: ResourceProcessingStartedPayload) => void>(),
  CodeGroupCreated: new Set<(payload: CodeGroupCreatedPayload) => void>(),
  CodeGroupExportReady: new Set<(payload: CodeGroupExportReadyPayload) => void>(),
  PurchaseCompleted: new Set<(payload: PurchaseCompletedPayload) => void>(),
  PurchaseFailed: new Set<(payload: PurchaseFailedPayload) => void>(),
  NotificationRead: new Set<(payload: NotificationReadPayload) => void>(),
  NotificationsCleared: new Set<(payload: NotificationsClearedPayload) => void>(),
  ExamGraded: new Set<(payload: ExamGradedPayload) => void>(),
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
    const onPackagePublished = (payload: PackagePublishedPayload) => {
      handlersRef.current?.onPackagePublished?.(payload);
    };
    const onLessonUpdated = (payload: LessonUpdatedPayload) => {
      handlersRef.current?.onLessonUpdated?.(payload);
    };
    const onVideoFailed = (payload: VideoFailedPayload) => {
      handlersRef.current?.onVideoFailed?.(payload);
    };
    const onExamSubmitted = (payload: ExamSubmittedPayload) => {
      handlersRef.current?.onExamSubmitted?.(payload);
    };
    const onHomeworkSubmitted = (payload: HomeworkSubmittedPayload) => {
      handlersRef.current?.onHomeworkSubmitted?.(payload);
    };
    const onCommunityPostCreated = (payload: CommunityPostCreatedPayload) => {
      handlersRef.current?.onCommunityPostCreated?.(payload);
    };
    const onAiJobCompleted = (payload: AiJobCompletedPayload) => {
      handlersRef.current?.onAiJobCompleted?.(payload);
    };
    const onAiJobFailed = (payload: AiJobFailedPayload) => {
      handlersRef.current?.onAiJobFailed?.(payload);
    };
    const onPackageArchived = (payload: PackageArchivedPayload) => {
      handlersRef.current?.onPackageArchived?.(payload);
    };
    const onPackageAccessGranted = (payload: PackageAccessGrantedPayload) => {
      handlersRef.current?.onPackageAccessGranted?.(payload);
    };
    const onTermPublished = (payload: TermPublishedPayload) => {
      handlersRef.current?.onTermPublished?.(payload);
    };
    const onSectionPublished = (payload: SectionPublishedPayload) => {
      handlersRef.current?.onSectionPublished?.(payload);
    };
    const onLessonLocked = (payload: LessonLockedPayload) => {
      handlersRef.current?.onLessonLocked?.(payload);
    };
    const onLessonUnlocked = (payload: LessonUnlockedPayload) => {
      handlersRef.current?.onLessonUnlocked?.(payload);
    };
    const onResourceProcessingStarted = (payload: ResourceProcessingStartedPayload) => {
      handlersRef.current?.onResourceProcessingStarted?.(payload);
    };
    const onCodeGroupCreated = (payload: CodeGroupCreatedPayload) => {
      handlersRef.current?.onCodeGroupCreated?.(payload);
    };
    const onCodeGroupExportReady = (payload: CodeGroupExportReadyPayload) => {
      handlersRef.current?.onCodeGroupExportReady?.(payload);
    };
    const onPurchaseCompleted = (payload: PurchaseCompletedPayload) => {
      handlersRef.current?.onPurchaseCompleted?.(payload);
    };
    const onPurchaseFailed = (payload: PurchaseFailedPayload) => {
      handlersRef.current?.onPurchaseFailed?.(payload);
    };
    const onNotificationRead = (payload: NotificationReadPayload) => {
      handlersRef.current?.onNotificationRead?.(payload);
    };
    const onNotificationsCleared = (payload: NotificationsClearedPayload) => {
      handlersRef.current?.onNotificationsCleared?.(payload);
    };
    const onExamGraded = (payload: ExamGradedPayload) => {
      handlersRef.current?.onExamGraded?.(payload);
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
    listeners.PackagePublished.add(onPackagePublished);
    listeners.LessonUpdated.add(onLessonUpdated);
    listeners.VideoFailed.add(onVideoFailed);
    listeners.ExamSubmitted.add(onExamSubmitted);
    listeners.HomeworkSubmitted.add(onHomeworkSubmitted);
    listeners.CommunityPostCreated.add(onCommunityPostCreated);
    listeners.AiJobCompleted.add(onAiJobCompleted);
    listeners.AiJobFailed.add(onAiJobFailed);
    listeners.PackageArchived.add(onPackageArchived);
    listeners.PackageAccessGranted.add(onPackageAccessGranted);
    listeners.TermPublished.add(onTermPublished);
    listeners.SectionPublished.add(onSectionPublished);
    listeners.LessonLocked.add(onLessonLocked);
    listeners.LessonUnlocked.add(onLessonUnlocked);
    listeners.ResourceProcessingStarted.add(onResourceProcessingStarted);
    listeners.CodeGroupCreated.add(onCodeGroupCreated);
    listeners.CodeGroupExportReady.add(onCodeGroupExportReady);
    listeners.PurchaseCompleted.add(onPurchaseCompleted);
    listeners.PurchaseFailed.add(onPurchaseFailed);
    listeners.NotificationRead.add(onNotificationRead);
    listeners.NotificationsCleared.add(onNotificationsCleared);
    listeners.ExamGraded.add(onExamGraded);

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
            invalidateMany(['student:shell', 'notifications']);
            toast.success(payload.message, { icon: '🔔' });
          } catch (e) {
            console.error('Error handling NotificationCreated event:', e);
          }
        });

        sharedConnection.on('BalanceChanged', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as BalancePayload;
            listeners.BalanceChanged.forEach(handler => handler(payload));
            invalidateMany(['student:shell', 'student:balance']);
          } catch (e) {
            console.error('Error handling BalanceChanged event:', e);
          }
        });

        sharedConnection.on('CodeActivated', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as CodeActivatedPayload;
            listeners.CodeActivated.forEach(handler => handler(payload));
            invalidateMany(['student:shell', 'content:packages', 'student:balance']);
          } catch (e) {
            console.error('Error handling CodeActivated event:', e);
          }
        });

        sharedConnection.on('LessonPublished', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as LessonPublishedPayload;
            listeners.LessonPublished.forEach(handler => handler(payload));
            invalidateMany(['content:packages']);
          } catch (e) {
            console.error('Error handling LessonPublished event:', e);
          }
        });

        sharedConnection.on('VideoReady', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as VideoReadyPayload;
            listeners.VideoReady.forEach(handler => handler(payload));
            invalidateMany([`content:lesson:${payload.lessonId}`]);
          } catch (e) {
            console.error('Error handling VideoReady event:', e);
          }
        });

        sharedConnection.on('VideoProcessingStarted', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as VideoProcessingStartedPayload;
            listeners.VideoProcessingStarted.forEach(handler => handler(payload));
            invalidateMany([`content:lesson:${payload.lessonId}`]);
          } catch (e) {
            console.error('Error handling VideoProcessingStarted event:', e);
          }
        });

        sharedConnection.on('ResourceReady', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as ResourceReadyPayload;
            listeners.ResourceReady.forEach(handler => handler(payload));
            invalidateMany([`content:lesson:${payload.lessonId}`]);
          } catch (e) {
            console.error('Error handling ResourceReady event:', e);
          }
        });

        sharedConnection.on('ExtraWatchRequestUpdated', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as ExtraWatchRequestPayload;
            listeners.ExtraWatchRequestUpdated.forEach(handler => handler(payload));
            invalidateMany([`content:lesson:${payload.videoId}`]);
          } catch (e) {
            console.error('Error handling ExtraWatchRequestUpdated event:', e);
          }
        });

        sharedConnection.on('AiJobProgress', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as AiJobProgressPayload;
            listeners.AiJobProgress.forEach(handler => handler(payload));
            invalidateMany(['admin:ai-monitor']);
          } catch (e) {
            console.error('Error handling AiJobProgress event:', e);
          }
        });

        sharedConnection.on('PackagePublished', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as PackagePublishedPayload;
            listeners.PackagePublished.forEach(handler => handler(payload));
            invalidateMany(['content:packages']);
          } catch (e) {
            console.error('Error handling PackagePublished event:', e);
          }
        });

        sharedConnection.on('LessonUpdated', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as LessonUpdatedPayload;
            listeners.LessonUpdated.forEach(handler => handler(payload));
            invalidateMany(['content:packages', `content:lesson:${payload.lessonId}`]);
          } catch (e) {
            console.error('Error handling LessonUpdated event:', e);
          }
        });

        sharedConnection.on('VideoFailed', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as VideoFailedPayload;
            listeners.VideoFailed.forEach(handler => handler(payload));
            invalidateMany([`content:lesson:${payload.lessonId}`]);
          } catch (e) {
            console.error('Error handling VideoFailed event:', e);
          }
        });

        sharedConnection.on('ExamSubmitted', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as ExamSubmittedPayload;
            listeners.ExamSubmitted.forEach(handler => handler(payload));
            invalidateMany(['student:exams']);
          } catch (e) {
            console.error('Error handling ExamSubmitted event:', e);
          }
        });

        sharedConnection.on('HomeworkSubmitted', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as HomeworkSubmittedPayload;
            listeners.HomeworkSubmitted.forEach(handler => handler(payload));
            invalidateMany(['student:homeworks']);
          } catch (e) {
            console.error('Error handling HomeworkSubmitted event:', e);
          }
        });

        sharedConnection.on('CommunityPostCreated', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as CommunityPostCreatedPayload;
            listeners.CommunityPostCreated.forEach(handler => handler(payload));
            invalidateMany(['community:posts']);
          } catch (e) {
            console.error('Error handling CommunityPostCreated event:', e);
          }
        });

        sharedConnection.on('AiJobCompleted', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as AiJobCompletedPayload;
            listeners.AiJobCompleted.forEach(handler => handler(payload));
            invalidateMany(['admin:ai-monitor']);
          } catch (e) {
            console.error('Error handling AiJobCompleted event:', e);
          }
        });

        sharedConnection.on('AiJobFailed', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as AiJobFailedPayload;
            listeners.AiJobFailed.forEach(handler => handler(payload));
            invalidateMany(['admin:ai-monitor']);
          } catch (e) {
            console.error('Error handling AiJobFailed event:', e);
          }
        });

        sharedConnection.on('PackageArchived', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as PackageArchivedPayload;
            listeners.PackageArchived.forEach(handler => handler(payload));
            invalidateMany(['content:packages']);
          } catch (e) {
            console.error('Error handling PackageArchived event:', e);
          }
        });

        sharedConnection.on('PackageAccessGranted', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as PackageAccessGrantedPayload;
            listeners.PackageAccessGranted.forEach(handler => handler(payload));
            invalidateMany(['content:packages', 'student:shell', 'student:balance']);
          } catch (e) {
            console.error('Error handling PackageAccessGranted event:', e);
          }
        });

        sharedConnection.on('TermPublished', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as TermPublishedPayload;
            listeners.TermPublished.forEach(handler => handler(payload));
            invalidateMany(['content:packages']);
          } catch (e) {
            console.error('Error handling TermPublished event:', e);
          }
        });

        sharedConnection.on('SectionPublished', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as SectionPublishedPayload;
            listeners.SectionPublished.forEach(handler => handler(payload));
            invalidateMany(['content:packages']);
          } catch (e) {
            console.error('Error handling SectionPublished event:', e);
          }
        });

        sharedConnection.on('LessonLocked', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as LessonLockedPayload;
            listeners.LessonLocked.forEach(handler => handler(payload));
            invalidateMany(['content:packages', `content:lesson:${payload.lessonId}`]);
          } catch (e) {
            console.error('Error handling LessonLocked event:', e);
          }
        });

        sharedConnection.on('LessonUnlocked', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as LessonUnlockedPayload;
            listeners.LessonUnlocked.forEach(handler => handler(payload));
            invalidateMany(['content:packages', `content:lesson:${payload.lessonId}`]);
          } catch (e) {
            console.error('Error handling LessonUnlocked event:', e);
          }
        });

        sharedConnection.on('ResourceProcessingStarted', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as ResourceProcessingStartedPayload;
            listeners.ResourceProcessingStarted.forEach(handler => handler(payload));
            invalidateMany([`content:lesson:${payload.lessonId}`]);
          } catch (e) {
            console.error('Error handling ResourceProcessingStarted event:', e);
          }
        });

        sharedConnection.on('CodeGroupCreated', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as CodeGroupCreatedPayload;
            listeners.CodeGroupCreated.forEach(handler => handler(payload));
            invalidateMany(['admin:ai-monitor']);
          } catch (e) {
            console.error('Error handling CodeGroupCreated event:', e);
          }
        });

        sharedConnection.on('CodeGroupExportReady', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as CodeGroupExportReadyPayload;
            listeners.CodeGroupExportReady.forEach(handler => handler(payload));
            invalidateMany(['admin:ai-monitor']);
          } catch (e) {
            console.error('Error handling CodeGroupExportReady event:', e);
          }
        });

        sharedConnection.on('PurchaseCompleted', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as PurchaseCompletedPayload;
            listeners.PurchaseCompleted.forEach(handler => handler(payload));
            invalidateMany(['student:shell', 'student:balance', 'content:packages']);
          } catch (e) {
            console.error('Error handling PurchaseCompleted event:', e);
          }
        });

        sharedConnection.on('PurchaseFailed', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as PurchaseFailedPayload;
            listeners.PurchaseFailed.forEach(handler => handler(payload));
            toast.error('فشلت عملية الشراء: ' + (payload.reason === 'insufficient_balance' ? 'الرصيد غير كافٍ' : 'تم الشراء مسبقاً'));
          } catch (e) {
            console.error('Error handling PurchaseFailed event:', e);
          }
        });

        sharedConnection.on('NotificationRead', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as NotificationReadPayload;
            listeners.NotificationRead.forEach(handler => handler(payload));
            invalidateMany(['notifications', 'student:shell']);
          } catch (e) {
            console.error('Error handling NotificationRead event:', e);
          }
        });

        sharedConnection.on('NotificationsCleared', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as NotificationsClearedPayload;
            listeners.NotificationsCleared.forEach(handler => handler(payload));
            invalidateMany(['notifications', 'student:shell']);
          } catch (e) {
            console.error('Error handling NotificationsCleared event:', e);
          }
        });

        sharedConnection.on('ExamGraded', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as ExamGradedPayload;
            listeners.ExamGraded.forEach(handler => handler(payload));
            invalidateMany(['student:exams']);
          } catch (e) {
            console.error('Error handling ExamGraded event:', e);
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
      listeners.PackagePublished.delete(onPackagePublished);
      listeners.LessonUpdated.delete(onLessonUpdated);
      listeners.VideoFailed.delete(onVideoFailed);
      listeners.ExamSubmitted.delete(onExamSubmitted);
      listeners.HomeworkSubmitted.delete(onHomeworkSubmitted);
      listeners.CommunityPostCreated.delete(onCommunityPostCreated);
      listeners.AiJobCompleted.delete(onAiJobCompleted);
      listeners.AiJobFailed.delete(onAiJobFailed);
      listeners.PackageArchived.delete(onPackageArchived);
      listeners.PackageAccessGranted.delete(onPackageAccessGranted);
      listeners.TermPublished.delete(onTermPublished);
      listeners.SectionPublished.delete(onSectionPublished);
      listeners.LessonLocked.delete(onLessonLocked);
      listeners.LessonUnlocked.delete(onLessonUnlocked);
      listeners.ResourceProcessingStarted.delete(onResourceProcessingStarted);
      listeners.CodeGroupCreated.delete(onCodeGroupCreated);
      listeners.CodeGroupExportReady.delete(onCodeGroupExportReady);
      listeners.PurchaseCompleted.delete(onPurchaseCompleted);
      listeners.PurchaseFailed.delete(onPurchaseFailed);
      listeners.NotificationRead.delete(onNotificationRead);
      listeners.NotificationsCleared.delete(onNotificationsCleared);
      listeners.ExamGraded.delete(onExamGraded);

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
