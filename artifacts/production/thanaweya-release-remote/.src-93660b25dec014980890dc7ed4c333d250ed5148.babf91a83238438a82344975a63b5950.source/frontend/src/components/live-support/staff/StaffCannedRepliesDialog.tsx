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
    <div className="fixed inset-0 z-50 grid place-items-center bg-slate-950/45 p-4" role="presentation">
      <section role="dialog" aria-modal="true" aria-labelledby="staff-replies-title" className="max-h-[min(760px,calc(100vh-2rem))] w-full max-w-3xl overflow-y-auto rounded-2xl bg-white shadow-xl">
        <header className="sticky top-0 z-10 flex items-start justify-between gap-4 border-b border-slate-200 bg-white p-5">
          <div>
            <h2 id="staff-replies-title" className="font-bold text-slate-900">ردودي الجاهزة</h2>
            <p className="mt-1 text-sm text-slate-600">هذه الردود تخص حسابك فقط. اكتب <bdi dir="ltr" className="rounded bg-slate-100 px-1 font-mono text-xs text-slate-800">{'{{اسم الموظف}}'}</bdi> لعرض اسمك تلقائيًا عند استخدام الرد.</p>
          </div>
          <button type="button" onClick={onClose} disabled={saving} aria-label="إغلاق إعدادات الردود" className="grid size-10 place-items-center rounded-lg text-slate-600 hover:bg-slate-100 disabled:opacity-50"><X size={20}/></button>
        </header>

        <div className="space-y-3 p-5">
          {error && <p role="alert" className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm font-medium text-red-800">{error}</p>}
          {replies.length === 0 && <p className="rounded-xl bg-slate-50 p-4 text-sm text-slate-600">أضف رسالة جاهزة لتظهر فوق مربع الرد أثناء المحادثة.</p>}
          {replies.map((reply) => (
            <div key={reply.id} className="grid gap-3 rounded-xl bg-slate-50 p-3 lg:grid-cols-[180px_minmax(0,1fr)_auto]">
              <label className="grid gap-1 text-sm font-semibold text-slate-800">عنوان الزر
                <input value={reply.title} onChange={(event) => updateReply(reply.id, { title: event.target.value })} maxLength={80} placeholder="مثل: ترحيب" className="h-10 rounded-lg border border-slate-300 bg-white px-3 text-sm font-normal outline-none focus-visible:border-cyan-700 focus-visible:ring-2 focus-visible:ring-cyan-700/20" />
              </label>
              <label className="grid gap-1 text-sm font-semibold text-slate-800">نص الرسالة
                <textarea value={reply.content} onChange={(event) => updateReply(reply.id, { content: event.target.value })} maxLength={4000} rows={3} placeholder="اكتب الرسالة أو أضف المتغير {{اسم الموظف}}" className="resize-y rounded-lg border border-slate-300 bg-white p-3 text-sm font-normal outline-none focus-visible:border-cyan-700 focus-visible:ring-2 focus-visible:ring-cyan-700/20" />
              </label>
              <div className="flex items-end gap-2 lg:flex-col lg:items-stretch lg:justify-end">
                <label className="flex min-h-10 items-center gap-2 text-sm font-medium text-slate-700"><input type="checkbox" checked={reply.sendImmediately} onChange={(event) => updateReply(reply.id, { sendImmediately: event.target.checked })} className="size-4 accent-cyan-700"/>إرسال مباشر</label>
                <button type="button" onClick={() => onChange(replies.filter((item) => item.id !== reply.id))} aria-label={`حذف ${reply.title || 'الرد'}`} className="grid size-10 place-items-center rounded-lg text-red-700 hover:bg-red-100"><Trash2 size={17}/></button>
              </div>
            </div>
          ))}
          <button type="button" disabled={replies.length >= 30 || saving} onClick={() => onChange([...replies, createReply()])} className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-cyan-200 px-4 text-sm font-bold text-cyan-800 hover:bg-cyan-50 disabled:opacity-50"><Plus size={17}/>إضافة رد</button>
        </div>

        <footer className="sticky bottom-0 flex items-center justify-between gap-3 border-t border-slate-200 bg-white p-4">
          <p className="text-xs text-slate-500">الحد الأقصى 30 ردًا لكل حساب.</p>
          <div className="flex gap-2"><button type="button" onClick={onClose} disabled={saving} className="min-h-11 rounded-xl px-4 text-sm font-semibold text-slate-700 hover:bg-slate-100 disabled:opacity-50">إلغاء</button><button type="button" onClick={onSave} disabled={saving} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-slate-900 px-4 text-sm font-bold text-white disabled:opacity-50"><Save size={17}/>{saving ? 'جارٍ الحفظ…' : 'حفظ ردودي'}</button></div>
        </footer>
      </section>
    </div>
  );
}
