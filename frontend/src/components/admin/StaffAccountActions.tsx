'use client';

import { useId, useRef, useState } from 'react';
import toast from 'react-hot-toast';
import { AdminModal, ConfirmModal } from '@/components/ui/admin-modal';
import NeumorphButton from '@/components/ui/neumorph-button';
import { adminService, type AdminUserListDto } from '@/services/admin-service';
import { useAuthStore } from '@/stores/auth-store';

export function ResetAdminPasswordButton({ user }: { user: AdminUserListDto }) {
  const actor = useAuthStore((state) => state.user);
  const [open, setOpen] = useState(false);
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [busy, setBusy] = useState(false);
  const submitting = useRef(false);
  const passwordId = useId();
  const confirmationId = useId();
  if (!actor?.roles.includes('Admin')) return null;

  const close = () => {
    if (submitting.current) return;
    setOpen(false);
    setPassword('');
    setConfirmation('');
  };
  const savePassword = async () => {
    if (submitting.current) return;
    if (password !== confirmation) { toast.error('كلمتا المرور غير متطابقتين.'); return; }
    if (password.trim().length === 0 || password.length < 8 || new TextEncoder().encode(password).length > 72) {
      toast.error('اكتب كلمة مرور من 8 أحرف على الأقل، وبحد أقصى 72 بايت.'); return;
    }
    submitting.current = true;
    setBusy(true);
    try {
      await adminService.resetAdminPassword(user.id, password);
      toast.success('تم تغيير كلمة المرور. يلزم تسجيل الدخول مجددًا للحساب المعدّل.');
      setOpen(false);
      setPassword('');
      setConfirmation('');
    } finally { submitting.current = false; setBusy(false); }
  };

  return <span onClick={(event) => event.stopPropagation()}>
    <NeumorphButton type="button" intent="primary" onClick={() => setOpen(true)}>تغيير كلمة المرور</NeumorphButton>
    <AdminModal open={open} onClose={close} title={`تغيير كلمة مرور ${user.fullName}`}>
      <form className="space-y-4" onSubmit={(event) => { event.preventDefault(); void savePassword().catch(() => toast.error('تعذر تغيير كلمة المرور. حاول مرة أخرى.')); }}>
        <p className="text-sm text-[var(--admin-muted)]">سيتم إلغاء الجلسات القديمة لهذا الحساب. كلمة المرور 8 أحرف على الأقل.</p>
        <label className="block" htmlFor={passwordId}>كلمة المرور الجديدة</label>
        <input id={passwordId} type="password" autoComplete="new-password" required minLength={8} value={password} disabled={busy} onChange={(event) => setPassword(event.target.value)} className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-3" />
        <label className="block" htmlFor={confirmationId}>تأكيد كلمة المرور</label>
        <input id={confirmationId} type="password" autoComplete="new-password" required value={confirmation} disabled={busy} onChange={(event) => setConfirmation(event.target.value)} className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-3" />
        <NeumorphButton type="submit" intent="primary" loading={busy} disabled={busy}>حفظ كلمة المرور</NeumorphButton>
      </form>
    </AdminModal>
  </span>;
}

export function ArchiveStaffButton({ user, onArchived }: { user: AdminUserListDto; onArchived: () => Promise<void> }) {
  const actor = useAuthStore((state) => state.user);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const submitting = useRef(false);
  if (!actor?.roles.includes('Admin') || actor.id === user.id || user.roles.includes('Admin')) return null;
  const archive = async () => {
    if (submitting.current) return;
    submitting.current = true;
    setBusy(true);
    try {
      await adminService.archiveStaff(user.id);
      setOpen(false);
      toast.success('تمت أرشفة الموظف مع الاحتفاظ ببياناته.');
      await onArchived();
    } finally { submitting.current = false; setBusy(false); }
  };
  return <span onClick={(event) => event.stopPropagation()}>
    <NeumorphButton type="button" intent="danger" onClick={() => setOpen(true)}>أرشفة</NeumorphButton>
    <ConfirmModal open={open} onClose={() => { if (!submitting.current) setOpen(false); }} title={`أرشفة ${user.fullName}؟`}
      description="سيختفي من قائمة الإدارة ويتوقف دخوله. ستبقى بياناته وجميع سجلاته في قاعدة البيانات دون حذف."
      confirmLabel="أرشفة الموظف" variant="danger" loading={busy}
      onConfirm={() => { void archive().catch(() => toast.error('تعذرت أرشفة الموظف. حاول مرة أخرى.')); }} />
  </span>;
}
