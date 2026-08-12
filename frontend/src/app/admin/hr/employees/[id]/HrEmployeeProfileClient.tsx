'use client';

import { useCallback, useEffect, useState } from 'react';
import { BriefcaseBusiness, CalendarDays, FileText, MapPin, RefreshCw, Trash2, UserRound } from 'lucide-react';
import { useRouter } from 'next/navigation';
import toast from 'react-hot-toast';
import { AdminConfirmationDialog, AdminPageSkeleton, AdminPage } from '@/components/admin';
import { EmployeeDetailDto, hrService } from '@/services/hr-service';
import { formatCairoDateTime } from '@/lib/cairo-time';

const date = (value?: string | null) => {
  if (!value) return 'مفتوح';

  try {
    return formatCairoDateTime(value);
  } catch (error) {
    if (!(error instanceof RangeError)) throw error;

    // Legacy records can contain an invalid date. Keep the employee profile usable.
    return 'غير محدد';
  }
};

export default function HrEmployeeProfileClient({ employeeId }: { employeeId: string }) {
  const router = useRouter();
  const [employee, setEmployee] = useState<EmployeeDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const load = useCallback(async () => { setLoading(true); setError(false); try { setEmployee(await hrService.getEmployeeDetail(employeeId)); } catch { setError(true); } finally { setLoading(false); } }, [employeeId]);
  useEffect(() => { void load(); }, [load]);

  // Older employee records can predate assignments/contracts. Do not let one
  // incomplete record crash the full admin route and trigger its error boundary.
  const assignments = Array.isArray(employee?.assignments) ? employee.assignments : [];
  const contracts = Array.isArray(employee?.contracts) ? employee.contracts : [];
  async function deleteEmployee() {
    setDeleting(true);
    try { await hrService.deleteEmployee(employeeId); toast.success('تم حذف الموظف نهائيًا'); router.push('/admin/hr/organization'); }
    catch { toast.error('لا يمكن حذف الموظف لأنه مرتبط بسجل تشغيلي أو مالي.'); }
    finally { setDeleting(false); setDeleteOpen(false); }
  }

  return <AdminPage activePath="/admin/hr/employees" sectionLabel="الموارد البشرية" pageTitle={employee?.fullName ?? 'ملف الموظف'} subtitle="الهوية الوظيفية، سجل التعيينات، والعقود المؤرخة." action={<div className="flex gap-2"><button type="button" onClick={() => void load()} className="admin-btn-primary inline-flex min-h-11 items-center gap-2"><RefreshCw className="h-4 w-4" />تحديث</button><button type="button" onClick={() => setDeleteOpen(true)} className="admin-btn-danger inline-flex min-h-11 items-center gap-2"><Trash2 className="h-4 w-4" />حذف الموظف</button></div>}>
    {loading ? <AdminPageSkeleton /> : error || !employee ? <div className="admin-panel py-14 text-center"><p className="font-black text-red-600">تعذر تحميل ملف الموظف أو لا تملك الصلاحية.</p></div> : <div className="space-y-6">
      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">{[
        [UserRound, 'الرقم الوظيفي', employee.employeeNumber], [BriefcaseBusiness, 'الحالة', employee.employmentStatus], [CalendarDays, 'تاريخ التعيين', date(employee.hireDate)], [MapPin, 'نظام العمل', employee.workMode],
      ].map(([Icon, label, value]) => { const Glyph = Icon as typeof UserRound; return <article key={String(label)} className="admin-panel"><Glyph className="h-5 w-5 text-[var(--admin-primary)]" /><p className="mt-4 text-xs font-bold text-[var(--admin-muted)]">{String(label)}</p><p className="mt-1 font-black">{String(value)}</p></article>; })}</section>
      <section className="space-y-3"><h2 className="text-lg font-black">سجل التعيينات</h2>{assignments.length === 0 ? <div className="admin-panel py-10 text-center text-sm font-bold text-[var(--admin-muted)]">لا توجد تعيينات مسجلة.</div> : assignments.map((assignment) => <article key={assignment.id} className="admin-panel"><div className="flex flex-wrap items-start justify-between gap-3"><div><p className="font-black">{assignment.position ?? 'وظيفة غير محددة'} · {assignment.organizationUnit}</p><p className="mt-1 text-sm font-bold text-[var(--admin-muted)]">المدير: {assignment.manager ?? 'غير محدد'} · الموقع: {assignment.location ?? 'غير محدد'}</p></div><span className="admin-badge">{date(assignment.effectiveFrom)} — {date(assignment.effectiveTo)}</span></div><p className="mt-3 text-sm">{assignment.changeReason}</p></article>)}</section>
      <section className="space-y-3"><h2 className="text-lg font-black">العقود</h2>{contracts.length === 0 ? <div className="admin-panel py-10 text-center text-sm font-bold text-[var(--admin-muted)]">لا توجد عقود مسجلة.</div> : contracts.map((contract) => <article key={contract.id} className="admin-panel flex flex-wrap items-center justify-between gap-4"><div className="flex items-center gap-3"><span className="flex h-11 w-11 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]"><FileText className="h-5 w-5" /></span><div><p className="font-black">{contract.contractNumber} · {contract.type}</p><p className="text-sm font-bold text-[var(--admin-muted)]">{date(contract.startDate)} — {date(contract.endDate)} · نسخة {contract.termsVersion}</p></div></div><span className="admin-badge">{contract.status}</span></article>)}</section>
    </div>}
    <AdminConfirmationDialog open={deleteOpen} onClose={() => setDeleteOpen(false)} onConfirm={() => void deleteEmployee()} title="حذف الموظف نهائيًا" consequence="سيُزال ملف الموظف والشفتات غير المرتبطة بسجل حضور. لا يمكن التراجع عن هذا الإجراء." confirmLabel="حذف نهائي" variant="danger" isConfirming={deleting} />
  </AdminPage>;
}
