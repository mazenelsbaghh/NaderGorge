'use client';

import { devConsole } from '@/utils/dev-console';
import { useCallback, useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { ArrowRight, Inbox, Eye, CheckCircle, RefreshCw, FileText, Settings } from 'lucide-react';
import toast from 'react-hot-toast';

import {
  AdminShellChrome,
  AdminDataTable,
  AdminModal,
  AdminPageSkeleton,
  AdminColumn,
} from '@/components/admin';
import {
  getAdminFormDetails,
  getFormSubmissions,
  updateSubmissionStatus,
  CustomFormDetailDto,
  FormSubmissionDto,
  FormFieldConfig,
  FormSubmissionStatus,
} from '@/services/forms-service';

interface SubmissionsPageProps {
  params: { id: string };
}

export default function SubmissionsPageClient({ params }: SubmissionsPageProps) {
  const { id } = params;
  const router = useRouter();

  const [form, setForm] = useState<CustomFormDetailDto | null>(null);
  const [fields, setFields] = useState<FormFieldConfig[]>([]);
  const [submissions, setSubmissions] = useState<FormSubmissionDto[]>([]);
  
  const [loading, setLoading] = useState(true);
  const [selectedSubmission, setSelectedSubmission] = useState<FormSubmissionDto | null>(null);
  const [modalStatus, setModalStatus] = useState<FormSubmissionStatus>('Pending');
  const [modalNotes, setModalNotes] = useState('');
  const [savingStatus, setSavingStatus] = useState(false);

  const loadData = useCallback(async () => {
    try {
      setLoading(true);
      const formData = await getAdminFormDetails(id);
      setForm(formData);
      setFields(JSON.parse(formData.fieldsJson || '[]'));

      const subsData = await getFormSubmissions(id);
      setSubmissions(subsData);
    } catch (error) {
      devConsole.error('Error loading submissions data:', error);
      toast.error('حدث خطأ أثناء تحميل البيانات');
      router.push('/admin/forms');
    } finally {
      setLoading(false);
    }
  }, [id, router]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const openDetailsModal = (sub: FormSubmissionDto) => {
    setSelectedSubmission(sub);
    setModalStatus(sub.status);
    setModalNotes(sub.adminNotes || '');
  };

  const handleSaveStatus = async () => {
    if (!selectedSubmission) return;
    try {
      setSavingStatus(true);
      await updateSubmissionStatus(selectedSubmission.id, modalStatus, modalNotes);
      toast.success('تم تحديث حالة الطلب وحفظ الملاحظات بنجاح');
      
      // Update local state
      setSubmissions((prev) =>
        prev.map((s) => {
          if (s.id === selectedSubmission.id) {
            return { ...s, status: modalStatus, adminNotes: modalNotes };
          }
          return s;
        })
      );
      setSelectedSubmission(null);
    } catch (error) {
      devConsole.error('Error updating status:', error);
    } finally {
      setSavingStatus(false);
    }
  };

  if (loading) {
    return (
      <AdminShellChrome activePath="/admin/forms" sectionLabel="أدوات الإدارة" pageTitle="الطلبات المستلمة">
        <AdminPageSkeleton />
      </AdminShellChrome>
    );
  }

  // Parse first two fields to show in the table row
  const summaryFields = fields.slice(0, 2);

  const columns: AdminColumn<FormSubmissionDto>[] = [
    {
      key: 'submittedAt',
      label: 'تاريخ التقديم',
      render: (row) => (
        <div>
          <div className="font-bold text-[var(--admin-text-strong)]">
            {new Date(row.submittedAt).toLocaleDateString('ar-EG', { dateStyle: 'medium' })}
          </div>
          <div className="text-xs text-[var(--admin-muted)] mt-0.5">
            {new Date(row.submittedAt).toLocaleTimeString('ar-EG', { timeStyle: 'short' })}
          </div>
        </div>
      ),
    },
    ...summaryFields.map((field) => ({
      key: `field_${field.id}`,
      label: field.label,
      render: (row: FormSubmissionDto) => {
        let answersMap: Record<string, string> = {};
        try {
          answersMap = JSON.parse(row.submittedDataJson || '{}');
        } catch {
          answersMap = {};
        }
        const val = answersMap[field.id] || '';
        return (
          <span className="truncate max-w-[200px] inline-block font-medium text-[var(--admin-text-strong)]">
            {val === 'true' ? 'نعم' : val || '-'}
          </span>
        );
      },
    })),
    {
      key: 'status',
      label: 'الحالة',
      render: (row) => {
        const colors: Record<FormSubmissionStatus, string> = {
          Pending: 'bg-amber-500/10 text-amber-500 border border-amber-500/20',
          Reviewed: 'bg-blue-500/10 text-blue-500 border border-blue-500/20',
          Accepted: 'bg-emerald-500/10 text-emerald-500 border border-emerald-500/20',
          Rejected: 'bg-rose-500/10 text-rose-500 border border-rose-500/20',
        };
        const labels: Record<FormSubmissionStatus, string> = {
          Pending: 'قيد الانتظار',
          Reviewed: 'قيد المراجعة',
          Accepted: 'مقبول',
          Rejected: 'مرفوض',
        };
        return (
          <span className={`inline-block rounded-full px-2.5 py-1 text-xs font-black ${colors[row.status]}`}>
            {labels[row.status]}
          </span>
        );
      },
      align: 'center',
    },
    {
      key: 'actions',
      label: 'إجراءات',
      render: (row) => (
        <button
          onClick={() => openDetailsModal(row)}
          className="flex items-center gap-1 bg-[var(--admin-card-strong)] hover:bg-[var(--admin-hover)] text-xs font-bold text-[var(--admin-text)] border border-[var(--admin-border)] rounded-xl px-3 py-1.5 transition"
        >
          <Eye className="h-3.5 w-3.5 text-[var(--admin-primary)]" />
          عرض التفاصيل
        </button>
      ),
      align: 'left',
    },
  ];

  // Decode the selected submission answers
  let selectedAnswers: Record<string, string> = {};
  if (selectedSubmission) {
    try {
      selectedAnswers = JSON.parse(selectedSubmission.submittedDataJson || '{}');
    } catch (e) {
      devConsole.error(e);
    }
  }

  return (
    <AdminShellChrome
      activePath="/admin/forms"
      sectionLabel="النماذج المخصصة"
      pageTitle={form?.title || 'الطلبات'}
      subtitle={`عرض وفرز طلبات الحجز والتوظيف المستلمة وتحديث حالات القبول والرفض.`}
      action={
        <button
          onClick={() => router.push('/admin/forms')}
          className="admin-btn-ghost flex items-center gap-2"
        >
          <ArrowRight className="h-5 w-5" />
          رجوع للنماذج
        </button>
      }
    >
      <div className="admin-panel">
        <div className="flex items-center justify-between border-b border-[var(--admin-border)] pb-4 mb-6">
          <div className="flex items-center gap-2">
            <Inbox className="h-5 w-5 text-[var(--admin-primary)]" />
            <h2 className="text-lg font-bold text-[var(--admin-text-strong)]">سجل الطلبات المستلمة ({submissions.length})</h2>
          </div>
          <button
            onClick={loadData}
            className="p-2 text-[var(--admin-muted)] hover:text-[var(--admin-text)] hover:bg-[var(--admin-hover)] rounded-xl"
            title="تحديث البيانات"
          >
            <RefreshCw className="h-4 w-4" />
          </button>
        </div>

        <AdminDataTable
          data={submissions}
          columns={columns}
          rowKey={(item) => item.id}
          emptyMessage="لم يتم استلام أي طلبات تقديم لهذا النموذج بعد."
        />
      </div>

      {/* Submission details review modal */}
      <AdminModal
        open={selectedSubmission !== null}
        onClose={() => setSelectedSubmission(null)}
        title="تفاصيل الطلب ومراجعته"
      >
        {selectedSubmission && (
          <div className="space-y-6 max-h-[70dvh] overflow-y-auto pr-1 text-right" dir="rtl">
            {/* Header info */}
            <div className="grid gap-2 border-b border-[var(--admin-border)] pb-4 text-xs text-[var(--admin-muted)]">
              <div>معرف التقديم: <code className="bg-[var(--admin-card-strong)] px-1.5 py-0.5 rounded text-[var(--admin-primary)]">{selectedSubmission.id}</code></div>
              <div>تاريخ ووقت التقديم: {new Date(selectedSubmission.submittedAt).toLocaleString('ar-EG')}</div>
            </div>

            {/* Submitted field values */}
            <div className="space-y-4">
              <div className="flex items-center gap-1 text-[var(--admin-text)] font-bold text-sm border-r-2 border-[var(--admin-primary)] pr-2">
                <FileText className="h-4 w-4" />
                <span>إجابات مقدم الطلب:</span>
              </div>

              <div className="grid gap-4 bg-[var(--admin-card-soft)] p-5 rounded-2xl border border-[var(--admin-border)]">
                {fields.map((field) => {
                  const val = selectedAnswers[field.id] || '';
                  return (
                    <div key={field.id} className="space-y-1">
                      <div className="text-[10px] font-bold text-[var(--admin-muted)] uppercase tracking-wider">
                        {field.label}
                      </div>
                      <div className="text-sm font-semibold text-[var(--admin-text-strong)] whitespace-pre-wrap">
                        {field.type === 'checkbox' ? (val === 'true' ? '✔️ نعم (موافق)' : '❌ لا') : (val || '-')}
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>

            {/* Moderation status and notes */}
            <div className="space-y-4 border-t border-[var(--admin-border)] pt-5">
              <div className="flex items-center gap-1 text-[var(--admin-text)] font-bold text-sm border-r-2 border-[var(--admin-primary)] pr-2">
                <Settings className="h-4 w-4" />
                <span>إجراء مراجعة الطلب:</span>
              </div>

              <div className="grid gap-4 md:grid-cols-2">
                <div className="space-y-1">
                  <label className="text-xs font-bold text-[var(--admin-text)]">تغيير حالة الطلب</label>
                  <select
                    value={modalStatus}
                    onChange={(e) => setModalStatus(e.target.value as FormSubmissionStatus)}
                    className="admin-input w-full"
                  >
                    <option value="Pending">قيد الانتظار (Pending)</option>
                    <option value="Reviewed">تمت المراجعة (Reviewed)</option>
                    <option value="Accepted">مقبول (Accepted)</option>
                    <option value="Rejected">مرفوض (Rejected)</option>
                  </select>
                </div>
              </div>

              <div className="space-y-1">
                <label className="text-xs font-bold text-[var(--admin-text)]">ملاحظات مراجعة الإدارة (داخلية)</label>
                <textarea
                  rows={3}
                  value={modalNotes}
                  onChange={(e) => setModalNotes(e.target.value)}
                  placeholder="اكتب ملاحظات داخلية حول هذا التقديم ليرى زملائك المشرفين تفاصيل التقييم..."
                  className="admin-input w-full text-sm"
                />
              </div>
            </div>

            {/* Modal Actions */}
            <div className="flex gap-3 justify-end pt-5 border-t border-[var(--admin-border)] w-full">
              <button
                type="button"
                onClick={() => setSelectedSubmission(null)}
                className="admin-btn-ghost"
                disabled={savingStatus}
              >
                إلغاء
              </button>
              <button
                type="button"
                onClick={handleSaveStatus}
                className="admin-btn-primary flex items-center gap-2 disabled:opacity-50"
                disabled={savingStatus}
              >
                <CheckCircle className="h-4 w-4" />
                {savingStatus ? 'جاري الحفظ...' : 'حفظ التعديلات'}
              </button>
            </div>
          </div>
        )}
      </AdminModal>
    </AdminShellChrome>
  );
}
