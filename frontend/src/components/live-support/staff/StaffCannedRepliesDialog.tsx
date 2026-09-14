'use client';

import { Plus, Save, Trash2, X } from 'lucide-react';
import type { LiveSupportCannedReply } from '@/services/live-support-service';
import { createClientId } from '@/lib/client-id';

interface StaffCannedRepliesDialogProps {
  open: boolean;
  replies: LiveSupportCannedReply[];
  saving: boolean;
  error?: string;
  onClose: () => void;
  onChange: (replies: LiveSupportCannedReply[]) => void;
  onSave: () => void;
}

const MAX_CANNED_REPLIES = 300;

const createReply = (): LiveSupportCannedReply => ({
  id: createClientId(),
  title: '',
  content: '',
  sendImmediately: false,
});

export function StaffCannedRepliesDialog({ open, replies, saving, error, onClose, onChange, onSave }: StaffCannedRepliesDialogProps) {
  if (!open) return null;

  function updateReply(id: string, change: Partial<LiveSupportCannedReply>) {
    onChange(replies.map((reply) => reply.id === id ? { ...reply, ...change } : reply));
  }

  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-[color-mix(in_srgb,var(--admin-text)_45%,transparent)] p-4" role="presentation">
      <section role="dialog" aria-modal="true" aria-labelledby="staff-replies-title" className="max-h-[min(760px,calc(100vh-2rem))] w-full max-w-3xl overflow-y-auto rounded-2xl bg-[var(--admin-card)] shadow-xl">
        <header className="sticky top-0 z-10 flex items-start justify-between gap-4 border-b border-[var(--admin-border)] bg-[var(--admin-card)] p-5">
          <div>
            <h2 id="staff-replies-title" className="font-bold text-[var(--admin-text)]">ردودي الجاهزة</h2>
            <p className="mt-1 text-sm text-[var(--admin-muted)]">هذه الردود تخص حسابك فقط. اكتب <bdi dir="ltr" className="rounded bg-[var(--admin-card-soft)] px-1 font-mono text-xs text-[var(--admin-text)]">{'{{اسم الموظف}}'}</bdi> لعرض اسمك تلقائيًا عند استخدام الرد.</p>
          </div>
          <button type="button" onClick={onClose} disabled={saving} aria-label="إغلاق إعدادات الردود" className="grid size-11 place-items-center rounded-lg text-[var(--admin-muted)] hover:bg-[var(--admin-hover)] disabled:opacity-50"><X size={20}/></button>
        </header>

        <div className="space-y-3 p-5">
          {error && <p role="alert" className="rounded-xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] px-3 py-2 text-sm font-medium text-[var(--admin-danger)]">{error}</p>}
          {replies.length === 0 && <p className="rounded-xl bg-[var(--admin-card-soft)] p-4 text-sm text-[var(--admin-muted)]">أضف رسالة جاهزة لتظهر فوق مربع الرد أثناء المحادثة.</p>}
          {replies.map((reply) => (
            <div key={reply.id} className="grid gap-3 rounded-xl bg-[var(--admin-card-soft)] p-3 lg:grid-cols-[180px_minmax(0,1fr)_auto]">
              <label className="grid gap-1 text-sm font-semibold text-[var(--admin-text)]">عنوان الزر
                <input value={reply.title} onChange={(event) => updateReply(reply.id, { title: event.target.value })} maxLength={80} placeholder="مثل: ترحيب" className="h-11 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm font-normal text-[var(--admin-text)] outline-none focus-visible:border-[var(--admin-primary)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary-15)]" />
              </label>
              <label className="grid gap-1 text-sm font-semibold text-[var(--admin-text)]">نص الرسالة
                <textarea value={reply.content} onChange={(event) => updateReply(reply.id, { content: event.target.value })} maxLength={4000} rows={3} placeholder="اكتب الرسالة أو أضف المتغير {{اسم الموظف}}" className="resize-y rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-3 text-sm font-normal text-[var(--admin-text)] outline-none focus-visible:border-[var(--admin-primary)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary-15)]" />
              </label>
              <div className="flex items-end gap-2 lg:flex-col lg:items-stretch lg:justify-end">
                <label className="flex min-h-11 items-center gap-2 text-sm font-medium text-[var(--admin-text)]"><input type="checkbox" checked={reply.sendImmediately} onChange={(event) => updateReply(reply.id, { sendImmediately: event.target.checked })} className="size-4 accent-[var(--admin-primary)]"/>إرسال مباشر</label>
                <button type="button" onClick={() => onChange(replies.filter((item) => item.id !== reply.id))} aria-label={`حذف ${reply.title || 'الرد'}`} className="grid size-11 place-items-center rounded-lg text-[var(--admin-danger)] hover:bg-[var(--admin-danger-10)]"><Trash2 size={17}/></button>
              </div>
            </div>
          ))}
          <button type="button" disabled={replies.length >= MAX_CANNED_REPLIES || saving} onClick={() => onChange([...replies, createReply()])} className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-[var(--admin-border)] px-4 text-sm font-bold text-[var(--admin-primary)] hover:bg-[var(--admin-hover)] disabled:opacity-50"><Plus size={17}/>إضافة رد</button>
        </div>

        <footer className="sticky bottom-0 flex items-center justify-between gap-3 border-t border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
          <p className="text-xs text-[var(--admin-muted)]">الحد الأقصى {MAX_CANNED_REPLIES} ردًا لكل حساب.</p>
          <div className="flex gap-2"><button type="button" onClick={onClose} disabled={saving} className="min-h-11 rounded-xl px-4 text-sm font-semibold text-[var(--admin-muted)] hover:bg-[var(--admin-hover)] disabled:opacity-50">إلغاء</button><button type="button" onClick={onSave} disabled={saving} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-bold text-[var(--admin-primary-contrast)] disabled:opacity-50"><Save size={17}/>{saving ? 'جارٍ الحفظ…' : 'حفظ ردودي'}</button></div>
        </footer>
      </section>
    </div>
  );
}
