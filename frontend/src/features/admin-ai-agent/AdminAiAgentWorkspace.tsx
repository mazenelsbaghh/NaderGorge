'use client';
import { AdminAiComposer } from './AdminAiComposer';
import { AdminAiConversationHeader } from './AdminAiConversationHeader';
import { AdminAiConversationList } from './AdminAiConversationList';
import { AdminAiErrorState } from './AdminAiErrorState';
import { AdminAiSkeleton } from './AdminAiSkeleton';
import { AdminAiTranscript } from './AdminAiTranscript';
import { AdminAiSecureInputOverlay } from './AdminAiSecureInputOverlay';
import { useAdminAiAgentController } from './useAdminAiAgentController';
import { AdminAiAuditEvidence } from './AdminAiAuditEvidence';
export function AdminAiAgentWorkspace() {
  const c = useAdminAiAgentController();
  const activeTurn = (c.snapshot?.activeTurns ?? c.snapshot?.turns)?.[0];
  const showList = c.responsiveView === 'history';
  return (
    <section
      aria-label="مساحة محادثات وكيل الإدارة"
      className="grid h-[min(760px,calc(100dvh-11rem))] min-h-[34rem] overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] font-sans text-[var(--admin-text)] lg:grid-cols-[19rem_minmax(0,1fr)]"
    >
      <div className={`${showList ? 'flex' : 'hidden'} min-h-0 lg:flex`}>
        <AdminAiConversationList
          items={c.items}
          selectedId={c.selectedConversationId}
          archived={c.archived}
          onSelect={c.selectConversation}
          onCreate={() => void c.create()}
          onToggleArchived={() => c.setArchived(!c.archived)}
          onArchive={(item) => void c.archiveConversation(item)}
          onLoadMore={() => void c.loadMoreConversations()}
          hasMore={Boolean(c.listCursor)}
          loadingMore={c.loadingMoreConversations}
        />
      </div>
      <div
        className={`${showList ? 'hidden' : 'flex'} min-h-0 min-w-0 flex-col lg:flex`}
      >
        <AdminAiConversationHeader
          conversation={c.snapshot?.conversation}
          connection={c.connection}
          onHistory={() => c.setResponsiveView('history')}
          onRename={() => void c.rename()}
        />
        <AdminAiAuditEvidence />
        {c.error && c.snapshot && (
          <div
            role="alert"
            className="border-b border-[var(--admin-warning)] bg-[var(--admin-warning-10)] px-4 py-2 text-sm font-bold text-[var(--admin-warning)]"
          >
            {c.error.messageAr}
          </div>
        )}
        {c.loading && !c.snapshot ? (
          <AdminAiSkeleton />
        ) : c.error && !c.snapshot ? (
          <AdminAiErrorState
            message={c.error.messageAr}
            onRetry={() => void c.loadList()}
          />
        ) : (
          <AdminAiTranscript
            snapshot={c.snapshot}
            submitting={Boolean(c.inFlightIntents.send) && !activeTurn}
            busyProposalId={c.proposalBusy}
            onConfirm={(id, phrase) => void c.confirm(id, phrase)}
            onCancelProposal={(id) => void c.cancelProposal(id)}
            onSecureInput={(id) =>
              c.setSecureProposal(
                c.snapshot?.proposals?.find((x) => x.id === id)
              )
            }
            onStop={() => void c.stop()}
            onRetry={() => void c.retryTurn()}
            onLoadOlder={() => void c.loadOlder()}
            loadingOlder={c.loadingOlder}
          />
        )}
        <AdminAiComposer
          value={c.draft}
          onChange={c.setDraft}
          onSend={() => void c.send()}
          onStop={() => void c.stop()}
          busy={Boolean(c.inFlightIntents.send)}
          canStop={Boolean(activeTurn?.canCancel)}
          disabled={
            !c.snapshot || c.snapshot.conversation.status === 'Archived'
          }
        />
      </div>
      <AdminAiSecureInputOverlay
        open={Boolean(c.secureProposal)}
        kind={c.secureProposal?.secureInputKind ?? 'Password'}
        busy={Boolean(c.proposalBusy)}
        onClose={() => c.setSecureProposal(undefined)}
        onSubmit={c.submitSecure}
      />
    </section>
  );
}
