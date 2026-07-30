'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { ArrowRight, BadgePercent, CalendarClock, RefreshCcw, Save, Target, Users } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { AdminShellChrome } from '@/components/admin';
import { AcademicScopeSelector } from '@/components/admin/AcademicScopeSelector';
import { getAcademicScopeLabel, type AcademicScopePayload } from '@/lib/academic-labels';
import {
  adminSalesService,
  type DiscountType,
  type SalesCouponDto,
  type SalesOwnerType,
  type SalesStatus,
  type SalesTargetType,
  type StackingPolicyDto,
} from '@/services/admin-sales-service';
import { teacherService, type SubjectDto, type TeacherDto } from '@/services/teacher-service';
import { invalidateMany } from '@/lib/cache-invalidation';
import { cairoDateTimeLocalToIso, formatCairoDateTimeLocal } from '@/components/admin/admin-utils';

type CouponProfilePageClientProps = {
  couponId: string;
};

type CouponForm = {
  code: string;
  name: string;
  discountType: DiscountType;
  discountValue: string;
  targetType: SalesTargetType;
  targetId: string;
  ownerType: SalesOwnerType;
  teacherId: string;
  stackingPolicyId: string;
  startsAt: string;
  expiresAt: string;
  globalUsageLimit: string;
  perStudentUsageLimit: string;
  status: SalesStatus;
  academicScopes: AcademicScopePayload[];
};

const targetLabels: Record<SalesTargetType, string> = {
  Platform: 'كل المنصة',
  Teacher: 'مدرس',
  Package: 'باكدج',
  Term: 'ترم',
  ContentSection: 'شهر / قسم',
  Lesson: 'حصة',
  SpecificVideo: 'فيديو محدد',
  VideoType: 'نوع فيديو',
  PublicExam: 'امتحان عام',
};

const statusLabels: Record<SalesStatus, string> = {
  Draft: 'مسودة',
  Active: 'نشط',
  Disabled: 'متوقف',
  Expired: 'منتهي',
  Archived: 'مؤرشف',
  Consumed: 'مستهلك',
};

function toInputDate(value?: string | null) {
  if (!value) return '';
  return formatCairoDateTimeLocal(value);
}

function formatDate(value?: string | null) {
  if (!value) return 'غير محدد';
  return new Intl.DateTimeFormat('ar-EG', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Africa/Cairo' }).format(new Date(value));
}

function toForm(coupon: SalesCouponDto): CouponForm {
  return {
    code: coupon.code,
    name: coupon.name,
    discountType: coupon.discountType,
    discountValue: String(coupon.discountValue ?? ''),
    targetType: coupon.targetType,
    targetId: coupon.targetId ?? '',
    ownerType: coupon.ownerType,
    teacherId: coupon.teacherId ?? '',
    stackingPolicyId: coupon.stackingPolicyId ?? '',
    startsAt: toInputDate(coupon.startsAt),
    expiresAt: toInputDate(coupon.expiresAt),
    globalUsageLimit: coupon.globalUsageLimit == null ? '' : String(coupon.globalUsageLimit),
    perStudentUsageLimit: coupon.perStudentUsageLimit == null ? '' : String(coupon.perStudentUsageLimit),
    status: coupon.status,
    academicScopes: coupon.academicScopes?.map((scope) => ({
      scopeLevel: scope.scopeLevel,
      educationStage: scope.educationStage ?? null,
      gradeLevel: scope.gradeLevel ?? null,
      subjectId: scope.subjectId ?? null,
    })) ?? [{ scopeLevel: 'GradeAllSubjects', educationStage: 'Secondary', gradeLevel: 'FirstSecondary' }],
  };
}

function toPayload(form: CouponForm) {
  return {
    ...form,
    discountValue: Number(form.discountValue || 0),
    targetId: form.targetId || null,
    teacherId: form.teacherId || null,
    stackingPolicyId: form.stackingPolicyId || null,
    startsAt: form.startsAt ? cairoDateTimeLocalToIso(form.startsAt) : null,
    expiresAt: form.expiresAt ? cairoDateTimeLocalToIso(form.expiresAt) : null,
    globalUsageLimit: form.globalUsageLimit ? Number(form.globalUsageLimit) : null,
    perStudentUsageLimit: form.perStudentUsageLimit ? Number(form.perStudentUsageLimit) : null,
    academicScopes: form.academicScopes,
  };
}

export default function CouponProfilePageClient({ couponId }: CouponProfilePageClientProps) {
  const [coupon, setCoupon] = useState<SalesCouponDto | null>(null);
  const [form, setForm] = useState<CouponForm | null>(null);
  const [policies, setPolicies] = useState<StackingPolicyDto[]>([]);
  const [teachers, setTeachers] = useState<TeacherDto[]>([]);
  const [subjects, setSubjects] = useState<SubjectDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState('');

  const teacherNames = useMemo(() => Object.fromEntries(teachers.map((teacher) => [teacher.id, teacher.fullName])), [teachers]);
  const policyNames = useMemo(() => Object.fromEntries(policies.map((policy) => [policy.id, policy.name])), [policies]);

  const load = useCallback(async () => {
    setLoading(true);
    setMessage('');
    try {
      const [nextCoupon, nextPolicies, nextTeachers, nextSubjects] = await Promise.all([
        adminSalesService.coupon(couponId),
        adminSalesService.stackingPolicies().catch(() => []),
        teacherService.getTeachers().catch(() => ({ success: true, data: [] as TeacherDto[] })),
        teacherService.getSubjects().catch(() => ({ success: true, data: [] as SubjectDto[] })),
      ]);
      setCoupon(nextCoupon);
      setForm(toForm(nextCoupon));
      setPolicies(nextPolicies);
      setTeachers(nextTeachers.data ?? []);
      setSubjects(nextSubjects.data ?? []);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'تعذر تحميل الكوبون.');
    } finally {
      setLoading(false);
    }
  }, [couponId]);

  useEffect(() => {
    void load();
  }, [load]);

  async function save() {
    if (!form) return;
    setSaving(true);
    setMessage('');
    try {
      const updated = await adminSalesService.updateCoupon(couponId, toPayload(form));
      invalidateMany(['reports']);
      setCoupon(updated);
      setForm(toForm(updated));
      setMessage('تم تحديث الكوبون.');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'فشل تحديث الكوبون.');
    } finally {
      setSaving(false);
    }
  }

  const discountText = coupon ? `${coupon.discountValue}${coupon.discountType === 'Percentage' ? '%' : ' ج.م'}` : '...';

  return (
    <AdminShellChrome
      activePath="/admin/sales"
      sectionLabel="الخصومات"
      pageTitle={coupon ? `كوبون ${coupon.code}` : 'بروفايل الكوبون'}
      subtitle="كل بيانات الكوبون، حالته، استخدامه، وإمكانية تعديله من نفس الصفحة."
      action={
        <div className="flex flex-wrap gap-2">
          <Link href="/admin/sales" className="inline-flex items-center gap-2 rounded-md border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)]">
            <ArrowRight className="h-4 w-4" />
            رجوع
          </Link>
          <button onClick={load} disabled={loading || saving} className="inline-flex items-center gap-2 rounded-md border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] disabled:opacity-60">
            <RefreshCcw className="h-4 w-4" />
            تحديث
          </button>
        </div>
      }
    >
      <div className="space-y-5">
        {message && <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm font-bold text-amber-900">{message}</div>}
        {loading || !coupon || !form ? (
          <div className="h-72 animate-pulse rounded-lg bg-[var(--admin-card-strong)]" />
        ) : (
          <>
            <section className="overflow-hidden rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)]">
              <div className="grid gap-5 bg-[linear-gradient(135deg,var(--admin-primary),var(--admin-primary-strong))] p-5 text-[var(--admin-primary-contrast)] lg:grid-cols-[minmax(0,1fr)_260px]">
                <div>
                  <div className="inline-flex items-center gap-2 rounded-full bg-white/15 px-3 py-1 text-xs font-black">
                    <BadgePercent className="h-4 w-4" />
                    كوبون خصم
                  </div>
                  <h2 className="mt-4 text-3xl font-black">{coupon.code}</h2>
                  <p className="mt-2 text-sm font-bold opacity-85">{coupon.name || 'بدون اسم'}</p>
                </div>
                <div className="rounded-xl border border-white/20 bg-white/10 p-4">
                  <p className="text-xs font-bold opacity-80">قيمة الخصم</p>
                  <p className="mt-2 text-4xl font-black">{discountText}</p>
                  <p className="mt-3 text-xs font-bold opacity-80">الحالة: {statusLabels[coupon.status]}</p>
                </div>
              </div>
              <div className="grid gap-px bg-[var(--admin-border)] md:grid-cols-4">
                <Metric icon={Users} label="مرات الاستخدام" value={coupon.usedCount} />
                <Metric icon={Target} label="هدف الخصم" value={targetLabels[coupon.targetType]} />
                <Metric icon={CalendarClock} label="يبدأ" value={formatDate(coupon.startsAt)} />
                <Metric icon={CalendarClock} label="ينتهي" value={formatDate(coupon.expiresAt)} />
              </div>
            </section>

            <section className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_380px]">
              <div className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5">
                <div className="mb-5 flex items-center justify-between gap-3">
                  <h2 className="text-lg font-black text-[var(--admin-text)]">تعديل بيانات الكوبون</h2>
                  <button onClick={save} disabled={saving} className="inline-flex items-center gap-2 rounded-md bg-[var(--admin-primary)] px-3 py-2 text-sm font-bold text-white hover:opacity-90 disabled:opacity-60">
                    <Save className="h-4 w-4" />
                    حفظ
                  </button>
                </div>
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="الكود" value={form.code} onChange={(value) => setForm({ ...form, code: value })} />
                  <Field label="الاسم" value={form.name} onChange={(value) => setForm({ ...form, name: value })} />
                  <Select label="نوع الخصم" value={form.discountType} onChange={(value) => setForm({ ...form, discountType: value as DiscountType })} options={[['Percentage', 'نسبة'], ['FixedAmount', 'مبلغ ثابت']]} />
                  <Field label="قيمة الخصم" type="number" value={form.discountValue} onChange={(value) => setForm({ ...form, discountValue: value })} />
                  <Select label="هدف الخصم" value={form.targetType} onChange={(value) => setForm({ ...form, targetType: value as SalesTargetType, targetId: '' })} options={Object.entries(targetLabels) as Array<[string, string]>} />
                  <Field label="Target ID عند الحاجة" value={form.targetId} onChange={(value) => setForm({ ...form, targetId: value })} />
                  <Select label="مالك البيع" value={form.ownerType} onChange={(value) => setForm({ ...form, ownerType: value as SalesOwnerType })} options={[['Platform', 'المنصة'], ['Teacher', 'المدرس']]} />
                  <Select label="المدرس" value={form.teacherId} onChange={(value) => setForm({ ...form, teacherId: value })} options={[['', 'بدون مدرس'], ...teachers.map((teacher) => [teacher.id, teacher.fullName] as [string, string])]} />
                  <Select label="سياسة الدمج" value={form.stackingPolicyId} onChange={(value) => setForm({ ...form, stackingPolicyId: value })} options={[['', 'بدون سياسة محددة'], ...policies.map((policy) => [policy.id, policy.name] as [string, string])]} />
                  <Select label="الحالة" value={form.status} onChange={(value) => setForm({ ...form, status: value as SalesStatus })} options={Object.entries(statusLabels) as Array<[string, string]>} />
                  <Field label="حد الاستخدام الكلي" type="number" value={form.globalUsageLimit} onChange={(value) => setForm({ ...form, globalUsageLimit: value })} />
                  <Field label="حد استخدام الطالب" type="number" value={form.perStudentUsageLimit} onChange={(value) => setForm({ ...form, perStudentUsageLimit: value })} />
                  <Field label="تاريخ البداية" type="datetime-local" value={form.startsAt} onChange={(value) => setForm({ ...form, startsAt: value })} />
                  <Field label="تاريخ الانتهاء" type="datetime-local" value={form.expiresAt} onChange={(value) => setForm({ ...form, expiresAt: value })} />
                </div>
                <div className="mt-5 border-t border-[var(--admin-border)] pt-5">
                  <h3 className="text-sm font-black text-[var(--admin-text)]">نطاق استخدام الكوبون</h3>
                  <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">لن يقبل الكوبون إلا إذا طابق أحد هذه النطاقات الطالب وقت الاستخدام.</p>
                  <div className="mt-3">
                    <AcademicScopeSelector value={form.academicScopes} onChange={(academicScopes) => setForm({ ...form, academicScopes })} subjects={subjects} />
                  </div>
                </div>
              </div>

              <aside className="space-y-5">
                {coupon.academicScopes?.length ? (
                  <div className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
                    <p className="text-xs font-bold text-[var(--admin-muted)]">النطاقات الأكاديمية</p>
                    <div className="mt-2 flex flex-wrap gap-1.5">
                      {coupon.academicScopes.map((scope, index) => (
                        <span key={`${coupon.id}-scope-${index}`} className="rounded-full bg-[var(--admin-primary-15)] px-2 py-1 text-[11px] font-black text-[var(--admin-primary)]">
                          {getAcademicScopeLabel(scope)}
                        </span>
                      ))}
                    </div>
                  </div>
                ) : null}
                <InfoCard label="المدرس" value={coupon.teacherId ? teacherNames[coupon.teacherId] ?? coupon.teacherId : 'المنصة'} />
                <InfoCard label="سياسة الدمج" value={coupon.stackingPolicyId ? policyNames[coupon.stackingPolicyId] ?? coupon.stackingPolicyId : 'بدون سياسة محددة'} />
                <InfoCard label="حد الاستخدام الكلي" value={coupon.globalUsageLimit ?? 'غير محدود'} />
                <InfoCard label="حد استخدام الطالب" value={coupon.perStudentUsageLimit ?? 'غير محدود'} />
                <InfoCard label="سبب التعطيل" value={coupon.disableReason || 'لا يوجد'} />
                <InfoCard label="تم الإنشاء" value={formatDate(coupon.createdAt)} />
                <InfoCard label="آخر تعديل" value={formatDate(coupon.updatedAt)} />
              </aside>
            </section>

            <section className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5">
              <h2 className="mb-4 text-lg font-black text-[var(--admin-text)]">آخر استخدامات الكوبون</h2>
              {coupon.recentUsages?.length ? (
                <div className="overflow-x-auto">
                  <table className="w-full min-w-[760px] text-sm">
                    <thead className="text-right text-xs font-black text-[var(--admin-muted)]">
                      <tr className="border-b border-[var(--admin-border)]">
                        <th className="py-2">الطالب</th>
                        <th className="py-2">الهدف</th>
                        <th className="py-2">السعر</th>
                        <th className="py-2">قيمة الخصم</th>
                        <th className="py-2">التاريخ</th>
                      </tr>
                    </thead>
                    <tbody className="font-bold text-[var(--admin-text)]">
                      {coupon.recentUsages.map((usage) => (
                        <tr key={usage.id} className="border-b border-[var(--admin-border)] last:border-0">
                          <td className="py-3">{usage.studentName}</td>
                          <td className="py-3">{targetLabels[usage.targetType]} - {usage.targetId}</td>
                          <td className="py-3">{usage.grossAmount} ج.م</td>
                          <td className="py-3 text-[var(--admin-success)]">-{usage.discountAmount} ج.م</td>
                          <td className="py-3">{formatDate(usage.createdAt)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <p className="rounded-lg border border-dashed border-[var(--admin-border)] p-6 text-center text-sm font-bold text-[var(--admin-muted)]">لم يتم استخدام الكوبون بعد.</p>
              )}
            </section>
          </>
        )}
      </div>
    </AdminShellChrome>
  );
}

function Metric({ icon: Icon, label, value }: { icon: LucideIcon; label: string; value: string | number }) {
  return (
    <div className="flex items-center gap-3 bg-[var(--admin-card)] p-4">
      <span className="grid h-10 w-10 place-items-center rounded-lg bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
        <Icon className="h-5 w-5" />
      </span>
      <div className="min-w-0">
        <p className="text-xs font-bold text-[var(--admin-muted)]">{label}</p>
        <p className="mt-1 truncate text-sm font-black text-[var(--admin-text)]">{value}</p>
      </div>
    </div>
  );
}

function InfoCard({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
      <p className="text-xs font-bold text-[var(--admin-muted)]">{label}</p>
      <p className="mt-2 break-words text-sm font-black text-[var(--admin-text)]">{value}</p>
    </div>
  );
}

function Field({ label, value, onChange, type = 'text' }: { label: string; value: string; onChange: (value: string) => void; type?: string }) {
  return (
    <label className="grid gap-1 text-sm">
      <span className="font-bold text-[var(--admin-muted)]">{label}</span>
      <input type={type} value={value} onChange={(event) => onChange(event.target.value)} className="rounded-md border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)]" />
    </label>
  );
}

function Select({ label, value, onChange, options }: { label: string; value: string; onChange: (value: string) => void; options: Array<[string, string]> }) {
  return (
    <label className="grid gap-1 text-sm">
      <span className="font-bold text-[var(--admin-muted)]">{label}</span>
      <select value={value} onChange={(event) => onChange(event.target.value)} className="rounded-md border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)]">
        {options.map(([optionValue, labelText]) => <option key={optionValue || labelText} value={optionValue}>{labelText}</option>)}
      </select>
    </label>
  );
}
