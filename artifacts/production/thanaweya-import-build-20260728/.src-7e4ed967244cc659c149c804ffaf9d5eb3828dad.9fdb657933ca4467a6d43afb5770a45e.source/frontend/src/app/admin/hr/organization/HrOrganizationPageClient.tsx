'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { Building2, ChevronLeft, RefreshCw, Search, UserRound } from 'lucide-react';
import { AdminPageSkeleton, AdminShellChrome } from '@/components/admin';
import { EmployeeDto, hrService, OrganizationUnitDto } from '@/services/hr-service';

function UnitBranch({ unit, all, depth = 0 }: { unit: OrganizationUnitDto; all: OrganizationUnitDto[]; depth?: number }) {
  const children = all.filter((item) => item.parentId === unit.id);
  return (
    <li className="space-y-2">
      <div className="admin-panel flex min-h-14 items-center justify-between gap-3" style={{ marginInlineStart: `${Math.min(depth, 4) * 18}px` }}>
        <div className="flex items-center gap-3">
          <span className="flex h-10 w-10 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]"><Building2 className="h-5 w-5" /></span>
          <div><p className="font-black text-[var(--admin-text)]">{unit.name}</p><p className="text-xs font-bold text-[var(--admin-muted)]">{unit.code} · {unit.type}</p></div>
        </div>
        <span className="admin-badge">{children.length} وحدة فرعية</span>
      </div>
      {children.length > 0 && <ul className="space-y-2">{children.map((child) => <UnitBranch key={child.id} unit={child} all={all} depth={depth + 1} />)}</ul>}
    </li>
  );
}

export default function HrOrganizationPageClient() {
  const [units, setUnits] = useState<OrganizationUnitDto[]>([]);
  const [employees, setEmployees] = useState<EmployeeDto[]>([]);
  const [query, setQuery] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const load = useCallback(async () => {
    setLoading(true); setError(false);
    try { const [unitRows, employeeRows] = await Promise.all([hrService.listOrganizationUnits(), hrService.listEmployees()]); setUnits(unitRows); setEmployees(employeeRows); }
    catch { setError(true); } finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); }, [load]);
  const roots = useMemo(() => units.filter((item) => !item.parentId), [units]);
  const filteredEmployees = employees.filter((item) => `${item.fullName} ${item.phoneNumber} ${item.employeeProfile?.employeeNumber ?? ''}`.toLowerCase().includes(query.toLowerCase()));

  return <AdminShellChrome activePath="/admin/hr/organization" sectionLabel="الموارد البشرية" pageTitle="الهيكل الإداري" subtitle="الوحدات التنظيمية وملفات الموظفين من مصدر حقيقة واحد." action={<button type="button" onClick={() => void load()} className="admin-btn-primary inline-flex min-h-11 items-center gap-2"><RefreshCw className="h-4 w-4" />تحديث</button>}>
    {loading ? <AdminPageSkeleton /> : error ? <div className="admin-panel py-14 text-center"><p className="font-black text-red-600">تعذر تحميل الهيكل الإداري.</p><button type="button" onClick={() => void load()} className="admin-btn-primary mt-4">إعادة المحاولة</button></div> : <div className="grid gap-6 xl:grid-cols-[1.1fr_.9fr]">
      <section className="space-y-4"><h2 className="text-lg font-black">شجرة الوحدات</h2>{roots.length === 0 ? <div className="admin-panel py-14 text-center text-sm font-bold text-[var(--admin-muted)]">لا توجد وحدات تنظيمية بعد.</div> : <ul className="space-y-3">{roots.map((root) => <UnitBranch key={root.id} unit={root} all={units} />)}</ul>}</section>
      <section className="space-y-4"><div className="flex items-center justify-between"><h2 className="text-lg font-black">ملفات الموظفين</h2><span className="admin-badge">{employees.length}</span></div><label className="admin-panel flex min-h-12 items-center gap-2 py-2"><Search className="h-4 w-4 text-[var(--admin-muted)]" /><span className="sr-only">بحث في الموظفين</span><input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="الاسم أو الهاتف أو الرقم الوظيفي" className="w-full bg-transparent text-sm outline-none" /></label><div className="space-y-2">{filteredEmployees.length === 0 ? <div className="admin-panel py-12 text-center text-sm font-bold text-[var(--admin-muted)]">لا يوجد موظفون مطابقون.</div> : filteredEmployees.map((employee) => <Link key={employee.id} href={`/admin/hr/employees/${employee.id}`} className="admin-panel flex min-h-16 items-center justify-between gap-3 transition hover:-translate-y-0.5"><div className="flex items-center gap-3"><span className="flex h-10 w-10 items-center justify-center rounded-2xl bg-[var(--admin-card-soft)]"><UserRound className="h-5 w-5" /></span><div><p className="font-black">{employee.fullName}</p><p className="text-xs font-bold text-[var(--admin-muted)]">{employee.employeeProfile?.employeeNumber ?? employee.phoneNumber}</p></div></div><ChevronLeft className="h-4 w-4" /></Link>)}</div></section>
    </div>}
  </AdminShellChrome>;
}
