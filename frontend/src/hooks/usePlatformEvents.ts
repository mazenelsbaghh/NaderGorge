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
  onAiJobQueued?: (payload: { lessonVideoId: string; jobType: string; }) => void;
  onAiJobCancelled?: (payload: { lessonVideoId: string; isMindmapOnly: boolean; }) => void;
  onCommunityCommentCreated?: (payload: { commentId: string; postId: string; authorUserId: string; body: string; status: string; }) => void;
  onCommunityCommentApproved?: (payload: { commentId: string; postId: string; authorUserId: string; body: string; status: string; }) => void;
  onCommunityPostApproved?: (payload: { postId: string; authorUserId: string; status: string; }) => void;
  onCommunityPostLiked?: (payload: { postId: string; newLikesCount: number; }) => void;
  onCommunityPostRejected?: (payload: { postId: string; authorUserId: string; status: string; }) => void;
  onCommunityCommentRejected?: (payload: { commentId: string; postId: string; authorUserId: string; status: string; reason: string; }) => void;
  onExamPublished?: (payload: { examId: string; title: string; lessonId: string; }) => void;
  onExamResultReady?: (payload: { examId: string; attemptId: string; score: number; isPassed: boolean; }) => void;
  onExtraWatchRequestCreated?: (payload: { videoId: string; studentId: string; studentName: string; }) => void;
  onHomeworkPublished?: (payload: { homeworkId: string; lessonId: string; title: string; }) => void;
  onHomeworkGraded?: (payload: { homeworkId: string; submissionId: string; score: number; studentId: string; }) => void;
  onPackageCreated?: (payload: { packageId: string; name: string; }) => void;
  onPackageUpdated?: (payload: { packageId: string; name: string; }) => void;
  onSectionCreated?: (payload: { sectionId: string; termId: string; packageId: string; title: string; }) => void;
  onSectionUpdated?: (payload: { sectionId: string; termId: string; packageId: string; title: string; }) => void;
  onSectionDeleted?: (payload: { sectionId: string; packageId: string; }) => void;
  onTermCreated?: (payload: { termId: string; packageId: string; title: string; }) => void;
  onTermUpdated?: (payload: { termId: string; packageId: string; title: string; }) => void;
  onTermDeleted?: (payload: { termId: string; packageId: string; }) => void;
  onVideoUpdated?: (payload: { videoId: string; lessonId: string; }) => void;
  onVideoDeleted?: (payload: { videoId: string; lessonId: string; }) => void;
  onPackageAccessRevoked?: (payload: { packageId: string; packageName: string; userId: string; }) => void;
  onVideoWatchLimitChanged?: (payload: { userId: string; videoId: string; newLimit: number; lessonId: string; }) => void;
  onLessonManuallyUnlocked?: (payload: { lessonId: string; studentId: string; }) => void;
  onGamificationPointsChanged?: (payload: { userId: string; newPoints: number; change: number; reason: string; }) => void;
  onLessonCommentCreated?: (payload: { commentId: string; lessonId: string; authorUserId: string; body: string; status: string; }) => void;
  onLessonCommentApproved?: (payload: { commentId: string; lessonId: string; authorUserId: string; body: string; status: string; }) => void;
  onLessonCommentRejected?: (payload: { commentId: string; lessonId: string; authorUserId: string; status: string; }) => void;
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
const activePackages = new Set<string>();
const activeLessons = new Set<string>();

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
  AiJobQueued: new Set<(payload: { lessonVideoId: string; jobType: string; }) => void>(),
  AiJobCancelled: new Set<(payload: { lessonVideoId: string; isMindmapOnly: boolean; }) => void>(),
  CommunityCommentCreated: new Set<(payload: { commentId: string; postId: string; authorUserId: string; body: string; status: string; }) => void>(),
  CommunityCommentApproved: new Set<(payload: { commentId: string; postId: string; authorUserId: string; body: string; status: string; }) => void>(),
  CommunityPostApproved: new Set<(payload: { postId: string; authorUserId: string; status: string; }) => void>(),
  CommunityPostLiked: new Set<(payload: { postId: string; newLikesCount: number; }) => void>(),
  CommunityPostRejected: new Set<(payload: { postId: string; authorUserId: string; status: string; }) => void>(),
  CommunityCommentRejected: new Set<(payload: { commentId: string; postId: string; authorUserId: string; status: string; reason: string; }) => void>(),
  ExamPublished: new Set<(payload: { examId: string; title: string; lessonId: string; }) => void>(),
  ExamResultReady: new Set<(payload: { examId: string; attemptId: string; score: number; isPassed: boolean; }) => void>(),
  ExtraWatchRequestCreated: new Set<(payload: { videoId: string; studentId: string; studentName: string; }) => void>(),
  HomeworkPublished: new Set<(payload: { homeworkId: string; lessonId: string; title: string; }) => void>(),
  HomeworkGraded: new Set<(payload: { homeworkId: string; submissionId: string; score: number; studentId: string; }) => void>(),
  PackageCreated: new Set<(payload: { packageId: string; name: string; }) => void>(),
  PackageUpdated: new Set<(payload: { packageId: string; name: string; }) => void>(),
  SectionCreated: new Set<(payload: { sectionId: string; termId: string; packageId: string; title: string; }) => void>(),
  SectionUpdated: new Set<(payload: { sectionId: string; termId: string; packageId: string; title: string; }) => void>(),
  SectionDeleted: new Set<(payload: { sectionId: string; packageId: string; }) => void>(),
  TermCreated: new Set<(payload: { termId: string; packageId: string; title: string; }) => void>(),
  TermUpdated: new Set<(payload: { termId: string; packageId: string; title: string; }) => void>(),
  TermDeleted: new Set<(payload: { termId: string; packageId: string; }) => void>(),
  VideoUpdated: new Set<(payload: { videoId: string; lessonId: string; }) => void>(),
  VideoDeleted: new Set<(payload: { videoId: string; lessonId: string; }) => void>(),
  PackageAccessRevoked: new Set<(payload: { packageId: string; packageName: string; userId: string; }) => void>(),
  VideoWatchLimitChanged: new Set<(payload: { userId: string; videoId: string; newLimit: number; lessonId: string; }) => void>(),
  LessonManuallyUnlocked: new Set<(payload: { lessonId: string; studentId: string; }) => void>(),
  GamificationPointsChanged: new Set<(payload: { userId: string; newPoints: number; change: number; reason: string; }) => void>(),
  LessonCommentCreated: new Set<(payload: { commentId: string; lessonId: string; authorUserId: string; body: string; status: string; }) => void>(),
  LessonCommentApproved: new Set<(payload: { commentId: string; lessonId: string; authorUserId: string; body: string; status: string; }) => void>(),
  LessonCommentRejected: new Set<(payload: { commentId: string; lessonId: string; authorUserId: string; status: string; }) => void>(),
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
    activePackages.add(packageId);
    if (sharedConnection && sharedConnection.state === signalR.HubConnectionState.Connected) {
      try {
        await sharedConnection.invoke('JoinPackage', packageId);
      } catch (err) {
        console.error('Failed to join package group:', err);
      }
    }
  }, []);

  const leavePackage = useCallback(async (packageId: string) => {
    activePackages.delete(packageId);
    if (sharedConnection && sharedConnection.state === signalR.HubConnectionState.Connected) {
      try {
        await sharedConnection.invoke('LeavePackage', packageId);
      } catch (err) {
        console.error('Failed to leave package group:', err);
      }
    }
  }, []);

  const joinLesson = useCallback(async (lessonId: string) => {
    activeLessons.add(lessonId);
    if (sharedConnection && sharedConnection.state === signalR.HubConnectionState.Connected) {
      try {
        await sharedConnection.invoke('JoinLesson', lessonId);
      } catch (err) {
        console.error('Failed to join lesson group:', err);
      }
    }
  }, []);

  const leaveLesson = useCallback(async (lessonId: string) => {
    activeLessons.delete(lessonId);
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
    const onAiJobQueued = (payload: { lessonVideoId: string; jobType: string; }) => {
      handlersRef.current?.onAiJobQueued?.(payload);
    };
    const onAiJobCancelled = (payload: { lessonVideoId: string; isMindmapOnly: boolean; }) => {
      handlersRef.current?.onAiJobCancelled?.(payload);
    };
    const onCommunityCommentCreated = (payload: { commentId: string; postId: string; authorUserId: string; body: string; status: string; }) => {
      handlersRef.current?.onCommunityCommentCreated?.(payload);
    };
    const onCommunityCommentApproved = (payload: { commentId: string; postId: string; authorUserId: string; body: string; status: string; }) => {
      handlersRef.current?.onCommunityCommentApproved?.(payload);
    };
    const onCommunityPostApproved = (payload: { postId: string; authorUserId: string; status: string; }) => {
      handlersRef.current?.onCommunityPostApproved?.(payload);
    };
    const onCommunityPostLiked = (payload: { postId: string; newLikesCount: number; }) => {
      handlersRef.current?.onCommunityPostLiked?.(payload);
    };
    const onCommunityPostRejected = (payload: { postId: string; authorUserId: string; status: string; }) => {
      handlersRef.current?.onCommunityPostRejected?.(payload);
    };
    const onCommunityCommentRejected = (payload: { commentId: string; postId: string; authorUserId: string; status: string; reason: string; }) => {
      handlersRef.current?.onCommunityCommentRejected?.(payload);
    };
    const onExamPublished = (payload: { examId: string; title: string; lessonId: string; }) => {
      handlersRef.current?.onExamPublished?.(payload);
    };
    const onExamResultReady = (payload: { examId: string; attemptId: string; score: number; isPassed: boolean; }) => {
      handlersRef.current?.onExamResultReady?.(payload);
    };
    const onExtraWatchRequestCreated = (payload: { videoId: string; studentId: string; studentName: string; }) => {
      handlersRef.current?.onExtraWatchRequestCreated?.(payload);
    };
    const onHomeworkPublished = (payload: { homeworkId: string; lessonId: string; title: string; }) => {
      handlersRef.current?.onHomeworkPublished?.(payload);
    };
    const onHomeworkGraded = (payload: { homeworkId: string; submissionId: string; score: number; studentId: string; }) => {
      handlersRef.current?.onHomeworkGraded?.(payload);
    };
    const onPackageCreated = (payload: { packageId: string; name: string; }) => {
      handlersRef.current?.onPackageCreated?.(payload);
    };
    const onPackageUpdated = (payload: { packageId: string; name: string; }) => {
      handlersRef.current?.onPackageUpdated?.(payload);
    };
    const onSectionCreated = (payload: { sectionId: string; termId: string; packageId: string; title: string; }) => {
      handlersRef.current?.onSectionCreated?.(payload);
    };
    const onSectionUpdated = (payload: { sectionId: string; termId: string; packageId: string; title: string; }) => {
      handlersRef.current?.onSectionUpdated?.(payload);
    };
    const onSectionDeleted = (payload: { sectionId: string; packageId: string; }) => {
      handlersRef.current?.onSectionDeleted?.(payload);
    };
    const onTermCreated = (payload: { termId: string; packageId: string; title: string; }) => {
      handlersRef.current?.onTermCreated?.(payload);
    };
    const onTermUpdated = (payload: { termId: string; packageId: string; title: string; }) => {
      handlersRef.current?.onTermUpdated?.(payload);
    };
    const onTermDeleted = (payload: { termId: string; packageId: string; }) => {
      handlersRef.current?.onTermDeleted?.(payload);
    };
    const onVideoUpdated = (payload: { videoId: string; lessonId: string; }) => {
      handlersRef.current?.onVideoUpdated?.(payload);
    };
    const onVideoDeleted = (payload: { videoId: string; lessonId: string; }) => {
      handlersRef.current?.onVideoDeleted?.(payload);
    };
    const onPackageAccessRevoked = (payload: { packageId: string; packageName: string; userId: string; }) => {
      handlersRef.current?.onPackageAccessRevoked?.(payload);
    };
    const onVideoWatchLimitChanged = (payload: { userId: string; videoId: string; newLimit: number; lessonId: string; }) => {
      handlersRef.current?.onVideoWatchLimitChanged?.(payload);
    };
    const onLessonManuallyUnlocked = (payload: { lessonId: string; studentId: string; }) => {
      handlersRef.current?.onLessonManuallyUnlocked?.(payload);
    };
    const onGamificationPointsChanged = (payload: { userId: string; newPoints: number; change: number; reason: string; }) => {
      handlersRef.current?.onGamificationPointsChanged?.(payload);
    };
    const onLessonCommentCreated = (payload: { commentId: string; lessonId: string; authorUserId: string; body: string; status: string; }) => {
      handlersRef.current?.onLessonCommentCreated?.(payload);
    };
    const onLessonCommentApproved = (payload: { commentId: string; lessonId: string; authorUserId: string; body: string; status: string; }) => {
      handlersRef.current?.onLessonCommentApproved?.(payload);
    };
    const onLessonCommentRejected = (payload: { commentId: string; lessonId: string; authorUserId: string; status: string; }) => {
      handlersRef.current?.onLessonCommentRejected?.(payload);
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
    listeners.AiJobQueued.add(onAiJobQueued);
    listeners.AiJobCancelled.add(onAiJobCancelled);
    listeners.CommunityCommentCreated.add(onCommunityCommentCreated);
    listeners.CommunityCommentApproved.add(onCommunityCommentApproved);
    listeners.CommunityPostApproved.add(onCommunityPostApproved);
    listeners.CommunityPostLiked.add(onCommunityPostLiked);
    listeners.CommunityPostRejected.add(onCommunityPostRejected);
    listeners.CommunityCommentRejected.add(onCommunityCommentRejected);
    listeners.ExamPublished.add(onExamPublished);
    listeners.ExamResultReady.add(onExamResultReady);
    listeners.ExtraWatchRequestCreated.add(onExtraWatchRequestCreated);
    listeners.HomeworkPublished.add(onHomeworkPublished);
    listeners.HomeworkGraded.add(onHomeworkGraded);
    listeners.PackageCreated.add(onPackageCreated);
    listeners.PackageUpdated.add(onPackageUpdated);
    listeners.SectionCreated.add(onSectionCreated);
    listeners.SectionUpdated.add(onSectionUpdated);
    listeners.SectionDeleted.add(onSectionDeleted);
    listeners.TermCreated.add(onTermCreated);
    listeners.TermUpdated.add(onTermUpdated);
    listeners.TermDeleted.add(onTermDeleted);
    listeners.VideoUpdated.add(onVideoUpdated);
    listeners.VideoDeleted.add(onVideoDeleted);
    listeners.PackageAccessRevoked.add(onPackageAccessRevoked);
    listeners.VideoWatchLimitChanged.add(onVideoWatchLimitChanged);
    listeners.LessonManuallyUnlocked.add(onLessonManuallyUnlocked);
    listeners.GamificationPointsChanged.add(onGamificationPointsChanged);
    listeners.LessonCommentCreated.add(onLessonCommentCreated);
    listeners.LessonCommentApproved.add(onLessonCommentApproved);
    listeners.LessonCommentRejected.add(onLessonCommentRejected);
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

        sharedConnection.onreconnected(async (connectionId) => {
          console.log('Platform SignalR reconnected:', connectionId);
          connectionStatusListeners.forEach(listener => listener(true));

          // Re-join active package groups
          for (const packageId of activePackages) {
            try {
              await sharedConnection?.invoke('JoinPackage', packageId);
              console.log(`Re-joined package group on reconnect: ${packageId}`);
            } catch (err) {
              console.error(`Failed to re-join package group ${packageId} on reconnect:`, err);
            }
          }

          // Re-join active lesson groups
          for (const lessonId of activeLessons) {
            try {
              await sharedConnection?.invoke('JoinLesson', lessonId);
              console.log(`Re-joined lesson group on reconnect: ${lessonId}`);
            } catch (err) {
              console.error(`Failed to re-join lesson group ${lessonId} on reconnect:`, err);
            }
          }
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

        sharedConnection.on('AiJobQueued', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { lessonVideoId: string; jobType: string; };
            listeners.AiJobQueued.forEach(handler => handler(payload));
            invalidateMany(['admin:ai-monitor']);
          } catch (e) {
            console.error('Error handling AiJobQueued event:', e);
          }
        });

        sharedConnection.on('AiJobCancelled', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { lessonVideoId: string; isMindmapOnly: boolean; };
            listeners.AiJobCancelled.forEach(handler => handler(payload));
            invalidateMany(['admin:ai-monitor']);
          } catch (e) {
            console.error('Error handling AiJobCancelled event:', e);
          }
        });

        sharedConnection.on('CommunityCommentCreated', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { commentId: string; postId: string; authorUserId: string; body: string; status: string; };
            listeners.CommunityCommentCreated.forEach(handler => handler(payload));
            invalidateMany(['community:posts']);
          } catch (e) {
            console.error('Error handling CommunityCommentCreated event:', e);
          }
        });

        sharedConnection.on('CommunityCommentApproved', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { commentId: string; postId: string; authorUserId: string; body: string; status: string; };
            listeners.CommunityCommentApproved.forEach(handler => handler(payload));
            invalidateMany(['community:posts']);
          } catch (e) {
            console.error('Error handling CommunityCommentApproved event:', e);
          }
        });

        sharedConnection.on('CommunityPostApproved', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { postId: string; authorUserId: string; status: string; };
            listeners.CommunityPostApproved.forEach(handler => handler(payload));
            invalidateMany(['community:posts']);
          } catch (e) {
            console.error('Error handling CommunityPostApproved event:', e);
          }
        });

        sharedConnection.on('CommunityPostLiked', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { postId: string; newLikesCount: number; };
            listeners.CommunityPostLiked.forEach(handler => handler(payload));
            invalidateMany(['community:posts']);
          } catch (e) {
            console.error('Error handling CommunityPostLiked event:', e);
          }
        });

        sharedConnection.on('CommunityPostRejected', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { postId: string; authorUserId: string; status: string; };
            listeners.CommunityPostRejected.forEach(handler => handler(payload));
            invalidateMany(['community:posts']);
          } catch (e) {
            console.error('Error handling CommunityPostRejected event:', e);
          }
        });

        sharedConnection.on('CommunityCommentRejected', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { commentId: string; postId: string; authorUserId: string; status: string; reason: string; };
            listeners.CommunityCommentRejected.forEach(handler => handler(payload));
            invalidateMany(['community:posts']);
          } catch (e) {
            console.error('Error handling CommunityCommentRejected event:', e);
          }
        });

        sharedConnection.on('ExamPublished', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { examId: string; title: string; lessonId: string; };
            listeners.ExamPublished.forEach(handler => handler(payload));
            invalidateMany(['student:exams']);
          } catch (e) {
            console.error('Error handling ExamPublished event:', e);
          }
        });

        sharedConnection.on('ExamResultReady', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { examId: string; attemptId: string; score: number; isPassed: boolean; };
            listeners.ExamResultReady.forEach(handler => handler(payload));
            invalidateMany(['student:exams']);
          } catch (e) {
            console.error('Error handling ExamResultReady event:', e);
          }
        });

        sharedConnection.on('ExtraWatchRequestCreated', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { videoId: string; studentId: string; studentName: string; };
            listeners.ExtraWatchRequestCreated.forEach(handler => handler(payload));
            invalidateMany(['admin:ai-monitor']);
          } catch (e) {
            console.error('Error handling ExtraWatchRequestCreated event:', e);
          }
        });

        sharedConnection.on('HomeworkPublished', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { homeworkId: string; lessonId: string; title: string; };
            listeners.HomeworkPublished.forEach(handler => handler(payload));
            invalidateMany(['student:homeworks']);
          } catch (e) {
            console.error('Error handling HomeworkPublished event:', e);
          }
        });

        sharedConnection.on('HomeworkGraded', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { homeworkId: string; submissionId: string; score: number; studentId: string; };
            listeners.HomeworkGraded.forEach(handler => handler(payload));
            invalidateMany(['student:homeworks']);
          } catch (e) {
            console.error('Error handling HomeworkGraded event:', e);
          }
        });

        sharedConnection.on('PackageCreated', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { packageId: string; name: string; };
            listeners.PackageCreated.forEach(handler => handler(payload));
            invalidateMany(['content:packages']);
          } catch (e) {
            console.error('Error handling PackageCreated event:', e);
          }
        });

        sharedConnection.on('PackageUpdated', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { packageId: string; name: string; };
            listeners.PackageUpdated.forEach(handler => handler(payload));
            invalidateMany(['content:packages']);
          } catch (e) {
            console.error('Error handling PackageUpdated event:', e);
          }
        });

        sharedConnection.on('SectionCreated', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { sectionId: string; termId: string; packageId: string; title: string; };
            listeners.SectionCreated.forEach(handler => handler(payload));
            invalidateMany(['content:packages']);
          } catch (e) {
            console.error('Error handling SectionCreated event:', e);
          }
        });

        sharedConnection.on('SectionUpdated', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { sectionId: string; termId: string; packageId: string; title: string; };
            listeners.SectionUpdated.forEach(handler => handler(payload));
            invalidateMany(['content:packages']);
          } catch (e) {
            console.error('Error handling SectionUpdated event:', e);
          }
        });

        sharedConnection.on('SectionDeleted', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { sectionId: string; packageId: string; };
            listeners.SectionDeleted.forEach(handler => handler(payload));
            invalidateMany(['content:packages']);
          } catch (e) {
            console.error('Error handling SectionDeleted event:', e);
          }
        });

        sharedConnection.on('TermCreated', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { termId: string; packageId: string; title: string; };
            listeners.TermCreated.forEach(handler => handler(payload));
            invalidateMany(['content:packages']);
          } catch (e) {
            console.error('Error handling TermCreated event:', e);
          }
        });

        sharedConnection.on('TermUpdated', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { termId: string; packageId: string; title: string; };
            listeners.TermUpdated.forEach(handler => handler(payload));
            invalidateMany(['content:packages']);
          } catch (e) {
            console.error('Error handling TermUpdated event:', e);
          }
        });

        sharedConnection.on('TermDeleted', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { termId: string; packageId: string; };
            listeners.TermDeleted.forEach(handler => handler(payload));
            invalidateMany(['content:packages']);
          } catch (e) {
            console.error('Error handling TermDeleted event:', e);
          }
        });

        sharedConnection.on('VideoUpdated', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { videoId: string; lessonId: string; };
            listeners.VideoUpdated.forEach(handler => handler(payload));
            invalidateMany([`content:lesson:${payload.lessonId}`]);
          } catch (e) {
            console.error('Error handling VideoUpdated event:', e);
          }
        });

        sharedConnection.on('VideoDeleted', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { videoId: string; lessonId: string; };
            listeners.VideoDeleted.forEach(handler => handler(payload));
            invalidateMany([`content:lesson:${payload.lessonId}`]);
          } catch (e) {
            console.error('Error handling VideoDeleted event:', e);
          }
        });

        sharedConnection.on('PackageAccessRevoked', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { packageId: string; packageName: string; userId: string; };
            listeners.PackageAccessRevoked.forEach(handler => handler(payload));
            invalidateMany(['content:packages', 'student:shell']);
          } catch (e) {
            console.error('Error handling PackageAccessRevoked event:', e);
          }
        });

        sharedConnection.on('VideoWatchLimitChanged', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { userId: string; videoId: string; newLimit: number; lessonId: string; };
            listeners.VideoWatchLimitChanged.forEach(handler => handler(payload));
            invalidateMany([`content:lesson:${payload.lessonId}`]);
          } catch (e) {
            console.error('Error handling VideoWatchLimitChanged event:', e);
          }
        });

        sharedConnection.on('LessonManuallyUnlocked', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { lessonId: string; studentId: string; };
            listeners.LessonManuallyUnlocked.forEach(handler => handler(payload));
            invalidateMany([`content:lesson:${payload.lessonId}`, 'content:packages']);
          } catch (e) {
            console.error('Error handling LessonManuallyUnlocked event:', e);
          }
        });

        sharedConnection.on('GamificationPointsChanged', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { userId: string; newPoints: number; change: number; reason: string; };
            listeners.GamificationPointsChanged.forEach(handler => handler(payload));
            invalidateMany(['student:shell']);
          } catch (e) {
            console.error('Error handling GamificationPointsChanged event:', e);
          }
        });

        sharedConnection.on('LessonCommentCreated', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { commentId: string; lessonId: string; authorUserId: string; body: string; status: string; };
            listeners.LessonCommentCreated.forEach(handler => handler(payload));
            invalidateMany([`content:lesson:${payload.lessonId}:comments`]);
          } catch (e) {
            console.error('Error handling LessonCommentCreated event:', e);
          }
        });

        sharedConnection.on('LessonCommentApproved', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { commentId: string; lessonId: string; authorUserId: string; body: string; status: string; };
            listeners.LessonCommentApproved.forEach(handler => handler(payload));
            invalidateMany([`content:lesson:${payload.lessonId}:comments`]);
          } catch (e) {
            console.error('Error handling LessonCommentApproved event:', e);
          }
        });

        sharedConnection.on('LessonCommentRejected', (payloadJson: string) => {
          try {
            const payload = JSON.parse(payloadJson) as { commentId: string; lessonId: string; authorUserId: string; status: string; };
            listeners.LessonCommentRejected.forEach(handler => handler(payload));
            invalidateMany([`content:lesson:${payload.lessonId}:comments`]);
          } catch (e) {
            console.error('Error handling LessonCommentRejected event:', e);
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
