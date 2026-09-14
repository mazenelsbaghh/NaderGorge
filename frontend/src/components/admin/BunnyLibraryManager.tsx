'use client';

import { useCallback, useEffect, useState } from 'react';
import {
  CheckCircle2,
  Cloud,
  Edit2,
  KeyRound,
  Library,
  Loader2,
  Plus,
  Power,
  RefreshCw,
  Trash2,
  Video,
} from 'lucide-react';
import toast from 'react-hot-toast';

import { getApiErrorSummary } from '@/lib/api-errors';
import {
  adminService,
  type BunnyLibraryDto,
  type CreateBunnyLibraryPayload,
  type UpdateBunnyLibraryPayload,
} from '@/services/admin-service';

import { AdminConfirmationDialog } from './AdminConfirmationDialog';
import { AdminModal } from './AdminModal';

type BunnyLibraryFormState = {
  name: string;
  libraryId: string;
  apiKey: string;
  hlsCdnHostname: string;
  hlsTokenKey: string;
};

const EMPTY_FORM: BunnyLibraryFormState = { name: '', libraryId: '', apiKey: '', hlsCdnHostname: '', hlsTokenKey: '' };

function validationMessage(
  form: BunnyLibraryFormState,
  editingLibrary: BunnyLibraryDto | null
) {
  if (form.name.trim().length < 2) return 'اكتب اسمًا واضحًا للمكتبة.';
  if (!/^\d+$/.test(form.libraryId) || /^0+$/.test(form.libraryId)) {
    return 'Library ID يجب أن يكون رقمًا موجبًا.';
  }
  if (!editingLibrary?.apiKeyConfigured && !form.apiKey.trim()) return 'أدخل API Key صالحًا لهذه المكتبة.';
  const hasHost = Boolean(form.hlsCdnHostname.trim());
  const hasKey = Boolean(form.hlsTokenKey.trim());
  if (!editingLibrary?.hlsConfigured && hasHost !== hasKey) return 'اسم CDN ومفتاح Token Authentication مطلوبان معًا.';
  if (hasHost && !/^[a-z0-9.-]+\.b-cdn\.net$/i.test(form.hlsCdnHostname.trim())) return 'اكتب اسم Bunny CDN مثل vz-xxxx.b-cdn.net بدون https.';
  return null;
}

function createPayload(form: BunnyLibraryFormState): CreateBunnyLibraryPayload {
  return {
    name: form.name.trim(),
    libraryId: form.libraryId,
    apiKey: form.apiKey.trim(),
    isActive: true,
    hlsCdnHostname: form.hlsCdnHostname.trim() || undefined,
    hlsTokenKey: form.hlsTokenKey.trim() || undefined,
  };
}

function updatePayload(
  form: BunnyLibraryFormState,
  editingLibrary: BunnyLibraryDto
): UpdateBunnyLibraryPayload {
  const apiKey = form.apiKey.trim();
  return {
    name: form.name.trim(),
    libraryId: form.libraryId,
    isActive: editingLibrary.isActive,
    ...(apiKey ? { apiKey } : {}),
    ...(form.hlsCdnHostname.trim() ? { hlsCdnHostname: form.hlsCdnHostname.trim() } : {}),
    ...(form.hlsTokenKey.trim() ? { hlsTokenKey: form.hlsTokenKey.trim() } : {}),
  };
}

function formattedValidationDate(date?: string | null) {
  if (!date) return 'لم يتم التحقق بعد';
  return new Intl.DateTimeFormat('ar-EG', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(date));
}

export function BunnyLibraryManager() {
  const [libraries, setLibraries] = useState<BunnyLibraryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [editingLibrary, setEditingLibrary] = useState<BunnyLibraryDto | null>(null);
  const [form, setForm] = useState<BunnyLibraryFormState>(EMPTY_FORM);
  const [formOpen, setFormOpen] = useState(false);
  const [formSaving, setFormSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [busyLibraryId, setBusyLibraryId] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<BunnyLibraryDto | null>(null);

  const loadLibraries = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      setLibraries(await adminService.listBunnyLibraries());
    } catch (requestError) {
      setLibraries([]);
      setLoadError(getApiErrorSummary(requestError, 'تعذر تحميل مكتبات Bunny.'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void loadLibraries(); }, [loadLibraries]);

  const openCreateForm = () => {
    setEditingLibrary(null);
    setForm(EMPTY_FORM);
    setFormError(null);
    setFormOpen(true);
  };

  const openEditForm = (library: BunnyLibraryDto) => {
    setEditingLibrary(library);
    setForm({ name: library.name, libraryId: library.libraryId, apiKey: '', hlsCdnHostname: library.hlsCdnHostname ?? '', hlsTokenKey: '' });
    setFormError(null);
    setFormOpen(true);
  };

  const clearFormState = () => {
    setFormOpen(false);
    setEditingLibrary(null);
    setForm(EMPTY_FORM);
    setFormError(null);
  };

  const closeForm = () => {
    if (!formSaving) clearFormState();
  };

  const saveLibrary = async (event: React.FormEvent) => {
    event.preventDefault();
    const invalidMessage = validationMessage(form, editingLibrary);
    if (invalidMessage) {
      setFormError(invalidMessage);
      return;
    }

    setFormSaving(true);
    setFormError(null);
    try {
      if (editingLibrary) {
        await adminService.updateBunnyLibrary(
          editingLibrary.id,
          updatePayload(form, editingLibrary)
        );
      } else {
        await adminService.createBunnyLibrary(createPayload(form));
      }
      toast.success(editingLibrary ? 'تم تحديث مكتبة Bunny.' : 'تمت إضافة مكتبة Bunny.');
      clearFormState();
      await loadLibraries();
    } catch (requestError) {
      setFormError(getApiErrorSummary(requestError, 'تعذر التحقق من بيانات المكتبة وحفظها.'));
    } finally {
      setFormSaving(false);
    }
  };

  const toggleLibraryStatus = async (library: BunnyLibraryDto) => {
    setBusyLibraryId(library.id);
    try {
      await adminService.setBunnyLibraryStatus(library.id, !library.isActive);
      toast.success(library.isActive ? 'تم تعطيل المكتبة للرفع الجديد.' : 'تم تفعيل المكتبة.');
      await loadLibraries();
    } catch (requestError) {
      toast.error(getApiErrorSummary(requestError, 'تعذر تغيير حالة المكتبة.'));
    } finally {
      setBusyLibraryId(null);
    }
  };

  const deleteLibrary = async () => {
    if (!deleteTarget) return;
    setBusyLibraryId(deleteTarget.id);
    try {
      await adminService.deleteBunnyLibrary(deleteTarget.id);
      toast.success('تم حذف المكتبة غير المستخدمة.');
      setDeleteTarget(null);
      await loadLibraries();
    } catch (requestError) {
      toast.error(getApiErrorSummary(requestError, 'تعذر حذف المكتبة.'));
    } finally {
      setBusyLibraryId(null);
    }
  };

  return (
    <section className="space-y-5" dir="rtl" aria-labelledby="bunny-libraries-title">
      <div className="flex flex-col gap-4 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 sm:flex-row sm:items-center sm:justify-between sm:p-7">
        <div className="flex items-start gap-3">
          <span className="grid size-11 shrink-0 place-items-center rounded-xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
            <Cloud className="h-5 w-5" />
          </span>
          <div>
            <h2 id="bunny-libraries-title" className="text-lg font-black text-[var(--admin-text)]">مكتبات Bunny Stream</h2>
            <p className="mt-1 max-w-2xl text-sm font-semibold leading-6 text-[var(--admin-muted)]">
              أضف المكتبات المتاحة لرفع الفيديو، وحدّث المفتاح دون كشف القيمة المحفوظة.
            </p>
          </div>
        </div>
        <button type="button" onClick={openCreateForm} className="inline-flex min-h-11 shrink-0 items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-black text-[var(--admin-primary-contrast)] transition-colors hover:bg-[var(--admin-primary-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]">
          <Plus className="h-4 w-4" />
          إضافة مكتبة
        </button>
      </div>

      {loading ? (
        <div className="space-y-3" aria-label="جاري تحميل مكتبات Bunny">
          {[0, 1, 2].map((placeholder) => <div key={placeholder} className="h-28 animate-pulse rounded-2xl bg-[var(--admin-card-soft)]" />)}
        </div>
      ) : loadError ? (
        <div className="flex min-h-48 flex-col items-center justify-center gap-4 rounded-2xl border border-red-500/20 bg-red-500/10 p-8 text-center" role="alert">
          <p className="text-sm font-bold text-red-700 dark:text-red-300">{loadError}</p>
          <button type="button" onClick={() => void loadLibraries()} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-bold text-[var(--admin-primary-contrast)]">
            <RefreshCw className="h-4 w-4" />
            إعادة المحاولة
          </button>
        </div>
      ) : libraries.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-card)] px-6 py-12 text-center">
          <Library className="mx-auto h-8 w-8 text-[var(--admin-primary)]" />
          <h3 className="mt-4 text-lg font-black text-[var(--admin-text)]">لا توجد مكتبات مسجلة</h3>
          <p className="mt-2 text-sm font-semibold text-[var(--admin-muted)]">أضف المكتبة الأولى لتصبح متاحة عند رفع فيديو Bunny.</p>
        </div>
      ) : (
        <div className="overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]">
          <div className="divide-y divide-[var(--admin-border)]">
            {libraries.map((library) => {
              const busy = busyLibraryId === library.id;
              return (
                <article key={library.id} className="grid gap-4 p-4 transition-colors hover:bg-[var(--admin-hover)] sm:p-5 lg:grid-cols-[minmax(0,1.2fr)_minmax(190px,.8fr)_auto] lg:items-center">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <h3 className="truncate text-base font-black text-[var(--admin-text)]">{library.name}</h3>
                      <span className={`rounded-full px-2.5 py-1 text-xs font-black ${library.isActive ? 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-400' : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)]'}`}>
                        {library.isActive ? 'نشطة' : 'معطلة'}
                      </span>
                      <span className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-black ${library.apiKeyConfigured ? 'bg-[var(--admin-primary-15)] text-[var(--admin-primary)]' : 'bg-amber-500/10 text-amber-700 dark:text-amber-300'}`}>
                        <KeyRound className="h-3.5 w-3.5" />
                        {library.apiKeyConfigured ? 'المفتاح محفوظ' : 'مفتاح مطلوب'}
                      </span>
                      <span className={`rounded-full px-2.5 py-1 text-xs font-black ${library.hlsConfigured ? 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-400' : 'bg-amber-500/10 text-amber-700 dark:text-amber-300'}`}>
                        {library.hlsConfigured ? 'HLS جاهز' : 'HLS غير مُعد'}
                      </span>
                    </div>
                    <p className="mt-2 font-mono text-sm font-bold text-[var(--admin-muted)]" dir="ltr">Library ID: {library.libraryId}</p>
                    <p className="mt-1 text-xs font-semibold text-[var(--admin-muted)]">آخر تحقق: {formattedValidationDate(library.lastValidatedAtUtc)}</p>
                  </div>

                  <div className="flex items-center gap-3 rounded-xl bg-[var(--admin-card-soft)] px-4 py-3">
                    <Video className="h-5 w-5 text-[var(--admin-primary)]" />
                    <div>
                      <p className="text-xs font-bold text-[var(--admin-muted)]">الفيديوهات المرتبطة</p>
                      <p className="text-lg font-black text-[var(--admin-text)]">{library.assignedVideoCount}</p>
                    </div>
                  </div>

                  <div className="flex flex-wrap items-center justify-end gap-1.5">
                    <button type="button" onClick={() => openEditForm(library)} disabled={busy} className="admin-btn-icon" aria-label={`تعديل مكتبة ${library.name}`} title="تعديل الاسم أو المفتاح">
                      <Edit2 className="h-4 w-4" />
                    </button>
                    <button type="button" onClick={() => void toggleLibraryStatus(library)} disabled={busy} className="admin-btn-icon" aria-label={`${library.isActive ? 'تعطيل' : 'تفعيل'} مكتبة ${library.name}`} title={library.isActive ? 'تعطيل الرفع الجديد' : 'تفعيل المكتبة'}>
                      {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Power className="h-4 w-4" />}
                    </button>
                    <button type="button" onClick={() => setDeleteTarget(library)} disabled={busy || library.assignedVideoCount > 0} className="admin-btn-icon text-red-600 disabled:cursor-not-allowed disabled:opacity-35" aria-label={`حذف مكتبة ${library.name}`} title={library.assignedVideoCount > 0 ? 'لا يمكن حذف مكتبة مرتبطة بفيديوهات؛ عطّلها بدلًا من ذلك' : 'حذف المكتبة'}>
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                </article>
              );
            })}
          </div>
        </div>
      )}

      <AdminModal open={formOpen} onClose={closeForm} title={editingLibrary ? `تعديل مكتبة ${editingLibrary.name}` : 'إضافة مكتبة Bunny'} subtitle="سيختبر الخادم Library ID وAPI Key مع Bunny قبل الحفظ." maxWidth="max-w-xl">
        <form onSubmit={saveLibrary} className="space-y-5" dir="rtl">
          <label className="block space-y-2 text-sm font-bold text-[var(--admin-text)]">
            اسم المكتبة
            <input value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} disabled={formSaving} maxLength={80} placeholder="مثال: أولى" className="admin-input mt-2" autoFocus />
          </label>

          <div className="space-y-4 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
            <div>
              <p className="text-sm font-black text-[var(--admin-text)]">مشغل المنصة HLS</p>
              <p className="mt-1 text-xs font-semibold leading-5 text-[var(--admin-muted)]">من Bunny: Security → CDN token authentication. اترك Embed view token authentication مغلقًا.</p>
            </div>
            <label className="block space-y-2 text-sm font-bold text-[var(--admin-text)]">
              CDN hostname
              <input value={form.hlsCdnHostname} onChange={(event) => setForm((current) => ({ ...current, hlsCdnHostname: event.target.value }))} disabled={formSaving} dir="ltr" placeholder="vz-xxxx.b-cdn.net" className="admin-input mt-2 text-left font-mono" />
            </label>
            <label className="block space-y-2 text-sm font-bold text-[var(--admin-text)]">
              {editingLibrary?.hlsConfigured ? 'Token Authentication Key جديد (اختياري)' : 'Token Authentication Key'}
              <input type="password" value={form.hlsTokenKey} onChange={(event) => setForm((current) => ({ ...current, hlsTokenKey: event.target.value }))} disabled={formSaving} autoComplete="new-password" spellCheck={false} dir="ltr" placeholder={editingLibrary?.hlsConfigured ? 'اتركه فارغًا للاحتفاظ بالمفتاح الحالي' : 'ألصق مفتاح CDN'} className="admin-input mt-2 text-left font-mono" />
              <span className="block text-xs font-semibold text-[var(--admin-muted)]">المفتاح يُخزن مشفرًا ولن يظهر مرة أخرى.</span>
            </label>
          </div>
          <label className="block space-y-2 text-sm font-bold text-[var(--admin-text)]">
            Library ID
            <input value={form.libraryId} onChange={(event) => setForm((current) => ({ ...current, libraryId: event.target.value.replace(/\D/g, '') }))} disabled={formSaving || Boolean(editingLibrary?.assignedVideoCount)} inputMode="numeric" dir="ltr" placeholder="740733" className="admin-input mt-2 text-left font-mono" />
            {Boolean(editingLibrary?.assignedVideoCount) && <span className="block text-xs font-semibold text-[var(--admin-muted)]">لا يمكن تغيير الرقم بعد ربط فيديوهات بالمكتبة.</span>}
          </label>
          <label className="block space-y-2 text-sm font-bold text-[var(--admin-text)]">
            {editingLibrary?.apiKeyConfigured ? 'API Key جديد (اختياري)' : 'API Key'}
            <input type="password" value={form.apiKey} onChange={(event) => setForm((current) => ({ ...current, apiKey: event.target.value }))} disabled={formSaving} autoComplete="new-password" spellCheck={false} dir="ltr" placeholder={editingLibrary?.apiKeyConfigured ? 'اتركه فارغًا للاحتفاظ بالمفتاح الحالي' : 'ألصق المفتاح الكامل'} className="admin-input mt-2 text-left font-mono" />
            <span className="block text-xs font-semibold text-[var(--admin-muted)]">لن تظهر قيمة المفتاح مرة أخرى بعد الحفظ.</span>
          </label>

          {formError && <div className="rounded-xl border border-red-500/20 bg-red-500/10 px-4 py-3 text-sm font-bold text-red-700 dark:text-red-300" role="alert">{formError}</div>}

          <div className="flex flex-col-reverse gap-2 border-t border-[var(--admin-border)] pt-4 sm:flex-row sm:justify-end">
            <button type="button" onClick={closeForm} disabled={formSaving} className="min-h-11 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-5 text-sm font-bold text-[var(--admin-text)] disabled:opacity-50">إلغاء</button>
            <button type="submit" disabled={formSaving} className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] px-6 text-sm font-black text-[var(--admin-primary-contrast)] disabled:opacity-50">
              {formSaving ? <Loader2 className="h-4 w-4 animate-spin" /> : <CheckCircle2 className="h-4 w-4" />}
              {formSaving ? 'جاري التحقق والحفظ...' : 'تحقق واحفظ'}
            </button>
          </div>
        </form>
      </AdminModal>

      <AdminConfirmationDialog
        open={deleteTarget !== null}
        onClose={() => setDeleteTarget(null)}
        onConfirm={deleteLibrary}
        title="حذف مكتبة Bunny"
        consequence={`سيتم حذف مكتبة «${deleteTarget?.name ?? ''}» نهائيًا. لا يتاح هذا الإجراء إلا للمكتبات غير المرتبطة بأي فيديو.`}
        confirmLabel="حذف المكتبة"
        variant="danger"
        isConfirming={Boolean(deleteTarget && busyLibraryId === deleteTarget.id)}
      />
    </section>
  );
}
