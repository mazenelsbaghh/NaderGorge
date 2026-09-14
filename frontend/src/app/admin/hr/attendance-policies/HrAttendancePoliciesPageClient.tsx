'use client';

import { FormEvent, useCallback, useEffect, useState } from 'react';
import { MapPin, Plus, RefreshCw, ShieldCheck, Smartphone } from 'lucide-react';
import toast from 'react-hot-toast';
import { AdminPageSkeleton, AdminPage } from '@/components/admin';
import {
  AttendancePolicyAssignmentDto,
  AttendancePolicyDto,
  AttendancePolicyKind,
  EmployeeDto,
  hrService,
  ShiftTemplateDto,
} from '@/services/hr-service';
import { cairoCurrentDate } from '@/lib/cairo-time';

const kindLabels: Record<AttendancePolicyKind, string> = {
  Unrestricted: 'بدون قيود',
  Geofence: 'داخل موقع محدد',
  TrustedDevice: 'جهاز موثوق',
};

const kindIcons = {
  Unrestricted: ShieldCheck,
  Geofence: MapPin,
  TrustedDevice: Smartphone,
};

export default function HrAttendancePoliciesPageClient() {
  const [policies, setPolicies] = useState<AttendancePolicyDto[]>([]);
  const [assignments, setAssignments] = useState<AttendancePolicyAssignmentDto[]>([]);
  const [templates, setTemplates] = useState<ShiftTemplateDto[]>([]);
  const [employees, setEmployees] = useState<EmployeeDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [policyForm, setPolicyForm] = useState({
    code: '', name: '', kind: 'Unrestricted' as AttendancePolicyKind,
    latitude: '', longitude: '', radiusMeters: 150, maximumAccuracyMeters: 100,
  });
  const [assignmentForm, setAssignmentForm] = useState({
    attendancePolicyId: '', targetKind: 'shift' as 'shift' | 'employee',
    targetId: '', effectiveFrom: cairoCurrentDate(), effectiveTo: '',
  });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [configuration, shiftRows, employeeRows] = await Promise.all([
        hrService.getAttendancePolicyConfiguration(),
        hrService.listShiftTemplates(),
        hrService.listEmployees(),
      ]);
      setPolicies(configuration.policies);
      setAssignments(configuration.assignments);
      setTemplates(shiftRows);
      setEmployees(employeeRows);
      setAssignmentForm((current) => ({
        ...current,
        attendancePolicyId: current.attendancePolicyId || configuration.policies.find((item) => item.isActive)?.id || '',
      }));
    } catch {
      toast.error('تعذر تحميل سياسات الحضور');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void load(); }, [load]);

  async function createPolicy(event: FormEvent) {
    event.preventDefault();
    if (policyForm.kind === 'Geofence' && (!policyForm.latitude || !policyForm.longitude)) {
      toast.error('حدد خط العرض وخط الطول لسياسة الموقع');
      return;
    }
    try {
      await hrService.createAttendancePolicy({
        code: policyForm.code,
        name: policyForm.name,
        kind: policyForm.kind,
        latitude: policyForm.kind === 'Geofence' ? Number(policyForm.latitude) : null,
        longitude: policyForm.kind === 'Geofence' ? Number(policyForm.longitude) : null,
        radiusMeters: policyForm.radiusMeters,
        maximumAccuracyMeters: policyForm.maximumAccuracyMeters,
      });
      toast.success('تم إنشاء سياسة الحضور');
      setPolicyForm({ code: '', name: '', kind: 'Unrestricted', latitude: '', longitude: '', radiusMeters: 150, maximumAccuracyMeters: 100 });
      await load();
    } catch {
      toast.error('تعذر إنشاء السياسة؛ تأكد أن الكود غير مستخدم');
    }
  }

  async function assignPolicy(event: FormEvent) {
    event.preventDefault();
    try {
      await hrService.assignAttendancePolicy({
        attendancePolicyId: assignmentForm.attendancePolicyId,
        employeeId: assignmentForm.targetKind === 'employee' ? assignmentForm.targetId : null,
        shiftTemplateId: assignmentForm.targetKind === 'shift' ? assignmentForm.targetId : null,
        effectiveFrom: assignmentForm.effectiveFrom,
        effectiveTo: assignmentForm.effectiveTo || null,
      });
      toast.success('تم تطبيق سياسة الحضور');
      setAssignmentForm((current) => ({ ...current, targetId: '', effectiveTo: '' }));
      await load();
    } catch {
      toast.error('تعذر تطبيق السياسة على الهدف المحدد');
    }
  }

  return <AdminPage
    activePath="/admin/hr/attendance-policies"
    sectionLabel="الموارد البشرية"
    pageTitle="سياسات الحضور"
    subtitle="أنشئ قواعد الحضور واربطها بالشيفت كله أو بموظف محدد. السياسة الخاصة بالموظف لها الأولوية."
    action={<button type="button" onClick={() => void load()} className="admin-btn-primary inline-flex min-h-11 items-center gap-2"><RefreshCw className="h-4 w-4" />تحديث</button>}
  >
    {loading ? <AdminPageSkeleton /> : <div className="space-y-6">
      <section className="grid gap-4 lg:grid-cols-2">
        <form onSubmit={createPolicy} className="admin-panel space-y-4">
          <div><h2 className="text-lg font-black">سياسة جديدة</h2><p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">لو لم تربط سياسة، يظل الشيفت بدون قيود ولا يتعطل تسجيل الحضور.</p></div>
          <div className="grid gap-3 sm:grid-cols-2">
            <label className="text-sm font-bold">اسم السياسة<input required value={policyForm.name} onChange={(e) => setPolicyForm({ ...policyForm, name: e.target.value })} className="admin-input mt-1 w-full" /></label>
            <label className="text-sm font-bold">الكود<input required dir="ltr" value={policyForm.code} onChange={(e) => setPolicyForm({ ...policyForm, code: e.target.value })} className="admin-input mt-1 w-full" /></label>
            <label className="text-sm font-bold sm:col-span-2">نوع السياسة<select value={policyForm.kind} onChange={(e) => setPolicyForm({ ...policyForm, kind: e.target.value as AttendancePolicyKind })} className="admin-input mt-1 w-full">{Object.entries(kindLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
            {policyForm.kind === 'Geofence' && <>
              <label className="text-sm font-bold">خط العرض<input required type="number" step="0.000001" dir="ltr" value={policyForm.latitude} onChange={(e) => setPolicyForm({ ...policyForm, latitude: e.target.value })} className="admin-input mt-1 w-full" /></label>
              <label className="text-sm font-bold">خط الطول<input required type="number" step="0.000001" dir="ltr" value={policyForm.longitude} onChange={(e) => setPolicyForm({ ...policyForm, longitude: e.target.value })} className="admin-input mt-1 w-full" /></label>
              <label className="text-sm font-bold">نطاق الموقع بالمتر<input required type="number" min="10" value={policyForm.radiusMeters} onChange={(e) => setPolicyForm({ ...policyForm, radiusMeters: Number(e.target.value) })} className="admin-input mt-1 w-full" /></label>
              <label className="text-sm font-bold">أقصى دقة GPS بالمتر<input required type="number" min="1" value={policyForm.maximumAccuracyMeters} onChange={(e) => setPolicyForm({ ...policyForm, maximumAccuracyMeters: Number(e.target.value) })} className="admin-input mt-1 w-full" /></label>
            </>}
          </div>
          <button className="admin-btn-primary inline-flex min-h-11 w-full items-center justify-center gap-2"><Plus className="h-4 w-4" />إنشاء السياسة</button>
        </form>

        <form onSubmit={assignPolicy} className="admin-panel space-y-4">
          <div><h2 className="text-lg font-black">تطبيق سياسة</h2><p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">اختر الشيفت لتطبيقها على كل موظفيه، أو موظفًا لاستثناء خاص.</p></div>
          <label className="block text-sm font-bold">السياسة<select required value={assignmentForm.attendancePolicyId} onChange={(e) => setAssignmentForm({ ...assignmentForm, attendancePolicyId: e.target.value })} className="admin-input mt-1 w-full"><option value="">اختر السياسة</option>{policies.filter((item) => item.isActive).map((item) => <option key={item.id} value={item.id}>{item.name} — {kindLabels[item.kind]}</option>)}</select></label>
          <div className="grid grid-cols-2 gap-2 rounded-2xl bg-[var(--admin-bg)] p-1">
            <button type="button" onClick={() => setAssignmentForm({ ...assignmentForm, targetKind: 'shift', targetId: '' })} className={assignmentForm.targetKind === 'shift' ? 'admin-btn-primary min-h-10' : 'admin-btn-secondary min-h-10'}>شيفت كامل</button>
            <button type="button" onClick={() => setAssignmentForm({ ...assignmentForm, targetKind: 'employee', targetId: '' })} className={assignmentForm.targetKind === 'employee' ? 'admin-btn-primary min-h-10' : 'admin-btn-secondary min-h-10'}>موظف محدد</button>
          </div>
          <label className="block text-sm font-bold">{assignmentForm.targetKind === 'shift' ? 'الشيفت' : 'الموظف'}<select required value={assignmentForm.targetId} onChange={(e) => setAssignmentForm({ ...assignmentForm, targetId: e.target.value })} className="admin-input mt-1 w-full"><option value="">اختر</option>{assignmentForm.targetKind === 'shift' ? templates.map((item) => <option key={item.id} value={item.id}>{item.name}</option>) : employees.map((item) => <option key={item.id} value={item.id}>{item.fullName}</option>)}</select></label>
          <div className="grid gap-3 sm:grid-cols-2">
            <label className="text-sm font-bold">سارية من<input required type="date" value={assignmentForm.effectiveFrom} onChange={(e) => setAssignmentForm({ ...assignmentForm, effectiveFrom: e.target.value })} className="admin-input mt-1 w-full" /></label>
            <label className="text-sm font-bold">حتى (اختياري)<input type="date" value={assignmentForm.effectiveTo} onChange={(e) => setAssignmentForm({ ...assignmentForm, effectiveTo: e.target.value })} className="admin-input mt-1 w-full" /></label>
          </div>
          <button className="admin-btn-primary min-h-11 w-full">تطبيق السياسة</button>
        </form>
      </section>

      <section className="space-y-3">
        <h2 className="text-lg font-black">السياسات الحالية</h2>
        {policies.length === 0 ? <div className="admin-panel py-10 text-center text-sm font-bold text-[var(--admin-muted)]">لا توجد سياسات بعد. الشيفتات تعمل افتراضيًا بدون قيود.</div> : <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">{policies.map((policy) => { const Icon = kindIcons[policy.kind]; return <article key={policy.id} className="admin-panel"><div className="flex items-start justify-between gap-3"><div className="flex gap-3"><span className="rounded-xl bg-[var(--admin-primary-15)] p-2 text-[var(--admin-primary)]"><Icon className="h-5 w-5" /></span><div><h3 className="font-black">{policy.name}</h3><p className="text-xs font-bold text-[var(--admin-muted)]">{policy.code}</p></div></div><span className="admin-badge">{kindLabels[policy.kind]}</span></div>{policy.kind === 'Geofence' && <p className="mt-3 text-xs font-bold text-[var(--admin-muted)]">نطاق {policy.radiusMeters}م — دقة مطلوبة {policy.maximumAccuracyMeters}م</p>}</article>; })}</div>}
      </section>

      <section className="space-y-3">
        <h2 className="text-lg font-black">السياسات المطبقة</h2>
        {assignments.length === 0 ? <div className="admin-panel py-10 text-center text-sm font-bold text-[var(--admin-muted)]">لا توجد سياسات مخصصة؛ الحضور متاح بدون قيود.</div> : <div className="grid gap-3 md:grid-cols-2">{assignments.map((item) => <article key={item.id} className="admin-panel"><div className="flex items-center justify-between gap-3"><div><p className="font-black">{item.policy}</p><p className="mt-1 text-sm font-bold text-[var(--admin-primary)]">{item.employee ? `الموظف: ${item.employee}` : `الشيفت: ${item.shift}`}</p></div><span className="admin-badge">{item.effectiveFrom} — {item.effectiveTo ?? 'مستمرة'}</span></div></article>)}</div>}
      </section>
    </div>}
  </AdminPage>;
}
