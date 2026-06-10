'use client';

import { devConsole } from '@/utils/dev-console';
import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Clipboard, ClipboardList, Edit2, ExternalLink, Eye, Inbox, Plus, Trash2 } from 'lucide-react';
import toast from 'react-hot-toast';

import {
  AdminShellChrome,
  AdminDataTable,
  AdminStatCard,
  AdminModal,
  AdminColumn,
} from '@/components/admin';
import { getAdminForms, deleteAdminForm, CustomFormDto, getAdminFormDetails, updateAdminForm } from '@/services/forms-service';
import { getAbsoluteLandingUrl } from '@/utils/url-utils';

export default function AdminFormsPageClient() {
  const [forms, setForms] = useState<CustomFormDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [deleteFormId, setDeleteFormId] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);

  const fetchForms = async () => {
    try {
      setLoading(true);
      const data = await getAdminForms();
      setForms(data);
    } catch (error) {
      devConsole.error('Error fetching forms:', error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchForms();
  }, []);

  const getPublicFormUrl = (slug: string) => {
    return getAbsoluteLandingUrl(`/forms/${slug}`);
  };

  const handleCopyFormUrl = async (slug: string) => {
    const formUrl = getPublicFormUrl(slug);
    try {
      await navigator.clipboard.writeText(formUrl);
      toast.success('تم نسخ رابط النموذج الكامل');
    } catch (error) {
      devConsole.error('Error copying form link:', error);
      toast.error('تعذر نسخ الرابط');
    }
  };

  const handleDelete = async () => {
    if (!deleteFormId) return;
    try {
      setDeleting(true);
      await deleteAdminForm(deleteFormId);
      toast.success('تم حذف النموذج بنجاح');
      setForms((prev) => prev.filter((f) => f.id !== deleteFormId));
      setDeleteFormId(null);
    } catch (error) {
      devConsole.error('Error deleting form:', error);
    } finally {
      setDeleting(false);
    }
  };

  const handleToggleActive = async (row: CustomFormDto) => {
    const originalIsActive = row.isActive;
    // Optimistic UI update
    setForms((prev) =>
      prev.map((f) => (f.id === row.id ? { ...f, isActive: !originalIsActive } : f))
    );

    try {
      const details = await getAdminFormDetails(row.id);
      await updateAdminForm(row.id, {
        title: details.title,
        description: details.description,
        slug: details.slug,
        coverImageUrl: details.coverImageUrl,
        startsAt: details.startsAt,
        expiresAt: details.expiresAt,
        fieldsJson: details.fieldsJson,
        isActive: !originalIsActive,
      });
      toast.success(!originalIsActive ? 'تم فتح استقبال الطلبات بنجاح' : 'تم إغلاق استقبال الطلبات بنجاح');
    } catch (error) {
      devConsole.error('Error toggling form active state:', error);
      toast.error('تعذر تغيير حالة النموذج');
      // Revert UI update
      setForms((prev) =>
        prev.map((f) => (f.id === row.id ? { ...f, isActive: originalIsActive } : f))
      );
    }
  };

  const columns: AdminColumn<CustomFormDto>[] = [
    {
      key: 'title',
      label: 'عنوان النموذج',
      render: (row) => (
        <div>
          <div className="font-bold text-[var(--admin-text-strong)]">{row.title}</div>
          <div className="text-xs text-[var(--admin-muted)] mt-0.5">{row.description || 'لا يوجد وصف'}</div>
        </div>
      ),
    },
    {
      key: 'slug',
      label: 'الرابط المختصر',
      render: (row) => (
        <div className="flex flex-wrap items-center gap-2">
          <code className="rounded bg-[var(--admin-card-strong)] px-2 py-1 text-xs text-[var(--admin-primary)]">
            /forms/{row.slug}
          </code>
          <button
            type="button"
            onClick={() => handleCopyFormUrl(row.slug)}
            className="inline-flex h-9 w-9 items-center justify-center rounded-xl text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] hover:text-[var(--admin-text)] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--admin-primary)]"
            title="نسخ الرابط الكامل"
            aria-label={`نسخ الرابط الكامل لنموذج ${row.title}`}
          >
            <Clipboard className="h-4 w-4" />
          </button>
          <Link
            href={getPublicFormUrl(row.slug)}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex h-9 w-9 items-center justify-center rounded-xl text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] hover:text-[var(--admin-text)] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--admin-primary)]"
            title="فتح النموذج"
            aria-label={`فتح نموذج ${row.title}`}
          >
            <ExternalLink className="h-4 w-4" />
          </Link>
        </div>
      ),
    },
    {
      key: 'visitCount',
      label: 'الزوار',
      render: (row) => (
        <span className="inline-flex items-center gap-1.5 rounded-full bg-[var(--admin-card-strong)] px-3 py-1 text-xs font-bold text-[var(--admin-text)]">
          <Eye className="h-3.5 w-3.5 text-[var(--admin-primary)]" />
          {row.visitCount}
        </span>
      ),
      align: 'center',
    },
    {
      key: 'submissionCount',
      label: 'الطلبات المستلمة',
      render: (row) => (
        <span className="inline-flex items-center gap-1.5 rounded-full bg-[var(--admin-primary-soft)] px-3 py-1 text-xs font-bold text-[var(--admin-primary)]">
          <Inbox className="h-3.5 w-3.5" />
          {row.submissionCount}
        </span>
      ),
      align: 'center',
    },
    {
      key: 'isActive',
      label: 'حالة الاستقبال',
      render: (row) => (
        <button
          type="button"
          onClick={() => handleToggleActive(row)}
          className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-black transition-all hover:scale-105 active:scale-95 ${
            row.isActive
              ? 'bg-emerald-500/10 text-emerald-500 border border-emerald-500/20 hover:bg-emerald-500/20'
              : 'bg-rose-500/10 text-rose-500 border border-rose-500/20 hover:bg-rose-500/20'
          }`}
          title={row.isActive ? "اضغط لإغلاق استقبال الطلبات" : "اضغط لفتح استقبال الطلبات"}
        >
          <span className={`h-1.5 w-1.5 rounded-full ${row.isActive ? 'bg-emerald-500' : 'bg-rose-500'}`} />
          {row.isActive ? 'مفتوح' : 'مغلق'}
        </button>
      ),
      align: 'center',
    },
    {
      key: 'createdAt',
      label: 'تاريخ الإنشاء',
      render: (row) => new Date(row.createdAt).toLocaleDateString('ar-EG', { dateStyle: 'medium' }),
    },
    {
      key: 'actions',
      label: 'إجراءات',
      render: (row) => (
        <div className="flex gap-2 justify-end">
          <Link
            href={`/admin/forms/${row.id}/submissions`}
            className="flex items-center gap-1 text-xs font-bold text-[var(--admin-primary)] hover:underline border border-[var(--admin-border)] rounded-xl px-3 py-1.5 hover:bg-[var(--admin-hover)]"
            title="عرض الطلبات"
          >
            <Inbox className="h-3.5 w-3.5" />
            الطلبات
          </Link>
          <Link
            href={`/admin/forms/${row.id}/edit`}
            className="p-2 text-[var(--admin-muted)] hover:text-[var(--admin-text)] hover:bg-[var(--admin-hover)] rounded-xl"
            title="تعديل"
          >
            <Edit2 className="h-4 w-4" />
          </Link>
          <button
            onClick={() => setDeleteFormId(row.id)}
            className="p-2 text-rose-500 hover:bg-rose-500/10 rounded-xl"
            title="حذف"
          >
            <Trash2 className="h-4 w-4" />
          </button>
        </div>
      ),
      align: 'left',
    },
  ];

  const totalSubmissions = forms.reduce((sum, f) => sum + f.submissionCount, 0);
  const activeForms = forms.filter((f) => f.isActive).length;

  return (
    <AdminShellChrome
      activePath="/admin/forms"
      sectionLabel="أدوات الإدارة"
      pageTitle="النماذج المخصصة"
      subtitle="إدارة نماذج التوظيف والحجز وجمع البيانات وحالات القبول والملاحظات."
      action={
        <Link
          href="/admin/forms/new"
          className="admin-btn-primary flex items-center gap-2"
        >
          <Plus className="h-5 w-5" />
          إنشاء نموذج جديد
        </Link>
      }
    >
      <div className="grid gap-6 md:grid-cols-3 mb-8">
        <AdminStatCard
          variant="light"
          label="إجمالي النماذج"
          value={forms.length}
          icon={ClipboardList}
          subtitle="جميع النماذج التي تم إنشاؤها"
        />
        <AdminStatCard
          variant="light"
          label="النماذج الفعالة"
          value={activeForms}
          icon={ClipboardList}
          subtitle="تستقبل طلبات حالياً"
        />
        <AdminStatCard
          variant="muted"
          label="إجمالي الطلبات"
          value={totalSubmissions}
          icon={Inbox}
          subtitle="جميع التقديمات المستلمة للتوظيف والحجز"
        />
      </div>

      <div className="admin-panel mt-6">
        <AdminDataTable
          data={forms}
          columns={columns}
          loading={loading}
          rowKey={(item) => item.id}
          emptyMessage="لم يتم إنشاء أي نماذج مخصصة بعد."
        />
      </div>

      <AdminModal
        open={deleteFormId !== null}
        onClose={() => setDeleteFormId(null)}
        title="تأكيد الحذف"
      >
        <div className="space-y-6 text-right">
          <p className="text-sm text-[var(--admin-muted)] text-right">
            هل أنت متأكد من رغبتك في حذف هذا النموذج؟ هذا الإجراء سيؤدي لحذف جميع البيانات والطلبات والتقديمات المرتبطة به نهائياً ولا يمكن التراجع عنه.
          </p>
          <div className="flex gap-3 justify-end pt-4 border-t border-[var(--admin-border)]">
            <button
              type="button"
              onClick={() => setDeleteFormId(null)}
              className="admin-btn-ghost"
              disabled={deleting}
            >
              إلغاء
            </button>
            <button
              type="button"
              onClick={handleDelete}
              className="bg-rose-600 hover:bg-rose-700 text-white rounded-2xl px-5 py-2 text-sm font-bold disabled:opacity-50"
              disabled={deleting}
            >
              {deleting ? 'جاري الحذف...' : 'تأكيد الحذف'}
            </button>
          </div>
        </div>
      </AdminModal>
    </AdminShellChrome>
  );
}
