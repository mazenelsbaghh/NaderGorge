'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { AlertTriangle, RotateCcw, Save, Sparkles } from 'lucide-react';
import toast from 'react-hot-toast';

import type { PackageCodeProfileDto, PackageCodeProfilePayload, PackageCodeProfileStatus } from '@/services/admin-service';
import { adminService } from '@/services/admin-service';
import NeumorphButton from '@/components/ui/neumorph-button';

const THEME_OPTIONS = [
  { value: 'default-gold', label: 'ذهبي افتراضي' },
  { value: 'physics-gold', label: 'ذهبي أكاديمي' },
  { value: 'emerald-accent', label: 'زمردي هادئ' },
  { value: 'ocean-accent', label: 'أزرق بحري' },
] as const;

export type PackageCodeProfileSummary = {
  status: PackageCodeProfileStatus;
  isUsingFallback: boolean;
  publishedAt?: string | null;
};

type PackageCodeProfileFormProps = {
  packageId: string;
  packageName: string;
  onProfileStateChange?: (summary: PackageCodeProfileSummary) => void;
};

type FormState = PackageCodeProfilePayload;

function mapDtoToForm(dto: PackageCodeProfileDto): FormState {
  return {
    status: dto.status,
    heroEyebrow: dto.heroEyebrow,
    heroTitle: dto.heroTitle,
    heroDescription: dto.heroDescription,
    offerTitle: dto.offerTitle,
    offerDescription: dto.offerDescription,
    activationTitle: dto.activationTitle,
    activationDescription: dto.activationDescription,
    supportTitle: dto.supportTitle,
    supportDescription: dto.supportDescription,
    themeAccentKey: dto.themeAccentKey,
  };
}

export function PackageCodeProfileForm({
  packageId,
  packageName,
  onProfileStateChange,
}: PackageCodeProfileFormProps) {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [resetting, setResetting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [profile, setProfile] = useState<PackageCodeProfileDto | null>(null);
  const [form, setForm] = useState<FormState>({
    status: 'Draft',
    themeAccentKey: 'default-gold',
  });

  const loadProfile = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await adminService.getPackageCodeProfile(packageId);
      setProfile(data);
      setForm(mapDtoToForm(data));
      onProfileStateChange?.({
        status: data.status,
        isUsingFallback: data.isUsingFallback,
        publishedAt: data.publishedAt,
      });
    } catch {
      setError('تعذر تحميل بروفايل صفحة الكود لهذه الباقة.');
    } finally {
      setLoading(false);
    }
  }, [onProfileStateChange, packageId]);

  useEffect(() => {
    void loadProfile();
  }, [loadProfile]);

  const statusLabel = useMemo(() => {
    switch (profile?.status) {
      case 'Published':
        return 'منشور';
      case 'Draft':
        return 'مسودة';
      default:
        return 'افتراضي';
    }
  }, [profile?.status]);

  function updateField<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  async function handleSave(e: React.FormEvent) {
    e.preventDefault();

    try {
      setSaving(true);
      setError(null);
      await adminService.upsertPackageCodeProfile(packageId, form);
      toast.success('تم حفظ بروفايل صفحة الكود بنجاح.');
      await loadProfile();
    } catch (err: any) {
      const message =
        err?.response?.data?.errors?.[0] ??
        err?.response?.data?.message ??
        'حدث خطأ أثناء حفظ بروفايل صفحة الكود.';
      setError(message);
      toast.error(message);
    } finally {
      setSaving(false);
    }
  }

  async function handleReset() {
    try {
      setResetting(true);
      setError(null);
      await adminService.resetPackageCodeProfile(packageId);
      toast.success('تمت إعادة الصفحة إلى الوضع الافتراضي.');
      await loadProfile();
    } catch (err: any) {
      const message = err?.response?.data?.message ?? 'تعذر إعادة الصفحة إلى الوضع الافتراضي.';
      setError(message);
      toast.error(message);
    } finally {
      setResetting(false);
    }
  }

  if (loading) {
    return (
      <div className="space-y-4">
        <div className="h-24 animate-pulse rounded-[28px] bg-[var(--admin-card-soft)]" />
        <div className="grid gap-4 md:grid-cols-2">
          <div className="h-32 animate-pulse rounded-[24px] bg-[var(--admin-card-soft)]" />
          <div className="h-32 animate-pulse rounded-[24px] bg-[var(--admin-card-soft)]" />
        </div>
        <div className="h-48 animate-pulse rounded-[24px] bg-[var(--admin-card-soft)]" />
      </div>
    );
  }

  return (
    <form onSubmit={handleSave} className="space-y-6">
      <div className="rounded-[28px] bg-[var(--admin-card-soft)] p-5">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <p className="text-xs font-black uppercase tracking-[0.24em] text-[var(--admin-primary)]">
              Code Page Profile
            </p>
            <h4 className="mt-2 text-2xl font-black text-[var(--admin-text)]">
              تخصيص صفحة الأكواد لـ {packageName}
            </h4>
            <p className="mt-2 max-w-3xl text-sm leading-7 text-[var(--admin-muted)]">
              عدّل الرسائل الأساسية التي يراها الطالب عند فتح صفحة تفعيل كود هذه الباقة. النشر فقط هو الذي يظهر للطلاب، أما المسودة فتظل داخل لوحة الإدارة.
            </p>
          </div>

          <div className="rounded-[22px] bg-[var(--admin-card)] px-4 py-3 text-sm font-bold text-[var(--admin-text)] shadow-sm">
            الحالة الحالية: <span className="text-[var(--admin-primary)]">{statusLabel}</span>
          </div>
        </div>
      </div>

      {error ? (
        <div className="flex items-start gap-3 rounded-[24px] border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-4 text-sm font-semibold text-[var(--admin-danger)]">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <span>{error}</span>
        </div>
      ) : null}

      <section className="grid gap-6 lg:grid-cols-[0.75fr_1.25fr]">
        <div className="space-y-6">
          <div className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-5">
            <label className="mb-2 block text-sm font-bold text-[var(--admin-text)]">حالة البروفايل</label>
            <select
              value={form.status}
              onChange={(e) => updateField('status', e.target.value as PackageCodeProfileStatus)}
              className="admin-input"
            >
              <option value="Draft">مسودة</option>
              <option value="Published">منشور</option>
            </select>
            <p className="mt-2 text-xs leading-6 text-[var(--admin-muted)]">
              عند اختيار &quot;منشور&quot; يجب أن تكون الحقول الأساسية مكتملة، وإلا سيرفض الحفظ.
            </p>
          </div>

          <div className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-5">
            <label className="mb-2 block text-sm font-bold text-[var(--admin-text)]">لون التمييز</label>
            <select
              value={form.themeAccentKey ?? 'default-gold'}
              onChange={(e) => updateField('themeAccentKey', e.target.value)}
              className="admin-input"
            >
              {THEME_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>

          <div className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-5">
            <p className="text-sm font-bold text-[var(--admin-text)]">الوضع الافتراضي</p>
            <p className="mt-2 text-sm leading-7 text-[var(--admin-muted)]">
              إذا أردت حذف التخصيصات الحالية والرجوع للنسخة الافتراضية العامة، استخدم زر الإعادة بالأسفل.
            </p>
          </div>
        </div>

        <div className="space-y-6">
          <FieldGroup
            title="القسم الافتتاحي"
            icon={<Sparkles className="h-4 w-4" />}
            fields={
              <>
                <InputField label="الوسم الصغير" value={form.heroEyebrow ?? ''} onChange={(value) => updateField('heroEyebrow', value)} />
                <InputField label="العنوان الرئيسي" value={form.heroTitle ?? ''} onChange={(value) => updateField('heroTitle', value)} />
                <TextareaField label="الوصف الرئيسي" value={form.heroDescription ?? ''} onChange={(value) => updateField('heroDescription', value)} rows={4} />
              </>
            }
          />

          <FieldGroup
            title="رسالة ما بعد التفعيل"
            fields={
              <>
                <InputField label="عنوان العرض" value={form.offerTitle ?? ''} onChange={(value) => updateField('offerTitle', value)} />
                <TextareaField label="وصف العرض" value={form.offerDescription ?? ''} onChange={(value) => updateField('offerDescription', value)} rows={4} />
              </>
            }
          />

          <FieldGroup
            title="قسم إدخال الكود"
            fields={
              <>
                <InputField label="عنوان التفعيل" value={form.activationTitle ?? ''} onChange={(value) => updateField('activationTitle', value)} />
                <TextareaField label="وصف التفعيل" value={form.activationDescription ?? ''} onChange={(value) => updateField('activationDescription', value)} rows={4} />
              </>
            }
          />

          <FieldGroup
            title="قسم المساعدة"
            fields={
              <>
                <InputField label="عنوان المساعدة" value={form.supportTitle ?? ''} onChange={(value) => updateField('supportTitle', value)} />
                <TextareaField label="وصف المساعدة" value={form.supportDescription ?? ''} onChange={(value) => updateField('supportDescription', value)} rows={3} />
              </>
            }
          />
        </div>
      </section>

      <div className="flex flex-col gap-3 border-t border-[var(--admin-border)] pt-6 sm:flex-row sm:items-center sm:justify-between">
        <NeumorphButton
          type="button"
          intent="ghost"
          pill
          disabled={resetting}
          loading={resetting}
          className="px-6"
          onClick={handleReset}
        >
          <RotateCcw className="h-4 w-4" />
          إعادة للوضع الافتراضي
        </NeumorphButton>

        <NeumorphButton
          type="submit"
          intent="primary"
          pill
          disabled={saving}
          loading={saving}
          className="px-8"
        >
          <Save className="h-4 w-4" />
          حفظ البروفايل
        </NeumorphButton>
      </div>
    </form>
  );
}

function FieldGroup({
  title,
  icon,
  fields,
}: {
  title: string;
  icon?: ReactNode;
  fields: ReactNode;
}) {
  return (
    <section className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-5">
      <div className="mb-4 flex items-center gap-2 text-lg font-black text-[var(--admin-text)]">
        {icon}
        <span>{title}</span>
      </div>
      <div className="space-y-4">{fields}</div>
    </section>
  );
}

function InputField({
  label,
  value,
  onChange,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <div className="space-y-2">
      <label className="text-sm font-bold text-[var(--admin-text)]">{label}</label>
      <input value={value} onChange={(e) => onChange(e.target.value)} className="admin-input" />
    </div>
  );
}

function TextareaField({
  label,
  value,
  rows,
  onChange,
}: {
  label: string;
  value: string;
  rows: number;
  onChange: (value: string) => void;
}) {
  return (
    <div className="space-y-2">
      <label className="text-sm font-bold text-[var(--admin-text)]">{label}</label>
      <textarea rows={rows} value={value} onChange={(e) => onChange(e.target.value)} className="admin-input" />
    </div>
  );
}
