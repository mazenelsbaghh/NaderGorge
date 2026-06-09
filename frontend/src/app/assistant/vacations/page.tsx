'use client';

import React, { useEffect, useState, useCallback } from 'react';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';
import { VacationRequestModal } from '@/components/admin';
import { hrService, VacationDto } from '@/services/hr-service';
import NeumorphButton from '@/components/ui/neumorph-button';
import { Calendar, Compass, RefreshCw, Clock, CheckCircle, XCircle } from 'lucide-react';
import toast from 'react-hot-toast';

export default function AssistantVacationsPage() {
  const [vacations, setVacations] = useState<VacationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);

  const fetchVacations = useCallback(async () => {
    setLoading(true);
    try {
      const data = await hrService.getMyVacations();
      setVacations(data ?? []);
    } catch {
      toast.error('حدث خطأ أثناء تحميل طلبات الإجازة الخاصة بك');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchVacations();
  }, [fetchVacations]);

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Approved':
        return (
          <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 dark:bg-emerald-950/40 px-2.5 py-0.5 text-xs font-bold text-emerald-700 dark:text-emerald-400">
            <CheckCircle className="h-3.5 w-3.5" />
            مقبول
          </span>
        );
      case 'Rejected':
        return (
          <span className="inline-flex items-center gap-1 rounded-full bg-rose-100 dark:bg-rose-950/40 px-2.5 py-0.5 text-xs font-bold text-rose-700 dark:text-rose-400">
            <XCircle className="h-3.5 w-3.5" />
            مرفوض
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 dark:bg-amber-950/40 px-2.5 py-0.5 text-xs font-bold text-amber-700 dark:text-amber-400">
            <Clock className="h-3.5 w-3.5" />
            قيد الانتظار
          </span>
        );
    }
  };

  const calculateDays = (start: string, end: string) => {
    const s = new Date(start);
    const e = new Date(end);
    const diff = Math.abs(e.getTime() - s.getTime());
    return Math.ceil(diff / (1000 * 60 * 60 * 24)) + 1;
  };

  return (
    <AssistantShellChrome
      activePath="/assistant/vacations"
      sectionLabel="الموارد البشرية"
      pageTitle="طلبات الإجازة"
      subtitle="تقديم ومتابعة طلبات الإجازة السنوية والمرضية الخاصة بك، ومراجعة رصيد الإجازات المتبقي."
      headerAccessory={
        <NeumorphButton onClick={() => setIsModalOpen(true)} intent="primary" size="sm" className="font-bold flex items-center gap-1">
          <Calendar className="h-4 w-4" />
          <span>تقديم طلب إجازة جديد</span>
        </NeumorphButton>
      }
    >
      <div className="mx-auto max-w-5xl space-y-8 text-right animate-[fadeIn_0.4s_ease-out]" dir="rtl">
        {/* Vacation Balance Cards */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          <div className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
            <span className="block text-xs font-black text-[var(--admin-muted)] uppercase">رصيد الإجازات السنوي</span>
            <div className="text-3xl font-black text-[var(--admin-text)] mt-2">21 يوماً</div>
          </div>
          <div className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
            <span className="block text-xs font-black text-[var(--admin-muted)] uppercase">الرصيد المستهلك</span>
            <div className="text-3xl font-black text-rose-500 mt-2">
              {vacations.filter(v => v.status === 'Approved').reduce((acc, v) => acc + calculateDays(v.startDate, v.endDate), 0)} أيام
            </div>
          </div>
          <div className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
            <span className="block text-xs font-black text-[var(--admin-muted)] uppercase">الرصيد المتبقي</span>
            <div className="text-3xl font-black text-emerald-500 mt-2">
              {21 - vacations.filter(v => v.status === 'Approved').reduce((acc, v) => acc + calculateDays(v.startDate, v.endDate), 0)} يوماً
            </div>
          </div>
        </div>

        {/* Requests Table */}
        <div className="space-y-4">
          <div className="flex justify-between items-center">
            <h4 className="text-lg font-black text-[var(--admin-text)]">سجل الطلبات السابقة</h4>
            <NeumorphButton onClick={fetchVacations} disabled={loading} intent="ghost" size="sm" className="flex items-center gap-1 text-xs">
              <RefreshCw className={`h-3.5 w-3.5 ${loading ? 'animate-spin' : ''}`} />
              <span>تحديث</span>
            </NeumorphButton>
          </div>

          {loading && vacations.length === 0 ? (
            <div className="space-y-3">
              {[1, 2].map((n) => (
                <div key={n} className="h-16 w-full animate-pulse rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)]" />
              ))}
            </div>
          ) : vacations.length === 0 ? (
            <div className="flex flex-col items-center justify-center rounded-3xl border border-dashed border-[var(--admin-border)] p-12 text-center text-[var(--admin-muted)] bg-[var(--admin-card-soft)]">
              <Compass className="h-12 w-12 text-[var(--admin-border)] mb-4" />
              <h5 className="font-bold text-[var(--admin-text)]">لا توجد طلبات إجازة</h5>
              <p className="text-xs mt-1">لم تقم بتقديم أي طلبات إجازة حتى الآن.</p>
            </div>
          ) : (
            <div className="overflow-hidden rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-sm">
              <div className="overflow-x-auto">
                <table className="w-full border-collapse text-right text-sm">
                  <thead>
                    <tr className="border-b border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-xs font-bold text-[var(--admin-muted)]">
                      <th className="px-5 py-4">تاريخ البدء</th>
                      <th className="px-5 py-4">تاريخ الانتهاء</th>
                      <th className="px-5 py-4">الأيام</th>
                      <th className="px-5 py-4">السبب</th>
                      <th className="px-5 py-4">حالة الطلب</th>
                      <th className="px-5 py-4">المراجع</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--admin-border)] text-[var(--admin-text)]">
                    {vacations.map((vac) => (
                      <tr key={vac.id} className="group hover:bg-[var(--admin-hover)] transition-colors">
                        <td className="px-5 py-4 font-bold font-mono">{vac.startDate}</td>
                        <td className="px-5 py-4 font-bold font-mono">{vac.endDate}</td>
                        <td className="px-5 py-4 font-bold font-mono">{calculateDays(vac.startDate, vac.endDate)} أيام</td>
                        <td className="px-5 py-4 text-xs font-medium max-w-[200px] truncate" title={vac.reason}>{vac.reason}</td>
                        <td className="px-5 py-4">{getStatusBadge(vac.status)}</td>
                        <td className="px-5 py-4 text-xs font-bold text-[var(--admin-muted)]">
                          {vac.handledByName ? (
                            <span>
                              {vac.handledByName}
                              <span className="block text-[9px] font-mono font-normal">{vac.handledAt ? new Date(vac.handledAt).toLocaleDateString('ar-EG') : ''}</span>
                            </span>
                          ) : '—'}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      </div>

      <VacationRequestModal
        open={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSuccess={fetchVacations}
      />
    </AssistantShellChrome>
  );
}
