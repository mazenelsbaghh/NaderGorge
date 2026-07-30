'use client';

import { ChangeEvent, useCallback, useEffect, useState } from 'react';
import { isAxiosError } from 'axios';
import { Check, ExternalLink, ImageUp, LoaderCircle, Save, Sparkles, Upload, X } from 'lucide-react';
import toast from 'react-hot-toast';

import { AdminPage } from '@/components/admin';
import { adminService } from '@/services/admin-service';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import { cairoDateTimeLocalToIso, formatCairoDateTimeLocal } from '@/components/admin/admin-utils';

type PopupSettings = {
  enabled: string;
  title: string;
  body: string;
  imageUrl: string;
  actionUrl: string;
  actionLabel: string;
  displayInterval: string;
  expiresAt: string;
};

const defaults: PopupSettings = {
  enabled: 'false',
  title: '',
  body: '',
  imageUrl: '',
  actionUrl: '',
  actionLabel: 'فتح الرابط',
  displayInterval: '0',
  expiresAt: '',
};

const popupSettingKeys = {
  enabled: 'PlatformPopupEnabled',
  title: 'PlatformPopupTitle',
  body: 'PlatformPopupBody',
  imageUrl: 'PlatformPopupImageUrl',
  actionUrl: 'PlatformPopupActionUrl',
  actionLabel: 'PlatformPopupActionLabel',
  displayInterval: 'PlatformPopupDisplayInterval',
  expiresAt: 'PlatformPopupExpiresAt',
} as const;

function toDateTimeLocal(value: string) {
  if (!value) return '';
  return formatCairoDateTimeLocal(value);
}

function toUtcIso(value: string) {
  if (!value) return '';
  return cairoDateTimeLocalToIso(value);
}

function isValidActionUrl(actionUrl: string) {
  if (!actionUrl) return true;
  if (actionUrl.startsWith('/')) return true;

  try {
    const url = new URL(actionUrl);
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch (error) {
    if (error instanceof TypeError) return false;
    throw error;
  }
}

function readPopupSettings(storedSettings: Array<{ key: string; value: string }>): PopupSettings {
  const settingsByKey = storedSettings.reduce<Record<string, string>>((valuesByKey, storedSetting) => {
    valuesByKey[storedSetting.key] = storedSetting.value;
    return valuesByKey;
  }, {});

  return {
    enabled: settingsByKey[popupSettingKeys.enabled] ?? defaults.enabled,
    title: settingsByKey[popupSettingKeys.title] ?? defaults.title,
    body: settingsByKey[popupSettingKeys.body] ?? defaults.body,
    imageUrl: settingsByKey[popupSettingKeys.imageUrl] ?? defaults.imageUrl,
    actionUrl: settingsByKey[popupSettingKeys.actionUrl] ?? defaults.actionUrl,
    actionLabel: settingsByKey[popupSettingKeys.actionLabel] ?? defaults.actionLabel,
    displayInterval: settingsByKey[popupSettingKeys.displayInterval] ?? defaults.displayInterval,
    expiresAt: toDateTimeLocal(settingsByKey[popupSettingKeys.expiresAt] ?? ''),
  };
}

function validateImageUpload(file: File) {
  return ['image/jpeg', 'image/png', 'image/webp'].includes(file.type)
    ? null
    : 'ارفع صورة بصيغة JPG أو PNG أو WebP.';
}

function getPopupValidationMessage(settings: PopupSettings) {
  if (settings.enabled === 'true' && !settings.title.trim()) {
    return 'أضف عنواناً قبل تفعيل البوب أب.';
  }

  const displayInterval = Number(settings.displayInterval);
  if (!Number.isInteger(displayInterval) || displayInterval < 0) {
    return 'عدد الزيارات يجب أن يكون رقماً صحيحاً يساوي صفر أو أكبر.';
  }

  if (settings.expiresAt) {
    const expiresAt = new Date(toUtcIso(settings.expiresAt));
    if (Number.isNaN(expiresAt.getTime())) return 'تاريخ انتهاء العرض غير صحيح.';
    if (expiresAt.getTime() <= Date.now()) return 'اختَر وقت انتهاء في المستقبل.';
  }

  return isValidActionUrl(settings.actionUrl.trim())
    ? null
    : 'الرابط يجب أن يبدأ بـ https:// أو http:// أو /';
}

function createPopupSettingsPayload(settings: PopupSettings): Record<string, string> {
  return {
    [popupSettingKeys.enabled]: settings.enabled,
    [popupSettingKeys.title]: settings.title.trim(),
    [popupSettingKeys.body]: settings.body.trim(),
    [popupSettingKeys.imageUrl]: settings.imageUrl.trim(),
    [popupSettingKeys.actionUrl]: settings.actionUrl.trim(),
    [popupSettingKeys.actionLabel]: settings.actionLabel.trim(),
    [popupSettingKeys.displayInterval]: settings.displayInterval,
    [popupSettingKeys.expiresAt]: toUtcIso(settings.expiresAt),
  };
}

export default function AdminPopupPageClient() {
  const [settings, setSettings] = useState<PopupSettings>(defaults);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [uploadProgress, setUploadProgress] = useState<number | null>(null);

  const loadSettings = useCallback(async () => {
    setIsLoading(true);
    try {
      const platformSettings = await adminService.getPlatformSettings();
      setSettings(readPopupSettings(platformSettings ?? []));
    } catch (error) {
      if (!isAxiosError(error)) throw error;
      toast.error('تعذر تحميل إعدادات البوب أب.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadSettings();
  }, [loadSettings]);

  function updateSetting(key: keyof PopupSettings, value: string) {
    setSettings((current) => ({ ...current, [key]: value }));
  }

  async function uploadImage(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;

    const imageValidationMessage = validateImageUpload(file);
    if (imageValidationMessage) {
      toast.error(imageValidationMessage);
      return;
    }

    setUploadProgress(0);
    try {
      const imageUrl = await adminService.uploadPlatformPopupImage(file, setUploadProgress);
      updateSetting('imageUrl', imageUrl);
      toast.success('تم رفع الصورة وتحويلها إلى WebP على assets.');
    } catch (error) {
      if (!isAxiosError(error)) throw error;
      toast.error('تعذر رفع الصورة.');
    } finally {
      setUploadProgress(null);
    }
  }

  async function save() {
    const validationMessage = getPopupValidationMessage(settings);
    if (validationMessage) return toast.error(validationMessage);

    setIsSaving(true);
    try {
      await adminService.updatePlatformSettings(createPopupSettingsPayload(settings));
      toast.success(settings.enabled === 'true' ? 'تم نشر البوب أب.' : 'تم حفظ البوب أب وهو متوقف حالياً.');
    } catch (error) {
      if (!isAxiosError(error)) throw error;
      toast.error('تعذر حفظ إعدادات البوب أب.');
    } finally {
      setIsSaving(false);
    }
  }

  const isEnabled = settings.enabled === 'true';

  return (
    <AdminPage
      activePath="/admin/popup"
      sectionLabel="التحكم في المنصة"
      pageTitle="Popup المنصة"
      subtitle="رسالة تظهر لزوار اللاندنج وللطلاب عند فتح المنصة، مع التحكم في عدد الزيارات بين كل ظهور."
      action={
        <button type="button" onClick={() => void save()} disabled={isLoading || isSaving} className="admin-btn-primary">
          {isSaving ? <LoaderCircle className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
          {isSaving ? 'جارٍ الحفظ...' : 'حفظ ونشر'}
        </button>
      }
    >
      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(320px,0.72fr)]" dir="rtl">
        <section className="admin-panel space-y-6 p-5 sm:p-7">
          <div className="flex flex-wrap items-center justify-between gap-4 border-b border-[var(--admin-border)] pb-5">
            <div>
              <h2 className="text-lg font-black text-[var(--admin-text)]">إعداد الرسالة</h2>
              <p className="mt-1 text-sm text-[var(--admin-muted)]">أوقف الزر لإخفاء البوب أب فوراً من اللاندنج وصفحات الطالب.</p>
            </div>
            <button
              type="button"
              role="switch"
              aria-checked={isEnabled}
              onClick={() => updateSetting('enabled', isEnabled ? 'false' : 'true')}
              className={`relative h-8 w-14 rounded-full transition ${isEnabled ? 'bg-[#0e8f8f]' : 'bg-[var(--admin-border)]'}`}
            >
              <span className={`absolute top-1 h-6 w-6 rounded-full bg-white shadow transition ${isEnabled ? 'left-1' : 'left-7'}`} />
              <span className="sr-only">تفعيل البوب أب</span>
            </button>
          </div>

          <label className="block">
            <span className="mb-2 block text-sm font-bold text-[var(--admin-text)]">العنوان <span className="text-red-600">*</span></span>
            <input value={settings.title} onChange={(event) => updateSetting('title', event.target.value)} placeholder="مثال: مراجعة ليلة الامتحان متاحة الآن" className="admin-input w-full" maxLength={140} disabled={isLoading} />
          </label>

          <label className="block">
            <span className="mb-2 block text-sm font-bold text-[var(--admin-text)]">النص</span>
            <textarea value={settings.body} onChange={(event) => updateSetting('body', event.target.value)} placeholder="اكتب تفاصيل الرسالة للطلاب..." className="admin-input min-h-32 w-full resize-y" maxLength={1000} disabled={isLoading} />
          </label>

          <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">
            <label className="block">
              <span className="mb-2 block text-sm font-bold text-[var(--admin-text)]">رابط الزر</span>
              <input value={settings.actionUrl} onChange={(event) => updateSetting('actionUrl', event.target.value)} placeholder="https://youtube.com/... أو /student/..." className="admin-input w-full" inputMode="url" disabled={isLoading} />
              <span className="mt-1.5 block text-xs text-[var(--admin-muted)]">يمكنك وضع رابط فيديو أو أي صفحة.</span>
            </label>
            <label className="block">
              <span className="mb-2 block text-sm font-bold text-[var(--admin-text)]">ينتهي العرض في</span>
              <input type="datetime-local" value={settings.expiresAt} onChange={(event) => updateSetting('expiresAt', event.target.value)} className="admin-input w-full" disabled={isLoading} />
              <span className="mt-1.5 block text-xs text-[var(--admin-muted)]">اتركه فارغاً لعرض البوب أب بدون موعد انتهاء.</span>
            </label>
            <label className="block">
              <span className="mb-2 block text-sm font-bold text-[var(--admin-text)]">نص الزر</span>
              <input value={settings.actionLabel} onChange={(event) => updateSetting('actionLabel', event.target.value)} placeholder="شاهد الآن" className="admin-input w-full" maxLength={50} disabled={isLoading} />
            </label>
            <label className="block">
              <span className="mb-2 block text-sm font-bold text-[var(--admin-text)]">يظهر بعد كل كام زيارة؟</span>
              <input type="number" min={0} step={1} value={settings.displayInterval} onChange={(event) => updateSetting('displayInterval', event.target.value)} className="admin-input w-full" disabled={isLoading} />
              <span className="mt-1.5 block text-xs text-[var(--admin-muted)]">0 = مرة واحدة فقط، و3 = كل ثلاث فتحات بعد الإغلاق.</span>
            </label>
          </div>

          <div className="rounded-2xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div className="flex items-center gap-3">
                <span className="inline-flex h-10 w-10 items-center justify-center rounded-xl bg-[#0e8f8f]/10 text-[#0e8f8f]"><ImageUp className="h-5 w-5" /></span>
                <div>
                  <p className="font-bold text-[var(--admin-text)]">صورة البوب أب</p>
                  <p className="text-xs text-[var(--admin-muted)]">JPG أو PNG أو WebP، حتى 10MB، ويتم تحويلها إلى WebP.</p>
                </div>
              </div>
              <label className="admin-btn-ghost cursor-pointer">
                {uploadProgress === null ? <Upload className="h-4 w-4" /> : <LoaderCircle className="h-4 w-4 animate-spin" />}
                {uploadProgress === null ? 'رفع صورة' : `رفع ${uploadProgress}%`}
                <input type="file" accept="image/jpeg,image/png,image/webp" onChange={uploadImage} className="sr-only" disabled={uploadProgress !== null} />
              </label>
            </div>

            {settings.imageUrl && (
              <div className="mt-4 flex items-center gap-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-3">
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img src={resolveMediaUrl(settings.imageUrl)} alt="معاينة صورة البوب أب" className="h-16 w-24 rounded-lg object-cover" />
                <span className="min-w-0 flex-1 truncate text-xs text-[var(--admin-muted)]" dir="ltr">{resolveMediaUrl(settings.imageUrl)}</span>
                <button type="button" onClick={() => updateSetting('imageUrl', '')} className="admin-btn-icon" aria-label="حذف الصورة"><X className="h-4 w-4" /></button>
              </div>
            )}
          </div>
        </section>

        <aside className="self-start xl:sticky xl:top-6">
          <div className="overflow-hidden rounded-[24px] border border-[var(--admin-border)] bg-[#f6f7f8] shadow-[0_18px_48px_var(--admin-shadow)]">
            <div className="flex items-center gap-2 border-b border-[var(--admin-border)] bg-[var(--admin-card)] px-5 py-4 text-sm font-black text-[var(--admin-text)]">
              <Sparkles className="h-4 w-4 text-[#0e8f8f]" /> معاينة للطالب
            </div>
            <div className="p-4">
              <div className="overflow-hidden rounded-2xl border border-[#dce1e6] bg-[#f6f7f8]">
                {settings.imageUrl && (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img src={resolveMediaUrl(settings.imageUrl)} alt="" className="h-40 w-full object-cover" />
                )}
                <div className="p-5">
                  <h3 className="text-xl font-black text-[#0a1d3d]">{settings.title || 'عنوان البوب أب'}</h3>
                  <p className="mt-2 min-h-12 whitespace-pre-line text-sm leading-6 text-[#2e3a47]">{settings.body || 'سيظهر هنا النص الذي تكتبه للطلاب والزوار.'}</p>
                  <div className="mt-5 flex items-center gap-3">
                    {settings.actionUrl && <span className="inline-flex min-h-10 items-center gap-2 rounded-xl bg-[#0a1d3d] px-4 text-sm font-bold text-white">{settings.actionLabel || 'فتح الرابط'} <ExternalLink className="h-4 w-4" /></span>}
                    <span className="inline-flex min-h-10 items-center gap-2 rounded-xl px-2 text-sm font-bold text-[#0e6f6f]"><Check className="h-4 w-4" /> فهمت</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </aside>
      </div>
    </AdminPage>
  );
}
