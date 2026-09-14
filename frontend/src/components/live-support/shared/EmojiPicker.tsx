'use client';

import { createPortal } from 'react-dom';
import { useCallback, useEffect, useId, useRef, useState, type RefObject } from 'react';
import { Smile, X } from 'lucide-react';
import { cn } from '@/lib/utils';

const EMOJIS = [
  '😀', '😄', '😂', '🙂', '😉', '😊',
  '😍', '🥳', '😅', '🤔', '😢', '😡',
  '👍', '👎', '👏', '🙏', '🤝', '🙌',
  '❤️', '🤍', '💙', '✨', '🎉', '🔥',
  '💡', '✅', '❌', '📌', '💬', '👋',
];

type EmojiPickerTone = 'participant' | 'staff';

type EmojiPickerProps = {
  onSelect: (emoji: string) => void;
  disabled?: boolean;
  tone?: EmojiPickerTone;
};

type PopoverPosition = { top: number; left: number };

type ToneStyle = {
  button: string;
  panel: string;
  title: string;
  hint: string;
  emoji: string;
  close: string;
};

type PickerRefs = {
  rootRef: RefObject<HTMLDivElement | null>;
  triggerRef: RefObject<HTMLButtonElement | null>;
  panelRef: RefObject<HTMLDivElement | null>;
};

const toneStyles: Record<EmojiPickerTone, ToneStyle> = {
  participant: {
    button: 'border-slate-200 bg-white text-slate-600 hover:bg-cyan-50 hover:text-cyan-700 focus-visible:ring-cyan-700/20',
    panel: 'border-slate-200 bg-white text-slate-800',
    title: 'text-slate-900',
    hint: 'text-slate-500',
    emoji: 'hover:bg-cyan-50 focus-visible:bg-cyan-50',
    close: 'text-slate-500 hover:bg-slate-100 hover:text-slate-800 focus-visible:ring-cyan-700/30',
  },
  staff: {
    button: 'border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:bg-[var(--admin-hover)] hover:text-[var(--admin-primary)] focus-visible:ring-[var(--admin-primary-15)]',
    panel: 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-text)]',
    title: 'text-[var(--admin-text)]',
    hint: 'text-[var(--admin-muted)]',
    emoji: 'hover:bg-[var(--admin-hover)] focus-visible:bg-[var(--admin-hover)]',
    close: 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)] hover:text-[var(--admin-text)] focus-visible:ring-[var(--admin-primary-15)]',
  },
};

export function insertEmojiAtCursor(
  input: HTMLInputElement | HTMLTextAreaElement | null,
  draftText: string,
  emojiText: string,
) {
  const selectionStart = input?.selectionStart ?? draftText.length;
  const selectionEnd = input?.selectionEnd ?? selectionStart;
  const nextDraft = `${draftText.slice(0, selectionStart)}${emojiText}${draftText.slice(selectionEnd)}`;

  return { draftText: nextDraft, cursorPosition: selectionStart + emojiText.length };
}

function calculatePopoverPosition(triggerRect: DOMRect, panelRect: DOMRect): PopoverPosition {
  const edge = 12;
  const left = Math.min(Math.max(edge, triggerRect.right - panelRect.width), window.innerWidth - panelRect.width - edge);
  const spaceAbove = triggerRect.top - panelRect.height - 8;
  const top = spaceAbove >= edge
    ? spaceAbove
    : Math.min(triggerRect.bottom + 8, window.innerHeight - panelRect.height - edge);

  return { top: Math.max(edge, top), left: Math.max(edge, left) };
}

function subscribeToViewport(updatePosition: () => void) {
  const positionFrame = requestAnimationFrame(updatePosition);
  window.addEventListener('resize', updatePosition);
  window.addEventListener('scroll', updatePosition, true);

  return () => {
    cancelAnimationFrame(positionFrame);
    window.removeEventListener('resize', updatePosition);
    window.removeEventListener('scroll', updatePosition, true);
  };
}

function useEmojiPickerPosition(isOpen: boolean, refs: Pick<PickerRefs, 'triggerRef' | 'panelRef'>) {
  const [popoverPosition, setPopoverPosition] = useState<PopoverPosition>();

  useEffect(() => {
    if (!isOpen) {
      setPopoverPosition(undefined);
      return;
    }

    const updatePosition = () => {
      const triggerRect = refs.triggerRef.current?.getBoundingClientRect();
      const panelRect = refs.panelRef.current?.getBoundingClientRect();
      if (triggerRect && panelRect) setPopoverPosition(calculatePopoverPosition(triggerRect, panelRect));
    };

    return subscribeToViewport(updatePosition);
  }, [isOpen, refs.panelRef, refs.triggerRef]);

  return popoverPosition;
}

function useEmojiPickerDismissal(isOpen: boolean, refs: PickerRefs, closePicker: () => void) {
  useEffect(() => {
    if (!isOpen) return;

    const closeOnOutsidePointer = (event: PointerEvent) => {
      const pointerTarget = event.target;
      if (!(pointerTarget instanceof Node)) return;
      if (refs.rootRef.current?.contains(pointerTarget) || refs.panelRef.current?.contains(pointerTarget)) return;
      closePicker();
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      event.preventDefault();
      closePicker();
      requestAnimationFrame(() => refs.triggerRef.current?.focus());
    };

    document.addEventListener('pointerdown', closeOnOutsidePointer);
    document.addEventListener('keydown', closeOnEscape);
    return () => {
      document.removeEventListener('pointerdown', closeOnOutsidePointer);
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [closePicker, isOpen, refs.panelRef, refs.rootRef, refs.triggerRef]);
}

function EmojiPickerTrigger({ isOpen, disabled, popoverId, toneStyle, onToggle, triggerRef }: {
  isOpen: boolean;
  disabled: boolean;
  popoverId: string;
  toneStyle: ToneStyle;
  onToggle: () => void;
  triggerRef: RefObject<HTMLButtonElement | null>;
}) {
  return <button
    ref={triggerRef}
    type="button"
    disabled={disabled}
    aria-label="اختيار إيموجي"
    aria-expanded={isOpen}
    aria-controls={popoverId}
    aria-haspopup="dialog"
    onClick={onToggle}
    className={cn('grid size-11 place-items-center rounded-xl border transition-colors focus-visible:outline-none focus-visible:ring-2 disabled:cursor-not-allowed disabled:opacity-50', toneStyle.button)}
  >
    <Smile size={18} />
  </button>;
}

function EmojiPickerHeader({ toneStyle, onClose }: { toneStyle: ToneStyle; onClose: () => void }) {
  return <div className="mb-2 flex items-start justify-between gap-3">
    <div>
      <p className={cn('text-sm font-black', toneStyle.title)}>اختر إيموجي</p>
      <p className={cn('mt-0.5 text-[11px]', toneStyle.hint)}>سيُضاف مكان المؤشر في الرسالة</p>
    </div>
    <button
      type="button"
      onClick={onClose}
      aria-label="إغلاق لوحة الإيموجي"
      className={cn('grid size-8 shrink-0 place-items-center rounded-lg transition-colors focus-visible:outline-none focus-visible:ring-2', toneStyle.close)}
    >
      <X size={15} />
    </button>
  </div>;
}

function EmojiGrid({ toneStyle, onSelect }: { toneStyle: ToneStyle; onSelect: (emoji: string) => void }) {
  return <div className="grid grid-cols-6 gap-1" aria-label="الإيموجي المتاحة">
    {EMOJIS.map((emoji) => <button
      key={emoji}
      type="button"
      onClick={() => onSelect(emoji)}
      aria-label={`إضافة ${emoji}`}
      title={emoji}
      className={cn('grid size-10 place-items-center rounded-xl text-xl transition-colors active:scale-95 focus-visible:outline-none focus-visible:ring-2', toneStyle.emoji)}
    >
      {emoji}
    </button>)}
  </div>;
}

function EmojiPickerPanel({ popoverId, popoverPosition, toneStyle, onClose, onSelect, panelRef }: {
  popoverId: string;
  popoverPosition?: PopoverPosition;
  toneStyle: ToneStyle;
  onClose: () => void;
  onSelect: (emoji: string) => void;
  panelRef: RefObject<HTMLDivElement | null>;
}) {
  return <div
    ref={panelRef}
    id={popoverId}
    role="dialog"
    aria-label="اختيار إيموجي"
    style={{
      top: popoverPosition?.top ?? 0,
      left: popoverPosition?.left ?? 0,
      visibility: popoverPosition ? 'visible' : 'hidden',
    }}
    className={cn('fixed z-[var(--z-modal-toolbar)] w-[min(18rem,calc(100vw-1.5rem))] rounded-2xl border p-3 shadow-2xl', toneStyle.panel)}
  >
    <EmojiPickerHeader toneStyle={toneStyle} onClose={onClose} />
    <EmojiGrid toneStyle={toneStyle} onSelect={onSelect} />
  </div>;
}

export function EmojiPicker({ onSelect, disabled = false, tone = 'participant' }: EmojiPickerProps) {
  const [isOpen, setIsOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const popoverId = useId();
  const toneStyle = toneStyles[tone];
  const closePicker = useCallback(() => setIsOpen(false), []);
  const popoverPosition = useEmojiPickerPosition(isOpen, { triggerRef, panelRef });
  useEmojiPickerDismissal(isOpen, { rootRef, triggerRef, panelRef }, closePicker);

  useEffect(() => {
    if (disabled) closePicker();
  }, [closePicker, disabled]);

  const selectEmoji = (emoji: string) => {
    onSelect(emoji);
    closePicker();
  };
  const emojiPanel = isOpen ? <EmojiPickerPanel popoverId={popoverId} popoverPosition={popoverPosition} toneStyle={toneStyle} onClose={closePicker} onSelect={selectEmoji} panelRef={panelRef} /> : null;

  return <div ref={rootRef} className="shrink-0">
    <EmojiPickerTrigger isOpen={isOpen} disabled={disabled} popoverId={popoverId} toneStyle={toneStyle} onToggle={() => setIsOpen((isCurrentlyOpen) => !isCurrentlyOpen)} triggerRef={triggerRef} />
    {typeof document !== 'undefined' && emojiPanel ? createPortal(emojiPanel, document.body) : null}
  </div>;
}
