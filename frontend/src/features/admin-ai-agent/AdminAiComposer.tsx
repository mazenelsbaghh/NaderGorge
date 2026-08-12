'use client';
import { useEffect, useRef, useState } from 'react';
import { Send, Square } from 'lucide-react';

export function AdminAiComposer({
  value,
  onChange,
  onSend,
  onStop,
  busy,
  canStop,
  disabled,
}: {
  value: string;
  onChange: (v: string) => void;
  onSend: () => void;
  onStop: () => void;
  busy: boolean;
  canStop: boolean;
  disabled?: boolean;
}) {
  const ref = useRef<HTMLTextAreaElement>(null);
  const [composing, setComposing] = useState(false);
  useEffect(() => {
    const el = ref.current;
    if (el) {
      el.style.height = '0';
      el.style.height = `${Math.min(el.scrollHeight, 180)}px`;
    }
  }, [value]);
  return (
    <form
      className="sticky bottom-0 border-t border-[var(--admin-border)] bg-[var(--admin-card)] p-3 pb-[max(.75rem,env(safe-area-inset-bottom))] sm:p-4"
      onSubmit={(e) => {
        e.preventDefault();
        if (!busy && value.trim()) onSend();
      }}
    >
      <label htmlFor="admin-ai-message" className="sr-only">
        اكتب سؤالك لوكيل الإدارة
      </label>
      <div className="flex items-end gap-2">
        <textarea
          ref={ref}
          id="admin-ai-message"
          value={value}
          disabled={disabled}
          rows={1}
          maxLength={8000}
          onChange={(e) => onChange(e.target.value)}
          onCompositionStart={() => setComposing(true)}
          onCompositionEnd={() => setComposing(false)}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && !e.shiftKey && !composing) {
              e.preventDefault();
              if (!busy && value.trim()) onSend();
            }
          }}
          placeholder="اسأل عن أي بيانات مسموح بها…"
          className="max-h-44 min-h-12 min-w-0 flex-1 resize-none rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-base outline-none focus:ring-2 focus:ring-[var(--admin-primary)]"
        />
        {canStop ? (
          <button
            type="button"
            onClick={onStop}
            className="min-h-12 rounded-xl border border-[var(--admin-danger)] px-4 font-black text-[var(--admin-danger)]"
          >
            <Square className="h-5 w-5" />
            <span className="sr-only">إيقاف</span>
          </button>
        ) : (
          <button
            type="submit"
            disabled={disabled || busy || !value.trim()}
            className="min-h-12 rounded-xl bg-[var(--admin-primary)] px-4 font-black text-[var(--admin-primary-contrast)] disabled:opacity-50"
          >
            <Send className="h-5 w-5" />
            <span className="sr-only">إرسال</span>
          </button>
        )}
      </div>
      <p className="mt-2 text-xs text-[var(--admin-muted)]">
        لن يُنفذ أي تعديل دون مراجعتك وتأكيدك الصريح.
      </p>
    </form>
  );
}
