'use client';
import { useCallback, useEffect, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useAuthStore } from '@/stores/auth-store';
import {
  adminAiAgentService,
  createAdminAiIntentKey,
} from '@/services/admin-ai-agent-service';
import type {
  AdminAiApiError,
  AdminAiConversationSnapshot,
  AdminAiConversationSummary,
  AdminAiProposal,
  AdminAiSecureInputKind,
} from '@/services/admin-ai-agent-contract';
import { parseAdminAiApiError } from '@/services/admin-ai-agent-contract';
import { useAdminAiAgentStore } from './admin-ai-agent-store';
import { useAdminAiAgentEvents } from '@/hooks/useAdminAiAgentEvents';
import { adminAiRefreshKeys } from '@/lib/query-contracts';
import { invalidateCanonicalKeys } from '@/lib/realtime-invalidation-map';

const safeError = (error: unknown): AdminAiApiError => {
  const candidate = error as {
    response?: { data?: { error?: AdminAiApiError } };
  };
  return (
    parseAdminAiApiError(candidate.response?.data?.error) ?? {
      code: 'UNKNOWN_SAFE_FAILURE',
      messageAr: 'حدث خطأ آمن غير متوقع. حاول مرة أخرى.',
      retryAfterSeconds: null,
      traceId: '',
      currentVersion: null,
    }
  );
};
export function useAdminAiAgentController() {
  const router = useRouter();
  const owner = useAuthStore((s) => s.user?.id);
  const store = useAdminAiAgentStore();
  const selectedConversationId = store.selectedConversationId;
  const clearSecurityBoundary = store.clearSecurityBoundary;
  const [items, setItems] = useState<AdminAiConversationSummary[]>([]);
  const [listCursor, setListCursor] = useState<string>();
  const [loadingMoreConversations, setLoadingMoreConversations] =
    useState(false);
  const [snapshot, setSnapshot] = useState<AdminAiConversationSnapshot>();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<AdminAiApiError>();
  const [archived, setArchived] = useState(false);
  const [proposalBusy, setProposalBusy] = useState<string>();
  const [secureProposal, setSecureProposal] = useState<AdminAiProposal>();
  const [loadingOlder, setLoadingOlder] = useState(false);
  const controllers = useRef(new Set<AbortController>());
  const snapshotGeneration = useRef(0);
  const retainedIntentKeys = useRef(new Map<string, string>());
  const intentKey = (identity: string) => {
    const existing = retainedIntentKeys.current.get(identity);
    if (existing) return existing;
    const created = createAdminAiIntentKey();
    retainedIntentKeys.current.set(identity, created);
    return created;
  };
  const request = useCallback(
    async <T>(fn: (signal: AbortSignal) => Promise<T>) => {
      const c = new AbortController();
      controllers.current.add(c);
      try {
        return await fn(c.signal);
      } finally {
        controllers.current.delete(c);
      }
    },
    []
  );
  const loadList = useCallback(async () => {
    if (!owner) return;
    setLoading(true);
    try {
      const page = await request((s) =>
        adminAiAgentService.list(s, undefined, archived ? 'Archived' : 'Active')
      );
      setItems(page.items);
      setListCursor(page.nextCursor);
      setError(undefined);
    } catch (e) {
      setError(safeError(e));
    } finally {
      setLoading(false);
    }
  }, [archived, owner, request]);
  const loadMoreConversations = async () => {
    if (!listCursor || loadingMoreConversations) return;
    setLoadingMoreConversations(true);
    try {
      const page = await request((signal) =>
        adminAiAgentService.list(
          signal,
          listCursor,
          archived ? 'Archived' : 'Active'
        )
      );
      setItems((current) => [...current, ...page.items]);
      setListCursor(page.nextCursor);
    } catch (requestError) {
      setError(safeError(requestError));
    } finally {
      setLoadingMoreConversations(false);
    }
  };
  const reconcile = useCallback(async () => {
    if (!selectedConversationId) return;
    const generation = ++snapshotGeneration.current;
    try {
      const data = await request((s) =>
        adminAiAgentService.snapshot(s, selectedConversationId)
      );
      if (
        generation !== snapshotGeneration.current ||
        useAdminAiAgentStore.getState().selectedConversationId !==
          selectedConversationId
      )
        return;
      setSnapshot(data);
    } catch (e) {
      const safe = safeError(e);
      setError(safe);
      if (
        ['UNAUTHORIZED', 'ADMIN_REQUIRED', 'ACCESS_REVOKED'].includes(safe.code)
      ) {
        clearSecurityBoundary();
        router.replace('/admin/unauthorized');
      }
    }
  }, [clearSecurityBoundary, request, router, selectedConversationId]);
  useEffect(() => {
    const activeControllers = controllers.current;
    void loadList();
    return () => {
      activeControllers.forEach((c) => c.abort());
    };
  }, [loadList]);
  useEffect(() => {
    if (selectedConversationId) void reconcile();
    else setSnapshot(undefined);
  }, [selectedConversationId, reconcile]);
  useEffect(
    () => () => {
      retainedIntentKeys.current.clear();
      snapshotGeneration.current += 1;
      clearSecurityBoundary();
    },
    [clearSecurityBoundary]
  );
  useAdminAiAgentEvents({
    onRefresh: reconcile,
    onListRefresh: loadList,
    onAccessRevoked: () => {
      controllers.current.forEach((c) => c.abort());
      retainedIntentKeys.current.clear();
      snapshotGeneration.current += 1;
      clearSecurityBoundary();
      setSnapshot(undefined);
      setItems([]);
      router.replace('/admin/unauthorized');
    },
  });
  const create = async () => {
    const identity = 'create';
    const key = intentKey(identity);
    if (!store.beginIntent('create', key)) return;
    try {
      const item = await request((s) => adminAiAgentService.create(s, key));
      setItems((v) => [item, ...v]);
      store.selectConversation(item.id);
      retainedIntentKeys.current.delete(identity);
    } catch (e) {
      setError(safeError(e));
    } finally {
      store.finishIntent('create', key);
    }
  };
  const send = async () => {
    const text = store.draft.trim();
    if (!text || !snapshot) return;
    const identity = `send:${snapshot.conversation.id}:${snapshot.conversation.version}:${text}`;
    const key = intentKey(identity);
    if (!store.beginIntent('send', key)) return;
    try {
      await request((s) =>
        adminAiAgentService.send(
          s,
          snapshot.conversation.id,
          text,
          snapshot.conversation.version,
          key
        )
      );
      store.setDraft('');
      retainedIntentKeys.current.delete(identity);
      await reconcile();
    } catch (e) {
      setError(safeError(e));
    } finally {
      store.finishIntent('send', key);
    }
  };
  const stop = async () => {
    const turn = (snapshot?.activeTurns ?? snapshot?.turns)?.[0];
    if (!snapshot || !turn) return;
    const identity = `cancel-turn:${turn.id}:${turn.version}`;
    const key = intentKey(identity);
    try {
      await request((s) =>
        adminAiAgentService.cancelTurn(
          s,
          snapshot.conversation.id,
          turn.id,
          turn.version,
          key
        )
      );
      await reconcile();
      retainedIntentKeys.current.delete(identity);
    } catch (e) {
      setError(safeError(e));
    }
  };
  const retryTurn = async () => {
    const failedTurn = (snapshot?.activeTurns ?? snapshot?.turns)?.find(
      (turn) => turn.canRetry
    );
    const sourceMessage = snapshot?.messages
      .slice()
      .reverse()
      .find(
        (message) =>
          message.role === 'Admin' && message.turnId === failedTurn?.id
      );
    if (!snapshot || !sourceMessage) return;
    const intentName = `retry:${failedTurn?.id}`;
    const key = intentKey(intentName);
    if (!store.beginIntent(intentName, key)) return;
    try {
      await request((signal) =>
        adminAiAgentService.send(
          signal,
          snapshot.conversation.id,
          sourceMessage.content,
          snapshot.conversation.version,
          key
        )
      );
      await reconcile();
      retainedIntentKeys.current.delete(intentName);
    } catch (requestError) {
      setError(safeError(requestError));
    } finally {
      store.finishIntent(intentName, key);
    }
  };
  const loadOlder = async () => {
    if (!snapshot?.nextBeforeSequence || loadingOlder) return;
    setLoadingOlder(true);
    try {
      const older = await request((signal) =>
        adminAiAgentService.snapshot(
          signal,
          snapshot.conversation.id,
          snapshot.nextBeforeSequence ?? undefined
        )
      );
      setSnapshot((current) => {
        if (!current) return older;
        const messages = [...older.messages, ...current.messages];
        return {
          ...current,
          messages: messages.filter(
            (message, index) =>
              messages.findIndex((candidate) => candidate.id === message.id) ===
              index
          ),
          nextBeforeSequence: older.nextBeforeSequence,
        };
      });
    } catch (requestError) {
      setError(safeError(requestError));
    } finally {
      setLoadingOlder(false);
    }
  };
  const confirm = async (id: string, phrase?: string) => {
    const p = snapshot?.proposals?.find((x) => x.id === id);
    if (!p || proposalBusy) return;
    const identity = `confirm:${id}:${p.version}`;
    setProposalBusy(id);
    try {
      const execution = await request((s) =>
        adminAiAgentService.confirmProposal(
          s,
          id,
          p.version,
          intentKey(identity),
          phrase
        )
      );
      execution.refreshScopes.forEach((scope) =>
        invalidateCanonicalKeys(adminAiRefreshKeys(scope))
      );
      await reconcile();
      await loadList();
      retainedIntentKeys.current.delete(identity);
    } catch (e) {
      setError(safeError(e));
      await reconcile();
    } finally {
      setProposalBusy(undefined);
    }
  };
  const cancelProposal = async (id: string) => {
    const p = snapshot?.proposals?.find((x) => x.id === id);
    if (!p || proposalBusy) return;
    const identity = `cancel-proposal:${id}:${p.version}`;
    setProposalBusy(id);
    try {
      await request((s) =>
        adminAiAgentService.cancelProposal(
          s,
          id,
          p.version,
          intentKey(identity)
        )
      );
      await reconcile();
      retainedIntentKeys.current.delete(identity);
    } catch (e) {
      setError(safeError(e));
    } finally {
      setProposalBusy(undefined);
    }
  };
  const submitSecure = async (value: string) => {
    if (!secureProposal?.secureInputKind) return;
    setProposalBusy(secureProposal.id);
    try {
      const grantIdentity = `secure-grant:${secureProposal.id}:${secureProposal.version}`;
      const key = intentKey(grantIdentity);
      const grant = await request((s) =>
        adminAiAgentService.issueSecureGrant(
          s,
          secureProposal.id,
          secureProposal.secureInputKind!,
          secureProposal.version,
          key
        )
      );
      const input =
        secureProposal.secureInputKind === 'PrivateFile'
          ? { kind: 'PrivateFile' as const, privateObjectToken: value }
          : {
              kind: secureProposal.secureInputKind as Exclude<
                AdminAiSecureInputKind,
                'PrivateFile'
              >,
              value,
            };
      await request((s) =>
        adminAiAgentService.submitSecureInput(
          s,
          grant.id,
          input,
          intentKey(`secure-submit:${grant.id}`)
        )
      );
      retainedIntentKeys.current.delete(grantIdentity);
      retainedIntentKeys.current.delete(`secure-submit:${grant.id}`);
      setSecureProposal(undefined);
      await reconcile();
    } catch (e) {
      setError(safeError(e));
    } finally {
      setProposalBusy(undefined);
    }
  };
  const archiveConversation = async (item: AdminAiConversationSummary) => {
    try {
      const identity = `${archived ? 'restore' : 'archive'}:${item.id}:${item.version}`;
      const key = intentKey(identity);
      await request((s) =>
        archived
          ? adminAiAgentService.restore(s, item.id, item.version, key)
          : adminAiAgentService.archive(s, item.id, item.version, key)
      );
      if (store.selectedConversationId === item.id) store.selectConversation();
      await loadList();
      retainedIntentKeys.current.delete(identity);
    } catch (e) {
      setError(safeError(e));
    }
  };
  const rename = async () => {
    if (!snapshot) return;
    const title = window
      .prompt('اسم المحادثة', snapshot.conversation.title)
      ?.trim();
    if (!title || title === snapshot.conversation.title) return;
    const identity = `rename:${snapshot.conversation.id}:${snapshot.conversation.version}:${title}`;
    try {
      await request((s) =>
        adminAiAgentService.rename(
          s,
          snapshot.conversation.id,
          title,
          snapshot.conversation.version,
          intentKey(identity)
        )
      );
      await Promise.all([loadList(), reconcile()]);
      retainedIntentKeys.current.delete(identity);
    } catch (e) {
      setError(safeError(e));
    }
  };
  return {
    ...store,
    items,
    snapshot,
    loading,
    error,
    archived,
    proposalBusy,
    secureProposal,
    setArchived,
    create,
    send,
    stop,
    retryTurn,
    loadOlder,
    loadingOlder,
    confirm,
    cancelProposal,
    submitSecure,
    setSecureProposal,
    archiveConversation,
    rename,
    reconcile,
    loadList,
    loadMoreConversations,
    listCursor,
    loadingMoreConversations,
  };
}
