"use client";

import { useId, useRef, useState } from "react";
import { Pencil, Loader2 } from "lucide-react";
import { getApiErrorSummary } from "@/lib/api-errors";

interface ContentTitleEditorProps {
  title: string;
  label: string;
  onSave: (title: string) => Promise<void>;
}

export function ContentTitleEditor({ title, label, onSave }: ContentTitleEditorProps) {
  const inputId = useId();
  const buttonRef = useRef<HTMLButtonElement>(null);
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState(title);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function closeEditor() {
    setOpen(false);
    requestAnimationFrame(() => buttonRef.current?.focus());
  }

  async function saveTitle(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const trimmedTitle = draft.trim();
    if (!trimmedTitle || trimmedTitle.length > 200) {
      setError("الاسم مطلوب ولا يزيد عن ٢٠٠ حرف.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await onSave(trimmedTitle);
      closeEditor();
    } catch (saveError: unknown) {
      setError(getApiErrorSummary(saveError, "تعذر حفظ الاسم. حاول مرة أخرى."));
    } finally {
      setSaving(false);
    }
  }

  return <div className="min-w-0" dir="rtl">
    <button ref={buttonRef} type="button" aria-expanded={open} aria-controls={`${inputId}-form`}
      onClick={() => { setDraft(title); setError(null); setOpen(true); }}
      className={`inline-flex min-h-11 items-center gap-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-2 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)] focus-visible:outline-2 focus-visible:outline-[var(--admin-primary)] ${open ? "hidden" : ""}`}>
      <Pencil className="h-4 w-4" aria-hidden="true" /> تعديل {label}
    </button>
    {open && <form id={`${inputId}-form`} onSubmit={saveTitle}
      onKeyDown={(event) => { if (event.key === "Escape" && !saving) { event.preventDefault(); closeEditor(); } }}
      className="flex w-full flex-wrap items-end gap-2" aria-busy={saving}>
      <div className="min-w-0 flex-1 basis-48">
        <label htmlFor={inputId} className="mb-1 block text-sm font-bold text-[var(--admin-text)]">{label}</label>
        <input id={inputId} autoFocus value={draft} onChange={(event) => setDraft(event.target.value)}
          maxLength={200} required disabled={saving} dir="auto" className="admin-input min-h-11 w-full"
          aria-invalid={Boolean(error)} aria-describedby={error ? `${inputId}-error` : undefined} />
      </div>
      <button type="submit" disabled={saving || !draft.trim()} className="admin-btn-primary inline-flex min-h-11 items-center gap-2 disabled:opacity-50">
        {saving && <Loader2 className="h-4 w-4 animate-spin motion-reduce:animate-none" aria-hidden="true" />}
        {saving ? "جارٍ الحفظ..." : "حفظ الاسم"}
      </button>
      <button type="button" disabled={saving} onClick={closeEditor}
        className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] disabled:opacity-50">إلغاء</button>
      {error && <p id={`${inputId}-error`} role="alert" className="w-full text-sm text-[var(--admin-danger)]">{error}</p>}
    </form>}
  </div>;
}
