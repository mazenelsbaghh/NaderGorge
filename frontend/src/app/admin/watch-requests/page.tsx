'use client';

import { useState, useEffect } from 'react';
import { adminService } from '@/services/admin-service';
import { Check, X, Clock, AlertCircle } from 'lucide-react';
import { formatRelativeDate } from '@/components/admin/admin-utils';
import { 
  AdminShellChrome, 
  AdminDataTable, 
  AdminColumn,
  AdminPageSkeleton,
  AdminStatCard 
} from '@/components/admin';
import NeumorphButton from '@/components/ui/neumorph-button';
import toast from 'react-hot-toast';

export default function WatchRequestsPage() {
  const [requests, setRequests] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [error, setError] = useState('');

  useEffect(() => {
    fetchRequests();
  }, []);

  const fetchRequests = async () => {
    try {
      setLoading(true);
      setError('');
      const response = await adminService.getWatchRequests();
      setRequests(response.data || []);
    } catch (err: any) {
      console.error(err);
      setError('فشل في تحميل الطلبات.');
    } finally {
      setLoading(false);
    }
  };

  const handleApprove = async (id: string) => {
    setActionLoading(id);
    try {
      await adminService.approveWatchRequest(id);
      await fetchRequests();
      toast.success('تم قبول طلب المشاهدة الإضافية.');
    } catch (err) {
      console.error(err);
      toast.error('فشل في الموافقة على الطلب');
    } finally {
      setActionLoading(null);
    }
  };

  const handleReject = async (id: string) => {
    setActionLoading(id);
    try {
      await adminService.rejectWatchRequest(id);
      await fetchRequests();
      toast.success('تم رفض طلب المشاهدة الإضافية.');
    } catch (err) {
      console.error(err);
      toast.error('فشل في رفض الطلب');
    } finally {
      setActionLoading(null);
    }
  };

  const pendingCount = requests.filter(r => r.status === 0).length;
  const approvedCount = requests.filter(r => r.status === 1).length;
  const rejectedCount = requests.filter(r => r.status === 2).length;

  const columns: AdminColumn<any>[] = [
    {
      key: 'student',
      label: 'الطالب',
      render: (req) => (
        <div>
          <div className="font-bold text-[var(--admin-text)]">{req.studentName}</div>
          <div className="text-xs text-[var(--admin-muted)] mt-1 font-mono">{req.studentPhone}</div>
        </div>
      )
    },
    {
      key: 'video',
      label: 'الفيديو',
      render: (req) => (
        <div className="text-sm font-bold text-[var(--admin-text)] max-w-sm truncate whitespace-normal leading-relaxed">
          {req.videoTitle}
        </div>
      )
    },
    {
      key: 'date',
      label: 'التاريخ',
      render: (req) => (
        <span className="text-sm text-[var(--admin-muted)] font-medium">
          {formatRelativeDate(req.createdAt)}
        </span>
      )
    },
    {
      key: 'status',
      label: 'الحالة',
      render: (req) => {
        if (req.status === 0) {
          return (
            <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-bold bg-yellow-500/10 text-yellow-600 dark:text-yellow-500">
              <Clock className="w-3.5 h-3.5 ml-1.5" /> قيد المراجعة
            </span>
          );
        }
        if (req.status === 1) {
          return (
            <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-bold bg-emerald-500/10 text-emerald-600 dark:text-emerald-500">
              <Check className="w-3.5 h-3.5 ml-1.5" /> تمت الموافقة
            </span>
          );
        }
        return (
          <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-bold bg-red-500/10 text-red-600 dark:text-red-500">
            <X className="w-3.5 h-3.5 ml-1.5" /> مرفوض
          </span>
        );
      }
    },
    {
      key: 'actions',
      label: 'الإجراءات',
      align: 'left',
      render: (req) => (
        req.status === 0 ? (
          <div className="flex items-center justify-end gap-2">
            <NeumorphButton
              type="button"
              onClick={() => handleApprove(req.id)}
              disabled={actionLoading !== null}
              intent="primary"
              size="sm"
            >
              {actionLoading === req.id ? (
                <span className="w-4 h-4 block border-2 border-emerald-500 border-t-transparent rounded-full animate-spin"></span>
              ) : (
                <Check className="w-4 h-4 ml-1" />
              )}
              موافقة
            </NeumorphButton>
            <NeumorphButton
              type="button"
              onClick={() => handleReject(req.id)}
              disabled={actionLoading !== null}
              intent="danger"
              size="sm"
            >
              {actionLoading === req.id ? (
                <span className="w-4 h-4 block border-2 border-red-500 border-t-transparent rounded-full animate-spin"></span>
              ) : (
                <X className="w-4 h-4 ml-1" />
              )}
              رفض
            </NeumorphButton>
          </div>
        ) : (
          <span className="text-[var(--admin-muted)] text-sm text-left w-full block ml-8 opacity-50">-</span>
        )
      )
    }
  ];

  return (
    <AdminShellChrome
      activePath="/admin/watch-requests"
      sectionLabel="المحتوى الأكاديمي"
      pageTitle="طلبات المشاهدة الإضافية"
      subtitle="مراجعة ومعالجة طلبات الطلاب لزيادة مرات مشاهدة الفيديوهات المقفلة."
    >
      {error && (
        <div className="mb-6 bg-red-500/10 border border-red-500 text-red-500 p-4 rounded-xl flex items-center shadow-sm">
          <AlertCircle className="w-5 h-5 ml-2" />
          <span className="font-bold">{error}</span>
        </div>
      )}

      {loading ? (
        <AdminPageSkeleton />
      ) : (
        <>
          <section className="mb-10 grid grid-cols-1 gap-6 md:grid-cols-3">
            <AdminStatCard
              variant="accent"
              icon={Clock}
              label="طلبات جديدة"
              value={pendingCount}
              subtitle="لم يتم الرد عليها بعد"
            />
            <AdminStatCard
              variant="light"
              icon={Check}
              label="تمت الموافقة"
              value={approvedCount}
              subtitle="الطلبات المقبولة"
            />
            <AdminStatCard
              variant="muted"
              icon={X}
              label="الطلبات المرفوضة"
              value={rejectedCount}
              subtitle="تم رفضها لعدم الاستحقاق"
            />
          </section>

          <AdminDataTable
            data={requests}
            columns={columns}
            loading={loading}
            rowKey={(r) => r.id}
            emptyMessage="لا توجد طلبات مشاهدة إضافية حالياً."
          />
        </>
      )}
    </AdminShellChrome>
  );
}
