'use client';
import { useEffect, useRef, useState } from 'react';
export function AdminAiStrongConfirmation({
  phrase,
  expiresAt,
  busy,
  onConfirm,
}: {
  phrase: string;
  expiresAt: string;
  busy: boolean;
  onConfirm: (phrase: string) => void;
}) {
  const [typed, setTyped] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    inputRef.current?.focus();
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, []);
  const remainingSeconds = Math.max(
    0,
    Math.ceil((Date.parse(expiresAt) - now) / 1000)
  );
  const matches = typed === phrase && remainingSeconds > 0;
  return (
    <div className="mt-4 rounded-xl border-2 border-[var(--admin-warning)] p-3">
      <p className="text-sm font-black">
        اكتب العبارة التالية كما هي لتأكيد الإجراء عالي الخطورة:
      </p>
      <code
        className="mt-2 block select-all rounded-lg bg-[var(--admin-bg)] p-3 text-center text-sm [unicode-bidi:isolate]"
        dir="ltr"
      >
        {phrase}
      </code>
      <label
        className="mt-3 block text-xs font-bold"
        htmlFor={`phrase-${phrase.length}`}
      >
        عبارة التأكيد
      </label>
      <input
        ref={inputRef}
        id={`phrase-${phrase.length}`}
        value={typed}
        onChange={(e) => setTyped(e.target.value)}
        autoComplete="off"
        spellCheck={false}
        dir="ltr"
        className="mt-1 min-h-11 w-full rounded-lg border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3"
      />
      <p role="status" className="mt-2 text-xs text-[var(--admin-muted)]">
        {remainingSeconds > 0
          ? `متبقي ${remainingSeconds} ثانية`
          : 'انتهت صلاحية عبارة التأكيد. حدّث المقترح.'}
      </p>
      <button
        disabled={!matches || busy}
        onClick={() => onConfirm(typed)}
        className="mt-3 min-h-11 w-full rounded-lg bg-[var(--admin-danger)] px-4 font-black text-[var(--admin-primary-contrast)] disabled:opacity-50"
      >
        {busy ? 'جارٍ التحقق…' : 'تأكيد وتنفيذ مرة واحدة'}
      </button>
    </div>
  );
}
