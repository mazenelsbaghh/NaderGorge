'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Check, Edit2, Plus, RefreshCw, Save, Trash2, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { AdminPageSkeleton, AdminPage, ConfirmDialog } from '@/components/admin';
import { useVideoTypes } from '@/hooks/useVideoTypes';
import { adminService, type VideoTypeDto } from '@/services/admin-service';
import { useAuthStore } from '@/stores/auth-store';

function apiMessage(error: unknown, fallback: string) {
  if (typeof error === 'object' && error !== null && 'response' in error) {
    const response = (error as { response?: { data?: { message?: string } } }).response;
    return response?.data?.message || fallback;
  }
  return fallback;
}

export default function VideoTypesPageClient() {
  const router = useRouter();
  const user = useAuthStore((state) => state.user);
  const { types, loading, error, retry } = useVideoTypes(true);
  const [name, setName] = useState('');
  const [sortOrder, setSortOrder] = useState(50);
  const [saving, setSaving] = useState(false);
  const [editing, setEditing] = useState<VideoTypeDto | null>(null);
  const [editName, setEditName] = useState('');
  const [editOrder, setEditOrder] = useState(0);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<VideoTypeDto | null>(null);

  const isAdmin = user?.roles.includes('Admin') === true;
  useEffect(() => {
    if (user && !isAdmin) router.replace('/admin/unauthorized');
  }, [isAdmin, router, user]);

  if (!user || !isAdmin) return null;

  const createType = async (event: React.FormEvent) => {
    event.preventDefault();
    if (name.trim().length < 2) {
      toast.error('اكتب اسماً من حرفين على الأقل.');
      return;
    }
    try {
      setSaving(true);
      await adminService.createVideoType({ name: name.trim(), sortOrder, isActive: true });
      setName('');
      setSortOrder((current) => current + 10);
      toast.success('تم إنشاء نوع الفيديو.');
      await retry();
    } catch (requestError) {
      toast.error(apiMessage(requestError, 'تعذر إنشاء نوع الفيديو.'));
    } finally {
      setSaving(false);
    }
  };

  const startEdit = (type: VideoTypeDto) => {
    setEditing(type);
    setEditName(type.name);
    setEditOrder(type.sortOrder);
  };

  const saveEdit = async () => {
    if (!editing || editName.trim().length < 2) return;
    try {
      setBusyId(editing.id);
      await adminService.updateVideoType(editing.id, { name: editName.trim(), sortOrder: editOrder });
      setEditing(null);
      toast.success('تم تحديث نوع الفيديو.');
      await retry();
    } catch (requestError) {
      toast.error(apiMessage(requestError, 'تعذر تحديث نوع الفيديو.'));
    } finally {
      setBusyId(null);
    }
  };

  const toggleStatus = async (type: VideoTypeDto) => {
    try {
      setBusyId(type.id);
      await adminService.setVideoTypeStatus(type.id, !type.isActive);
      toast.success(type.isActive ? 'تم تعطيل النوع.' : 'تم تفعيل النوع.');
      await retry();
    } catch (requestError) {
      toast.error(apiMessage(requestError, 'تعذر تحديث حالة النوع.'));
    } finally {
      setBusyId(null);
    }
  };

  const deleteType = async () => {
    if (!deleteTarget) return;
    try {
      setBusyId(deleteTarget.id);
      await adminService.deleteVideoType(deleteTarget.id);
      toast.success('تم حذف نوع الفيديو.');
      setDeleteTarget(null);
      await retry();
    } catch (requestError) {
      toast.error(apiMessage(requestError, 'تعذر حذف نوع الفيديو.'));
    } finally {
      setBusyId(null);
    }
  };

  return (
    <AdminPage
      activePath="/admin/content/video-types"
      sectionLabel="إدارة المحتوى"
      pageTitle="أنواع الفيديو"
      subtitle="أنشئ التصنيفات التي تظهر عند إضافة الفيديو، وعطّل النوع بدلاً من حذفه عندما يكون مستخدماً."
    >
      <div className="space-y-6">
        <form onSubmit={createType} className="flex flex-col gap-4 rounded-xl bg-[var(--admin-card)] p-5 md:flex-row md:items-end">
          <label className="flex-1 space-y-2 text-sm font-bold text-[var(--admin-text)]">
            اسم النوع
            <input
              value={name}
              onChange={(event) => setName(event.target.value)}
              className="admin-input mt-2"
              placeholder="مثال: حل أسئلة"
              maxLength={80}
              required
            />
          </label>
          <label className="w-full space-y-2 text-sm font-bold text-[var(--admin-text)] md:w-40">
            ترتيب العرض
            <input
              type="number"
              value={sortOrder}
              onChange={(event) => setSortOrder(Number(event.target.value))}
              className="admin-input mt-2"
              min={0}
              max={10000}
              required
            />
          </label>
          <button
            type="submit"
            disabled={saving || name.trim().length < 2}
            className="inline-flex h-11 cursor-pointer items-center justify-center gap-2 rounded-lg bg-[var(--admin-primary)] px-5 text-sm font-bold text-white transition-colors duration-200 hover:bg-[var(--admin-primary-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] disabled:cursor-not-allowed disabled:opacity-50"
          >
            <Plus className="h-4 w-4" aria-hidden="true" />
            {saving ? 'جاري الحفظ...' : 'إضافة النوع'}
          </button>
        </form>

        {loading ? (
          <AdminPageSkeleton />
        ) : error ? (
          <div className="flex min-h-52 flex-col items-center justify-center gap-4 rounded-xl bg-[var(--admin-card)] p-8 text-center">
            <p className="text-sm font-bold text-red-600">{error}</p>
            <button type="button" onClick={() => void retry()} className="inline-flex h-11 cursor-pointer items-center gap-2 rounded-lg bg-[var(--admin-primary)] px-5 text-sm font-bold text-white">
              <RefreshCw className="h-4 w-4" /> إعادة المحاولة
            </button>
          </div>
        ) : types.length === 0 ? (
          <div className="rounded-xl bg-[var(--admin-card)] p-10 text-center">
            <h2 className="text-lg font-bold text-[var(--admin-text)]">لا توجد أنواع فيديو</h2>
            <p className="mt-2 text-sm text-[var(--admin-muted)]">أضف النوع الأول من النموذج بالأعلى ليظهر في نماذج الفيديو.</p>
          </div>
        ) : (
          <div className="overflow-x-auto rounded-xl bg-[var(--admin-card)]">
            <table className="w-full min-w-[720px] text-right text-sm">
              <thead className="bg-[var(--admin-card-soft)] text-[var(--admin-muted)]">
                <tr>
                  <th className="px-4 py-3 font-bold">النوع</th>
                  <th className="px-4 py-3 font-bold">الترتيب</th>
                  <th className="px-4 py-3 font-bold">الفيديوهات</th>
                  <th className="px-4 py-3 font-bold">الحالة</th>
                  <th className="px-4 py-3 text-left font-bold">الإجراءات</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[var(--admin-border)]">
                {types.map((type) => {
                  const isEditing = editing?.id === type.id;
                  const isBusy = busyId === type.id;
                  return (
                    <tr key={type.id} className="transition-colors duration-200 hover:bg-[var(--admin-hover)]">
                      <td className="px-4 py-3">
                        {isEditing ? (
                          <input value={editName} onChange={(event) => setEditName(event.target.value)} className="admin-input max-w-xs" maxLength={80} aria-label="اسم النوع" />
                        ) : <span className="font-bold text-[var(--admin-text)]">{type.name}</span>}
                      </td>
                      <td className="px-4 py-3">
                        {isEditing ? (
                          <input type="number" value={editOrder} onChange={(event) => setEditOrder(Number(event.target.value))} className="admin-input w-28" min={0} max={10000} aria-label="ترتيب النوع" />
                        ) : type.sortOrder}
                      </td>
                      <td className="px-4 py-3">{type.assignedVideoCount}</td>
                      <td className="px-4 py-3">
                        <span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-bold ${type.isActive ? 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-400' : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)]'}`}>
                          {type.isActive && <Check className="h-3.5 w-3.5" aria-hidden="true" />}
                          {type.isActive ? 'نشط' : 'معطل'}
                        </span>
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex justify-end gap-1">
                          {isEditing ? (
                            <>
                              <button type="button" onClick={() => void saveEdit()} disabled={isBusy || editName.trim().length < 2} className="admin-btn-icon" aria-label="حفظ تعديلات النوع" title="حفظ التعديلات"><Save className="h-4 w-4" /></button>
                              <button type="button" onClick={() => setEditing(null)} className="admin-btn-icon" aria-label="إلغاء تعديل النوع" title="إلغاء"><X className="h-4 w-4" /></button>
                            </>
                          ) : (
                            <>
                              <button type="button" onClick={() => startEdit(type)} disabled={isBusy} className="admin-btn-icon" aria-label={`تعديل ${type.name}`} title="تعديل النوع"><Edit2 className="h-4 w-4" /></button>
                              <button type="button" onClick={() => void toggleStatus(type)} disabled={isBusy} className="admin-btn-icon" aria-label={`${type.isActive ? 'تعطيل' : 'تفعيل'} ${type.name}`} title={type.isActive ? 'تعطيل النوع' : 'تفعيل النوع'}><RefreshCw className="h-4 w-4" /></button>
                              <button type="button" onClick={() => setDeleteTarget(type)} disabled={isBusy} className="admin-btn-icon text-red-600" aria-label={`حذف ${type.name}`} title="حذف النوع"><Trash2 className="h-4 w-4" /></button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <ConfirmDialog
        open={deleteTarget !== null}
        title="حذف نوع الفيديو"
        description={deleteTarget?.assignedVideoCount ? `النوع مستخدم في ${deleteTarget.assignedVideoCount} فيديو. سيمنع النظام الحذف ويجب تعطيله بدلاً من ذلك.` : `سيتم حذف النوع "${deleteTarget?.name ?? ''}" نهائياً.`}
        confirmLabel="حذف النوع"
        onConfirm={() => void deleteType()}
        onCancel={() => setDeleteTarget(null)}
      />
    </AdminPage>
  );
}
