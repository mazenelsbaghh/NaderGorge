'use client';

import { useCallback, useEffect, useState } from 'react';
import { BriefcaseBusiness, CalendarDays, FileText, MapPin, RefreshCw, UserRound } from 'lucide-react';
import { AdminPageSkeleton, AdminPage } from '@/components/admin';
import { EmployeeDetailDto, hrService } from '@/services/hr-service';
import { formatCairoDateTime } from '@/lib/cairo-time';

const date = (value?: string | null) => value ? formatCairoDateTime(value) : 'مفتوح';

export default function HrEmployeeProfileClient({ employeeId }: { employeeId: string }) {
  const [employee, setEmployee] = useState<EmployeeDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const load = useCallback(async () => { setLoading(true); setError(false); try { setEmployee(await hrService.getEmployeeDetail(employeeId)); } catch { setError(true); } finally { setLoading(false); } }, [employeeId]);
  useEffect(() => { void load(); }, [load]);

  return <AdminPage activePath="/admin/hr/employees" sectionLabel="الموارد البشرية" pageTitle={employee?.fullName ?? 'ملف الموظف'} subtitle="الهوية الوظيفية، سجل التعيينات، والعقود المؤرخة." action={<button type="button" onClick={() => void load()} className="admin-btn-primary inline-flex min-h-11 items-center gap-2"><RefreshCw className="h-4 w-4" />تحديث</button>}>
    {loading ? <AdminPageSkeleton /> : error || !employee ? <div className="admin-panel py-14 text-center"><p className="font-black text-red-600">تعذر تحميل ملف الموظف أو لا تملك الصلاحية.</p></div> : <div className="space-y-6">
      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">{[
        [UserRound, 'الرقم الوظيفي', employee.employeeNumber], [BriefcaseBusiness, 'الحالة', employee.employmentStatus], [CalendarDays, 'تاريخ التعيين', date(employee.hireDate)], [MapPin, 'نظام العمل', employee.workMode],
      ].map(([Icon, label, value]) => { const Glyph = Icon as typeof UserRound; return <article key={String(label)} className="admin-panel"><Glyph className="h-5 w-5 text-[var(--admin-primary)]" /><p className="mt-4 text-xs font-bold text-[var(--admin-muted)]">{String(label)}</p><p className="mt-1 font-black">{String(value)}</p></article>; })}</section>
      <section className="space-y-3"><h2 className="text-lg font-black">سجل التعيينات</h2>{employee.assignments.length === 0 ? <div className="admin-panel py-10 text-center text-sm font-bold text-[var(--admin-muted)]">لا توجد تعيينات مسجلة.</div> : employee.assignments.map((assignment) => <article key={assignment.id} className="admin-panel"><div className="flex flex-wrap items-start justify-between gap-3"><div><p className="font-black">{assignment.position ?? 'وظيفة غير محددة'} · {assignment.organizationUnit}</p><p className="mt-1 text-sm font-bold text-[var(--admin-muted)]">المدير: {assignment.manager ?? 'غير محدد'} · الموقع: {assignment.location ?? 'غير محدد'}</p></div><span className="admin-badge">{date(assignment.effectiveFrom)} — {date(assignment.effectiveTo)}</span></div><p className="mt-3 text-sm">{assignment.changeReason}</p></article>)}</section>
      <section className="space-y-3"><h2 className="text-lg font-black">العقود</h2>{employee.contracts.length === 0 ? <div className="admin-panel py-10 text-center text-sm font-bold text-[var(--admin-muted)]">لا توجد عقود مسجلة.</div> : employee.contracts.map((contract) => <article key={contract.id} className="admin-panel flex flex-wrap items-center justify-between gap-4"><div className="flex items-center gap-3"><span className="flex h-11 w-11 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]"><FileText className="h-5 w-5" /></span><div><p className="font-black">{contract.contractNumber} · {contract.type}</p><p className="text-sm font-bold text-[var(--admin-muted)]">{date(contract.startDate)} — {date(contract.endDate)} · نسخة {contract.termsVersion}</p></div></div><span className="admin-badge">{contract.status}</span></article>)}</section>
    </div>}
  </AdminPage>;
}
