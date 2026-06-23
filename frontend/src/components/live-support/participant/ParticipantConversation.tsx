'use client';

import type { LiveSupportMessage } from '@/services/live-support-service';
import { AIPendingActionCard } from './AIPendingActionCard';
import { AIHandoffConfirmation } from './AIHandoffConfirmation';
import { AIGuestVerification } from './AIGuestVerification';
import { AISecureRegistrationForm } from './AISecureRegistrationForm';

export interface ParticipantConversationProps {
  conversationId: string;
  messages: LiveSupportMessage[];
  isAiTyping?: boolean;
  activeAction?: {
    id: string;
    actionKey: string;
    safeProposalJson: string;
    status: string;
    expiresAt: string;
  } | null;
  activeVerification?: any | null;
  onConfirmAction: (proposalId: string) => Promise<void>;
  onCancelAction: (proposalId: string) => Promise<void>;
  onConfirmHandoff: () => Promise<void>;
  onCancelHandoff: () => Promise<void>;
  onVerificationSuccess: () => void;
  onRegistrationSuccess: () => void;
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
  onRegistrationSuccess
}: ParticipantConversationProps) {
  return (
    <div role="log" aria-live="polite" aria-relevant="additions" className="min-h-0 flex-1 space-y-2 overflow-y-auto pb-3">
      {messages.map((message) => (
        <article
          key={message.id}
          aria-label={`${message.senderType}، ${new Date(message.sentAt).toLocaleTimeString('ar-EG')}`}
          className={`max-w-[85%] break-words rounded-2xl px-3 py-2 text-sm ${
            ['Student', 'Guest'].includes(message.senderType)
              ? 'mr-auto bg-cyan-700 text-white'
              : 'ml-auto bg-slate-100 text-slate-800'
          }`}
        >
          {message.content}
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
