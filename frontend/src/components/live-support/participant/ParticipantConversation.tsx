'use client';

import { useEffect, useRef } from 'react';

import { formatCairoDateTime } from '@/lib/cairo-time';
import type { LiveSupportAIPendingDecision, LiveSupportAIVerificationSession, LiveSupportMessage } from '@/services/live-support-service';
import { AIPendingActionCard } from './AIPendingActionCard';
import { AIHandoffConfirmation } from './AIHandoffConfirmation';
import { AIGuestVerification } from './AIGuestVerification';
import { AISecureRegistrationForm } from './AISecureRegistrationForm';
import { LiveSupportMessageContent, LiveSupportMessageMeta } from '@/components/live-support/LiveSupportMessageContent';
import { LiveSupportMessageActions } from '@/components/live-support/LiveSupportMessageActions';

export interface ParticipantConversationProps {
  conversationId: string;
  messages: LiveSupportMessage[];
  isAiTyping?: boolean;
  activeAction?: LiveSupportAIPendingDecision | null;
  activeVerification?: LiveSupportAIVerificationSession | null;
  onConfirmAction: (proposalId: string) => Promise<void>;
  onCancelAction: (proposalId: string) => Promise<void>;
  onConfirmHandoff: () => Promise<void>;
  onCancelHandoff: () => Promise<void>;
  onVerificationSuccess: () => void;
  onRegistrationSuccess: () => void;
  onEditMessage: (messageId: string, content: string) => Promise<void>;
  onDeleteMessage: (messageId: string) => Promise<void>;
}

export function ParticipantConversation({
  conversationId,
  messages,
  isAiTyping,
  activeAction,
  activeVerification,
  onConfirmAction,
  onCancelAction,
  onConfirmHandoff,
  onCancelHandoff,
  onVerificationSuccess,
  onRegistrationSuccess,
  onEditMessage,
  onDeleteMessage
}: ParticipantConversationProps) {
  const viewportRef = useRef<HTMLDivElement>(null);
  const shouldStickToBottom = useRef(true);

  useEffect(() => { shouldStickToBottom.current = true; }, [conversationId]);
  useEffect(() => {
    if (!shouldStickToBottom.current) return;
    const frame = requestAnimationFrame(() => {
      const viewport = viewportRef.current;
      if (viewport) viewport.scrollTop = viewport.scrollHeight;
    });
    return () => cancelAnimationFrame(frame);
  }, [conversationId, messages.length, isAiTyping, activeAction, activeVerification]);

  return (
    <div ref={viewportRef} onScroll={(event) => { const viewport = event.currentTarget; shouldStickToBottom.current = viewport.scrollHeight - viewport.scrollTop - viewport.clientHeight < 80; }} role="log" aria-live="polite" aria-relevant="additions" className="min-h-0 flex-1 touch-pan-y space-y-2 overflow-y-auto overscroll-contain pb-3 [-webkit-overflow-scrolling:touch] [scrollbar-gutter:stable]">
      {messages.map((message) => (
        <article
          dir="auto"
          key={message.id}
          aria-label={`${message.senderType}، ${formatCairoDateTime(message.sentAt, { hour: '2-digit', minute: '2-digit' })}`}
          className={`max-w-[85%] break-words [overflow-wrap:anywhere] rounded-2xl px-3 py-2 text-sm ${
            ['Student', 'Guest'].includes(message.senderType)
              ? 'mr-auto bg-cyan-700 text-white'
              : 'ml-auto bg-slate-100 text-slate-800'
          }`}
        >
          {['Staff', 'Admin'].includes(message.senderType) && message.senderDisplayName ? (
            <p className="mb-1 text-xs font-bold text-cyan-800">{message.senderDisplayName} · فريق الدعم</p>
          ) : null}
          <LiveSupportMessageContent message={message} audience="participant"/>
          {['Student', 'Guest'].includes(message.senderType) ? <LiveSupportMessageActions message={message} onEdit={onEditMessage} onDelete={onDeleteMessage}/> : null}
          <LiveSupportMessageMeta message={message} audience="participant"/>
        </article>
      ))}

      {activeAction && activeAction.status === 'PendingConfirmation' && (
        <div className="w-[90%] ml-auto">
          {activeAction.actionKey === 'system.handoff' && (
            <AIHandoffConfirmation
              action={activeAction}
              onConfirm={onConfirmHandoff}
              onCancel={onCancelHandoff}
            />
          )}
          {activeAction.actionKey === 'system.verification' && (
            <AIGuestVerification
              conversationId={conversationId}
              initialSession={activeVerification}
              onVerified={onVerificationSuccess}
            />
          )}
          {activeAction.actionKey === 'system.registration' && (
            <AISecureRegistrationForm
              conversationId={conversationId}
              decisionId={activeAction.id}
              onSuccess={onRegistrationSuccess}
            />
          )}
          {!['system.handoff', 'system.verification', 'system.registration'].includes(activeAction.actionKey) && (
            <AIPendingActionCard
              action={activeAction}
              onConfirm={onConfirmAction}
              onCancel={onCancelAction}
            />
          )}
        </div>
      )}

      {!activeAction && activeVerification && ['AwaitingLookup', 'Challenging'].includes(activeVerification.status) && (
        <div className="ml-auto w-[90%]">
          <AIGuestVerification conversationId={conversationId} initialSession={activeVerification} onVerified={onVerificationSuccess}/>
        </div>
      )}

      {isAiTyping && (
        <article
          aria-label="المساعد الذكي يكتب"
          className="ml-auto max-w-[85%] rounded-2xl bg-slate-100 px-4 py-3 text-slate-800"
        >
          <div className="flex items-center gap-1.5 py-0.5">
            <span className="h-2 w-2 animate-bounce rounded-full bg-slate-400 [animation-delay:-0.3s]"></span>
            <span className="h-2 w-2 animate-bounce rounded-full bg-slate-400 [animation-delay:-0.15s]"></span>
            <span className="h-2 w-2 animate-bounce rounded-full bg-slate-400"></span>
          </div>
        </article>
      )}
    </div>
  );
}
