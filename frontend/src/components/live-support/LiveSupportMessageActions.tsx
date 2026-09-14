'use client';

import { useState } from 'react';
import { Pencil, Save, Trash2, X } from 'lucide-react';
import type { LiveSupportMessage } from '@/services/live-support-service';

interface LiveSupportMessageActionsProps {
  message: LiveSupportMessage;
  onEdit: (messageId: string, content: string) => Promise<void>;
  onDelete: (messageId: string) => Promise<void>;
}

export function LiveSupportMessageActions({ message, onEdit, onDelete }: LiveSupportMessageActionsProps) {
  const [editing, setEditing] = useState(false);
  const [content, setContent] = useState(message.content);
  const [saving, setSaving] = useState(false);
  const [actionError, setActionError] = useState('');
  if (message.deletedAt) return null;

  async function save() {
    const nextContent = content.trim();
    if (!nextContent || nextContent === message.content) { setEditing(false); return; }
    setSaving(true);
    setActionError('');
    try { await onEdit(message.id, nextContent); setEditing(false); }
    catch { setActionError('تعذر حفظ التعديل.'); }
    finally { setSaving(false); }
  }

  async function remove() {
    if (!confirm('هل تريد حذف هذه الرسالة؟')) return;
    setSaving(true);
    setActionError('');
    try { await onDelete(message.id); }
    catch { setActionError('تعذر حذف الرسالة.'); }
    finally { setSaving(false); }
  }

  if (editing) return <div className="mt-2" dir="rtl">
    <div className="flex gap-1">
    <input value={content} maxLength={4000} disabled={saving} onChange={(event) => setContent(event.target.value)} onKeyDown={(event) => { if (event.key === 'Enter') { event.preventDefault(); void save(); } }} className="h-9 min-w-0 flex-1 rounded-lg border border-current/20 bg-white px-2 text-slate-900 outline-none" autoFocus/>
    <button type="button" disabled={saving || !content.trim()} onClick={() => void save()} aria-label="حفظ التعديل" className="grid size-9 place-items-center rounded-lg bg-white/20"><Save size={15}/></button>
    <button type="button" disabled={saving} onClick={() => { setContent(message.content); setEditing(false); }} aria-label="إلغاء التعديل" className="grid size-9 place-items-center rounded-lg bg-white/20"><X size={15}/></button>
    </div>
    {actionError ? <p role="alert" className="mt-1 text-xs">{actionError}</p> : null}
  </div>;

  return <div className="mt-1" dir="rtl">
    <div className="flex justify-end gap-1 opacity-70 transition-opacity hover:opacity-100">
    {message.type === 'Text' && !message.attachmentId ? <button type="button" disabled={saving} onClick={() => setEditing(true)} aria-label="تعديل الرسالة" className="grid size-7 place-items-center rounded-md hover:bg-black/10"><Pencil size={13}/></button> : null}
    <button type="button" disabled={saving} onClick={() => void remove()} aria-label="حذف الرسالة" className="grid size-7 place-items-center rounded-md hover:bg-black/10"><Trash2 size={13}/></button>
    </div>
    {actionError ? <p role="alert" className="text-xs">{actionError}</p> : null}
  </div>;
}
