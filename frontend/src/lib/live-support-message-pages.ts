import type { LiveSupportMessage } from '@/services/live-support-service';

export interface LiveSupportThreadPagination {
  initialized: boolean;
  cursor?: string;
  resumePoints: LiveSupportThreadResumePoint[];
  headMessageIds: string[];
}

interface LiveSupportThreadResumePoint {
  cursor: string | null;
  anchorMessageIds: string[];
}

interface LiveSupportThreadPageIdentity {
  items: ReadonlyArray<{ id: string }>;
  nextCursor?: string | null;
}

export interface LiveSupportThreadAdvanceResult {
  pagination: LiveSupportThreadPagination;
  historyGapUnresolved: boolean;
  stale: boolean;
}

export function createLiveSupportThreadPagination(): LiveSupportThreadPagination {
  return {
    initialized: false,
    cursor: undefined,
    resumePoints: [],
    headMessageIds: [],
  };
}

export function reconcileLiveSupportThreadHead(
  current: LiveSupportThreadPagination,
  page: LiveSupportThreadPageIdentity,
): LiveSupportThreadPagination {
  const nextCursor = normalizeCursor(page.nextCursor);
  const headMessageIds = uniqueMessageIds(page.items);
  if (!current.initialized || current.headMessageIds.length === 0) {
    return {
      initialized: true,
      cursor: nextCursor,
      resumePoints: [],
      headMessageIds,
    };
  }
  if (headMessageIds.length === 0) return current;

  const previousHeadIds = new Set(current.headMessageIds);
  const overlapsPreviousHead = headMessageIds.some((id) =>
    previousHeadIds.has(id)
  );
  if (overlapsPreviousHead || !nextCursor) {
    return { ...current, headMessageIds };
  }

  return {
    initialized: true,
    cursor: nextCursor,
    resumePoints: [
      ...current.resumePoints,
      {
        cursor: current.cursor ?? null,
        anchorMessageIds: current.headMessageIds,
      },
    ],
    headMessageIds,
  };
}

export function advanceLiveSupportThreadHistory(
  current: LiveSupportThreadPagination,
  requestedCursor: string,
  page: LiveSupportThreadPageIdentity,
): LiveSupportThreadAdvanceResult {
  const nextCursor = normalizeCursor(page.nextCursor);
  if (current.cursor !== requestedCursor) {
    return {
      pagination: current,
      historyGapUnresolved: false,
      stale: true,
    };
  }
  if (nextCursor === requestedCursor) {
    return {
      pagination: current,
      historyGapUnresolved: true,
      stale: false,
    };
  }
  if (current.resumePoints.length === 0) {
    return {
      pagination: { ...current, cursor: nextCursor },
      historyGapUnresolved: false,
      stale: false,
    };
  }

  const resumePoint = current.resumePoints.at(-1)!;
  const anchorMessageIds = new Set(resumePoint.anchorMessageIds);
  const reachedLoadedHistory = page.items.some((message) =>
    anchorMessageIds.has(message.id)
  );
  if (reachedLoadedHistory) {
    return {
      pagination: {
        ...current,
        cursor: resumePoint.cursor ?? undefined,
        resumePoints: current.resumePoints.slice(0, -1),
      },
      historyGapUnresolved: false,
      stale: false,
    };
  }

  if (!nextCursor) {
    return {
      pagination: current,
      historyGapUnresolved: true,
      stale: false,
    };
  }

  return {
    pagination: { ...current, cursor: nextCursor },
    historyGapUnresolved: false,
    stale: false,
  };
}

export function mergeOrderedLiveSupportMessages(
  currentMessages: LiveSupportMessage[],
  incomingMessages: LiveSupportMessage[],
) {
  const messagesById = new Map(
    currentMessages.map((message) => [message.id, message]),
  );
  for (const message of incomingMessages) {
    messagesById.set(message.id, message);
  }
  return [...messagesById.values()].sort(compareLiveSupportMessages);
}

function compareLiveSupportMessages(
  left: LiveSupportMessage,
  right: LiveSupportMessage,
) {
  const timeDifference = Date.parse(left.sentAt) - Date.parse(right.sentAt);
  if (timeDifference !== 0) return timeDifference;
  if (left.id === right.id) return 0;
  return left.id < right.id ? -1 : 1;
}

function normalizeCursor(cursor?: string | null) {
  return cursor || undefined;
}

function uniqueMessageIds(items: ReadonlyArray<{ id: string }>) {
  return [...new Set(items.map((item) => item.id))];
}
