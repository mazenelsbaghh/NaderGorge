'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { BadgePercent, Eye, RefreshCcw, Save, Settings2 } from 'lucide-react';
import { AdminPage } from '@/components/admin';
import { adminSalesService, SalesCouponDto, SalesTargetType, StackingPolicyDto } from '@/services/admin-sales-service';
import { adminService, type VideoTypeDto } from '@/services/admin-service';
import { contentService, type ContentSectionDto, type LessonSummaryDto, type PackageDto, type TermDto } from '@/services/content-service';
import { teacherService, type TeacherDto } from '@/services/teacher-service';
import { invalidateMany } from '@/lib/cache-invalidation';
import { cairoDateTimeLocalToUtcISOString } from '@/lib/cairo-time';

type DiscountType = 'Percentage' | 'FixedAmount';

type CouponForm = {
  code: string;
  name: string;
  discountType: DiscountType;
  discountValue: string;
  targetType: SalesTargetType;
  targetId: string;
  ownerType: 'Platform' | 'Teacher';
  teacherId: string;
  stackingPolicyId: string;
  startsAt: string;
  expiresAt: string;
  globalUsageLimit: string;
  perStudentUsageLimit: string;
  status: 'Draft' | 'Active';
};

export default function AdminSalesPageClient() {
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState('');
  const [policies, setPolicies] = useState<StackingPolicyDto[]>([]);
  const [coupons, setCoupons] = useState<SalesCouponDto[]>([]);
  const [teachers, setTeachers] = useState<TeacherDto[]>([]);
  const [packages, setPackages] = useState<PackageDto[]>([]);
  const [videoTypes, setVideoTypes] = useState<VideoTypeDto[]>([]);
  const [publicExams, setPublicExams] = useState<Array<{ id: string; examTitle: string }>>([]);

  const [policyForm, setPolicyForm] = useState({
    name: 'السياسة الافتراضية',
    mode: 'AllowCouponAndPrintedCode',
    maxDiscountPercentage: '100',
    maxDiscountAmount: '',
    priorityJson: '[]',
    isDefault: true,
    isActive: true,
  });

  const [couponForm, setCouponForm] = useState<CouponForm>({
    code: '',
    name: '',
    discountType: 'Percentage',
    discountValue: '10',
    targetType: 'Platform',
    targetId: '',
    ownerType: 'Platform',
    teacherId: '',
    stackingPolicyId: '',
    startsAt: '',
    expiresAt: '',
    globalUsageLimit: '',
    perStudentUsageLimit: '',
    status: 'Active',
  });

  async function load() {
    setLoading(true);
    setMessage('');
    try {
      const [nextPolicies, nextCoupons, nextTeachers, packagesResponse, nextVideoTypes, nextPublicExams] = await Promise.all([
        adminSalesService.stackingPolicies(),
        adminSalesService.coupons(),
        teacherService.getTeachers().catch(() => ({ success: true, data: [] as TeacherDto[] })),
        contentService.getPackages(),
        adminService.listVideoTypes(true).catch(() => [] as VideoTypeDto[]),
        adminSalesService.publicExams().catch(() => []),
      ]);
      setPolicies(nextPolicies);
      setCoupons(nextCoupons);
      setTeachers(nextTeachers.data ?? []);
      setPackages((packagesResponse.data?.data ?? []) as PackageDto[]);
      setVideoTypes(nextVideoTypes);
      setPublicExams(nextPublicExams.map((exam) => ({ id: exam.id, examTitle: exam.examTitle })));
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'تعذر تحميل بيانات الخصومات.');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  async function savePolicy() {
    setLoading(true);
    setMessage('');
    try {
      await adminSalesService.saveStackingPolicy(toPayload(policyForm));
      invalidateMany(['reports']);
      setMessage('تم حفظ سياسة الخصم.');
      await load();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'فشل حفظ سياسة الخصم.');
    } finally {
      setLoading(false);
    }
  }

  async function createCoupon() {
    setLoading(true);
    setMessage('');
    try {
      if ((couponForm.ownerType === 'Teacher' || couponForm.targetType === 'Teacher') && !couponForm.teacherId) {
        setMessage('اختيار المدرس مطلوب عندما يكون الخصم تابعاً لمدرس.');
        return;
      }
      await adminSalesService.createCoupon(toPayload(couponForm));
      invalidateMany(['reports']);
      setMessage('تم إنشاء كوبون الخصم.');
      setCouponForm((current) => ({ ...current, code: '', name: '' }));
      await load();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'فشل إنشاء كوبون الخصم.');
    } finally {
      setLoading(false);
    }
  }

  const teacherNames = useMemo(() => Object.fromEntries(teachers.map((teacher) => [teacher.id, teacher.fullName])), [teachers]);

  return (
    <AdminPage
      activePath="/admin/sales"
      sectionLabel="الخصومات"
      pageTitle="الخصومات والكوبونات"
      subtitle="إدارة سياسات الخصم والكوبونات مع اختيار المدرس والهدف من قوائم واضحة بدون كتابة IDs يدوياً."
      action={
        <button onClick={load} disabled={loading} className="inline-flex items-center gap-2 rounded-md border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] disabled:opacity-60">
          <RefreshCcw className="h-4 w-4" />
          تحديث
        </button>
      }
    >
      <div className="space-y-5">
        {message && <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm font-bold text-amber-900">{message}</div>}

        <section className="grid gap-4 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 lg:grid-cols-[minmax(0,1fr)_360px]">
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="sm:col-span-2 flex items-center justify-between gap-3">
              <h2 className="inline-flex items-center gap-2 text-lg font-black text-[var(--admin-text)]">
                <Settings2 className="h-5 w-5" />
                سياسة دمج الخصومات
              </h2>
              <SaveButton onClick={savePolicy} loading={loading} />
            </div>
            <Field label="الاسم" value={policyForm.name} onChange={(v) => setPolicyForm({ ...policyForm, name: v })} />
            <Select label="طريقة الدمج" value={policyForm.mode} onChange={(v) => setPolicyForm({ ...policyForm, mode: v })} options={[
              ['SingleOnly', 'خصم واحد فقط'],
              ['AllowCouponAndPrintedCode', 'كوبون + كود مطبوع'],
              ['AllowMultipleWithCap', 'أكثر من خصم بسقف'],
            ]} />
            <Field label="أقصى نسبة خصم" type="number" value={policyForm.maxDiscountPercentage} onChange={(v) => setPolicyForm({ ...policyForm, maxDiscountPercentage: v })} />
            <Field label="أقصى قيمة خصم" type="number" value={policyForm.maxDiscountAmount} onChange={(v) => setPolicyForm({ ...policyForm, maxDiscountAmount: v })} />
            <Toggle label="افتراضية" checked={policyForm.isDefault} onChange={(v) => setPolicyForm({ ...policyForm, isDefault: v })} />
          </div>
          <List rows={policies.map((p) => `${p.name} - ${p.mode} - ${p.isDefault ? 'افتراضية' : 'عادية'}`)} empty="لا توجد سياسات بعد." />
        </section>

        <section className="grid gap-4 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 lg:grid-cols-[minmax(0,1fr)_420px]">
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="sm:col-span-2 flex items-center justify-between gap-3">
              <h2 className="inline-flex items-center gap-2 text-lg font-black text-[var(--admin-text)]">
                <BadgePercent className="h-5 w-5" />
                كوبون خصم
              </h2>
              <SaveButton onClick={createCoupon} loading={loading} label="إنشاء" />
            </div>
            <Field label="الكود" value={couponForm.code} onChange={(v) => setCouponForm({ ...couponForm, code: v })} />
            <Field label="الاسم" value={couponForm.name} onChange={(v) => setCouponForm({ ...couponForm, name: v })} />
            <Select label="نوع الخصم" value={couponForm.discountType} onChange={(v) => setCouponForm({ ...couponForm, discountType: v as DiscountType })} options={[
              ['Percentage', 'نسبة'],
              ['FixedAmount', 'قيمة ثابتة'],
            ]} />
            <Field label="قيمة الخصم" type="number" value={couponForm.discountValue} onChange={(v) => setCouponForm({ ...couponForm, discountValue: v })} />
            <TargetPicker
              form={couponForm}
              setForm={setCouponForm}
              teachers={teachers}
              packages={packages}
              videoTypes={videoTypes}
              publicExams={publicExams}
            />
            <Select label="مالك البيع" value={couponForm.ownerType} onChange={(v) => setCouponForm({ ...couponForm, ownerType: v as CouponForm['ownerType'] })} options={[
              ['Platform', 'المنصة'],
              ['Teacher', 'المدرس'],
            ]} />
            <TeacherSelect label="المدرس" value={couponForm.teacherId} onChange={(v) => setCouponForm({ ...couponForm, teacherId: v })} teachers={teachers} />
            <Select label="سياسة الدمج" value={couponForm.stackingPolicyId} onChange={(v) => setCouponForm({ ...couponForm, stackingPolicyId: v })} options={[
              ['', 'بدون سياسة محددة'],
              ...policies.map((policy) => [policy.id, policy.name] as [string, string]),
            ]} />
            <Field label="حد الاستخدام الكلي" type="number" value={couponForm.globalUsageLimit} onChange={(v) => setCouponForm({ ...couponForm, globalUsageLimit: v })} />
            <Field label="حد استخدام الطالب" type="number" value={couponForm.perStudentUsageLimit} onChange={(v) => setCouponForm({ ...couponForm, perStudentUsageLimit: v })} />
            <Field label="تاريخ البداية" type="datetime-local" value={couponForm.startsAt} onChange={(v) => setCouponForm({ ...couponForm, startsAt: v })} />
            <Field label="تاريخ الانتهاء" type="datetime-local" value={couponForm.expiresAt} onChange={(v) => setCouponForm({ ...couponForm, expiresAt: v })} />
          </div>
          <div className="rounded-md border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3">
            {coupons.length === 0 ? (
              <p className="text-sm font-bold text-[var(--admin-muted)]">لا توجد كوبونات بعد.</p>
            ) : (
              <div className="grid gap-2">
                {coupons.map((coupon) => (
                  <Link
                    key={coupon.id}
                    href={`/admin/sales/${coupon.id}`}
                    className="group rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-3 text-sm transition hover:border-[var(--admin-primary)] hover:bg-[var(--admin-hover)]"
                  >
                    <div className="flex items-center justify-between gap-3">
                      <div className="min-w-0">
                        <p className="truncate font-black text-[var(--admin-text)]">{coupon.code}</p>
                        <p className="mt-1 truncate text-xs font-bold text-[var(--admin-muted)]">
                          {coupon.name || 'بدون اسم'} - {coupon.teacherId ? teacherNames[coupon.teacherId] ?? coupon.teacherId : 'المنصة'}
                        </p>
                      </div>
                      <span className="inline-flex items-center gap-1 rounded-full bg-[var(--admin-primary-15)] px-2 py-1 text-xs font-black text-[var(--admin-primary)]">
                        <Eye className="h-3.5 w-3.5" />
                        فتح
                      </span>
                    </div>
                    <div className="mt-3 flex flex-wrap gap-2 text-xs font-bold text-[var(--admin-muted)]">
                      <span>{coupon.discountValue}{coupon.discountType === 'Percentage' ? '%' : ' جنيه'}</span>
                      <span>{coupon.status}</span>
                      <span>استخدام: {coupon.usedCount}</span>
                    </div>
                  </Link>
                ))}
              </div>
            )}
          </div>
        </section>
      </div>
    </AdminPage>
  );
}

function TargetPicker({
  form,
  setForm,
  teachers,
  packages,
  videoTypes,
  publicExams,
}: {
  form: CouponForm;
  setForm: (form: CouponForm) => void;
  teachers: TeacherDto[];
  packages: PackageDto[];
  videoTypes: VideoTypeDto[];
  publicExams: Array<{ id: string; examTitle: string }>;
}) {
  const [terms, setTerms] = useState<TermDto[]>([]);
  const [sections, setSections] = useState<ContentSectionDto[]>([]);
  const [lessons, setLessons] = useState<LessonSummaryDto[]>([]);
  const [selectedPackageId, setSelectedPackageId] = useState('');
  const [selectedTermId, setSelectedTermId] = useState('');
  const [selectedSectionId, setSelectedSectionId] = useState('');

  const loadTerms = async (packageId: string) => {
    setTerms([]);
    setSections([]);
    setLessons([]);
    if (!packageId) return;
    const res = await contentService.getTerms(packageId);
    setTerms((res.data?.data ?? []) as TermDto[]);
  };

  const loadSections = async (termId: string) => {
    setSections([]);
    setLessons([]);
    if (!termId) return;
    const res = await contentService.getSections(termId);
    setSections((res.data?.data ?? []) as ContentSectionDto[]);
  };

  const loadLessons = async (sectionId: string) => {
    setLessons([]);
    if (!sectionId) return;
    const res = await contentService.getLessons(sectionId);
    setLessons((res.data?.data ?? []) as LessonSummaryDto[]);
  };

  const setTargetType = (targetType: SalesTargetType) => {
    setTerms([]);
    setSections([]);
    setLessons([]);
    setSelectedPackageId('');
    setSelectedTermId('');
    setSelectedSectionId('');
    setForm({ ...form, targetType, targetId: '' });
  };

  const handlePackageChange = (packageId: string) => {
    setSelectedPackageId(packageId);
    setSelectedTermId('');
    setSelectedSectionId('');
    setForm({ ...form, targetId: form.targetType === 'Package' ? packageId : '' });
    void loadTerms(packageId);
  };

  const handleTermChange = (termId: string) => {
    setSelectedTermId(termId);
    setSelectedSectionId('');
    setForm({ ...form, targetId: form.targetType === 'Term' ? termId : '' });
    void loadSections(termId);
  };

  const handleSectionChange = (sectionId: string) => {
    setSelectedSectionId(sectionId);
    setForm({ ...form, targetId: form.targetType === 'ContentSection' ? sectionId : '' });
    void loadLessons(sectionId);
  };

  return (
    <div className="sm:col-span-2 grid gap-3 rounded-md border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3 sm:grid-cols-2">
      <Select label="هدف الخصم" value={form.targetType} onChange={(v) => setTargetType(v as SalesTargetType)} options={[
        ['Platform', 'كل المنصة'],
        ['Teacher', 'مدرس'],
        ['Package', 'باكدج'],
        ['Term', 'ترم'],
        ['ContentSection', 'شهر / قسم'],
        ['Lesson', 'حصة'],
        ['VideoType', 'نوع فيديو'],
        ['PublicExam', 'امتحان عام'],
      ]} />

      {form.targetType === 'Teacher' && (
        <TeacherSelect label="اختر المدرس" value={form.teacherId} onChange={(v) => setForm({ ...form, teacherId: v, targetId: '' })} teachers={teachers} />
      )}

      {['Package', 'Term', 'ContentSection', 'Lesson'].includes(form.targetType) && (
        <Select
          label="اختر الباكدج"
          value={form.targetType === 'Package' ? form.targetId : selectedPackageId}
          onChange={handlePackageChange}
          options={[['', 'اختر الباكدج'], ...packages.map((pkg) => [pkg.id, pkg.name] as [string, string])]}
        />
      )}

      {['Term', 'ContentSection', 'Lesson'].includes(form.targetType) && (
        <Select
          label="اختر الترم"
          value={form.targetType === 'Term' ? form.targetId : selectedTermId}
          onChange={handleTermChange}
          options={[['', 'اختر الترم'], ...terms.map((term) => [term.id, term.title] as [string, string])]}
        />
      )}

      {['ContentSection', 'Lesson'].includes(form.targetType) && (
        <Select
          label="اختر الشهر / القسم"
          value={form.targetType === 'ContentSection' ? form.targetId : selectedSectionId}
          onChange={handleSectionChange}
          options={[['', 'اختر القسم'], ...sections.map((section) => [section.id, section.title] as [string, string])]}
        />
      )}

      {form.targetType === 'Lesson' && (
        <Select label="اختر الحصة" value={form.targetId} onChange={(v) => setForm({ ...form, targetId: v })} options={[['', 'اختر الحصة'], ...lessons.map((lesson) => [lesson.id, lesson.title] as [string, string])]} />
      )}

      {form.targetType === 'VideoType' && (
        <Select label="اختر نوع الفيديو" value={form.targetId} onChange={(v) => setForm({ ...form, targetId: v })} options={[['', 'اختر نوع الفيديو'], ...videoTypes.map((type) => [type.id, type.name] as [string, string])]} />
      )}

      {form.targetType === 'PublicExam' && (
        <Select label="اختر الامتحان العام" value={form.targetId} onChange={(v) => setForm({ ...form, targetId: v })} options={[['', 'اختر الامتحان العام'], ...publicExams.map((exam) => [exam.id, exam.examTitle] as [string, string])]} />
      )}
    </div>
  );
}

function SaveButton({ onClick, loading, label = 'حفظ' }: { onClick: () => void; loading: boolean; label?: string }) {
  return (
    <button onClick={onClick} disabled={loading} className="inline-flex items-center gap-2 rounded-md bg-[var(--admin-primary)] px-3 py-2 text-sm font-bold text-white hover:opacity-90 disabled:opacity-60">
      <Save className="h-4 w-4" />
      {label}
    </button>
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

function TeacherSelect({ label, value, onChange, teachers }: { label: string; value: string; onChange: (value: string) => void; teachers: TeacherDto[] }) {
  return <Select label={label} value={value} onChange={onChange} options={[['', 'اختر المدرس'], ...teachers.map((teacher) => [teacher.id, teacher.fullName] as [string, string])]} />;
}

function Toggle({ label, checked, onChange }: { label: string; checked: boolean; onChange: (value: boolean) => void }) {
  return (
    <label className="flex items-center gap-2 rounded-md border border-[var(--admin-border)] px-3 py-2 text-sm font-bold text-[var(--admin-text)]">
      <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} />
      {label}
    </label>
  );
}

function List({ rows, empty }: { rows: string[]; empty: string }) {
  return (
    <div className="rounded-md border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3">
      {rows.length === 0 ? (
        <p className="text-sm font-bold text-[var(--admin-muted)]">{empty}</p>
      ) : (
        <ul className="grid gap-2 text-sm text-[var(--admin-text)]">
          {rows.map((row, index) => <li key={`${row}-${index}`} className="rounded border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2">{row}</li>)}
        </ul>
      )}
    </div>
  );
}

function toPayload(form: Record<string, unknown>) {
  return Object.fromEntries(
    Object.entries(form).map(([key, value]) => {
      if (value === '') return [key, null];
      if (typeof value === 'string' && ['discountValue', 'maxDiscountPercentage', 'maxDiscountAmount', 'globalUsageLimit', 'perStudentUsageLimit'].includes(key)) {
        return [key, value === '' ? null : Number(value)];
      }
      if (typeof value === 'string' && ['startsAt', 'expiresAt'].includes(key)) return [key, cairoDateTimeLocalToUtcISOString(value)];
      return [key, value];
    }),
  );
}
