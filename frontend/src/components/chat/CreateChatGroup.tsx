'use client';

import { useState } from 'react';
import { AdminModal } from '@/components/ui/admin-modal';
import NeumorphButton from '@/components/ui/neumorph-button';
import { adminService, type AdminUserListDto } from '@/services/admin-service';
import { chatService } from '@/services/chat-service';
import apiClient from '@/services/api-client';

export function CreateChatGroup({ onCreated, roomId }: { onCreated: (id: string) => void; roomId?: string }) {
  const [open, setOpen] = useState(false);
  const [users, setUsers] = useState<AdminUserListDto[]>([]);
  const [selected, setSelected] = useState<string[]>([]);
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const start = async () => {
    setOpen(true); setBusy(true); setError(''); setSelected([]); setName('');
    try {
      setUsers((await adminService.listAllUsers({})).filter(user => user.status === 'Active' && !user.roles.includes('Student')));
      if (roomId) setSelected((await apiClient.get<string[]>(`/chat/rooms/${roomId}/members`)).data);
    }
    catch { setError('تعذر تحميل الموظفين. أغلق النافذة وحاول مرة أخرى.'); }
    finally { setBusy(false); }
  };
  const create = async () => {
    if (busy) return;
    setBusy(true); setError('');
    try {
      const id = roomId ?? await chatService.createRoom({ name: name.trim(), type: 'Group', participantIds: selected });
      if (roomId) await apiClient.put(`/chat/rooms/${roomId}/members`, selected);
      setOpen(false); onCreated(id);
    }
    catch { setError('تعذر إنشاء المجموعة. راجع الأعضاء وحاول مرة أخرى.'); }
    finally { setBusy(false); }
  };
  return <>
    <NeumorphButton onClick={() => void start()}>{roomId ? 'إدارة أعضاء المجموعة' : 'إنشاء مجموعة داخلية'}</NeumorphButton>
    <AdminModal open={open} onClose={() => { if (!busy) setOpen(false); }} title={roomId ? 'إدارة أعضاء المجموعة' : 'إنشاء مجموعة داخلية'}>
      <form className="space-y-4" onSubmit={event => { event.preventDefault(); void create(); }}>
        {!roomId && <label className="block">اسم المجموعة<input required maxLength={100} value={name} onChange={event => setName(event.target.value)} className="block w-full rounded-lg border p-3" /></label>}
        <p>اختر الأعضاء المسموح لهم بالمحادثة. ستنضم أنت تلقائيًا.</p>
        <div className="max-h-64 overflow-auto space-y-2">{users.map(user => <label key={user.id} className="flex gap-2 p-2"><input type="checkbox" checked={selected.includes(user.id)} onChange={event => setSelected(current => event.target.checked ? [...current, user.id] : current.filter(id => id !== user.id))} />{user.fullName}</label>)}</div>
        {error && <p role="alert">{error}</p>}
        <NeumorphButton type="submit" disabled={busy || (!roomId && !name.trim()) || !selected.length} loading={busy}>{roomId ? 'حفظ الأعضاء' : 'إنشاء المجموعة'}</NeumorphButton>
      </form>
    </AdminModal>
  </>;
}
