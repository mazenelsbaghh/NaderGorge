'use client';

import { devConsole } from '@/utils/dev-console';
import { useEffect, useState, use } from 'react';
import { useRouter } from 'next/navigation';
import { ArrowRight, Plus, Trash2, ArrowUp, ArrowDown, Settings, Eye, ClipboardList } from 'lucide-react';
import toast from 'react-hot-toast';

import { AdminShellChrome, AdminPageSkeleton } from '@/components/admin';
import { getAdminFormDetails, updateAdminForm, FormFieldConfig, FormFieldType } from '@/services/forms-service';
import { getAbsoluteLandingUrl } from '@/utils/url-utils';

interface EditFormPageProps {
  params: Promise<{ id: string }>;
}

const PREVIEW_GOVERNORATES = ['القاهرة', 'الجيزة', 'الإسكندرية', 'القليوبية', 'الدقهلية'];

const formatForDateTimeLocal = (isoString?: string) => {
  if (!isoString) return '';
  const date = new Date(isoString);
  const pad = (n: number) => String(n).padStart(2, '0');
  
  const yyyy = date.getFullYear();
  const MM = pad(date.getMonth() + 1);
  const dd = pad(date.getDate());
  const hh = pad(date.getHours());
  const mm = pad(date.getMinutes());
  
  return `${yyyy}-${MM}-${dd}T${hh}:${mm}`;
};

export default function EditFormPage({ params }: EditFormPageProps) {
  const { id } = use(params);
  const router = useRouter();
  
  const [loading, setLoading] = useState(true);
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [slug, setSlug] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [coverImageUrl, setCoverImageUrl] = useState('');
  const [startsAt, setStartsAt] = useState('');
  const [expiresAt, setExpiresAt] = useState('');
  const [fields, setFields] = useState<FormFieldConfig[]>([]);
  const [saving, setSaving] = useState(false);

  // State for options adding item-by-item
  const [newOptionTexts, setNewOptionTexts] = useState<Record<string, string>>({});

  useEffect(() => {
    const loadForm = async () => {
      try {
        const data = await getAdminFormDetails(id);
        setTitle(data.title);
        setDescription(data.description);
        setSlug(data.slug);
        setIsActive(data.isActive);
        setCoverImageUrl(data.coverImageUrl || '');
        setStartsAt(formatForDateTimeLocal(data.startsAt));
        setExpiresAt(formatForDateTimeLocal(data.expiresAt));
        setFields(JSON.parse(data.fieldsJson || '[]'));
      } catch (error) {
        devConsole.error('Error loading form details:', error);
        toast.error('حدث خطأ أثناء تحميل بيانات النموذج');
        router.push('/admin/forms');
      } finally {
        setLoading(false);
      }
    };
    loadForm();
  }, [id, router]);

  const addField = () => {
    const newField: FormFieldConfig = {
      id: `field_${Date.now()}`,
      type: 'text',
      label: 'حقل جديد',
      placeholder: 'ادخل القيمة هنا...',
      isRequired: false,
      options: [],
    };
    setFields([...fields, newField]);
  };

  const removeField = (fieldId: string) => {
    setFields(fields.filter((f) => f.id !== fieldId));
  };

  const updateField = (fieldId: string, updates: Partial<FormFieldConfig>) => {
    setFields(
      fields.map((f) => {
        if (f.id === fieldId) {
          return { ...f, ...updates };
        }
        return f;
      })
    );
  };

  const moveField = (index: number, direction: 'up' | 'down') => {
    const newIndex = direction === 'up' ? index - 1 : index + 1;
    if (newIndex < 0 || newIndex >= fields.length) return;

    const list = [...fields];
    const temp = list[index];
    list[index] = list[newIndex];
    list[newIndex] = temp;
    setFields(list);
  };

  // Predefined fields quick insertion
  const addPredefinedField = (type: 'name' | 'phone' | 'email' | 'gov_dist') => {
    const now = Date.now();
    if (type === 'name') {
      const newField: FormFieldConfig = {
        id: `field_name_${now}`,
        type: 'text',
        label: 'الاسم الكامل',
        placeholder: 'اكتب اسمك ثلاثي أو رباعي هنا...',
        isRequired: true,
        options: [],
      };
      setFields([...fields, newField]);
      toast.success('تم إضافة حقل الاسم الكامل');
    } else if (type === 'phone') {
      const newField: FormFieldConfig = {
        id: `field_phone_${now}`,
        type: 'phone',
        label: 'رقم الهاتف (واتساب)',
        placeholder: 'اكتب رقم الهاتف هنا...',
        isRequired: true,
        options: [],
      };
      setFields([...fields, newField]);
      toast.success('تم إضافة حقل رقم الهاتف');
    } else if (type === 'email') {
      const newField: FormFieldConfig = {
        id: `field_email_${now}`,
        type: 'email',
        label: 'البريد الإلكتروني',
        placeholder: 'مثال: student@masar.com',
        isRequired: true,
        options: [],
      };
      setFields([...fields, newField]);
      toast.success('تم إضافة حقل البريد الإلكتروني');
    } else if (type === 'gov_dist') {
      const hasGov = fields.some(f => f.type === 'governorate');
      if (hasGov) {
        toast.error('حقل المحافظة والحي موجود بالفعل بالنموذج');
        return;
      }
      const newFieldGov: FormFieldConfig = {
        id: `field_gov_${now}`,
        type: 'governorate',
        label: 'المحافظة',
        isRequired: true,
        options: [],
      };
      const newFieldDist: FormFieldConfig = {
        id: `field_dist_${now + 1}`,
        type: 'district',
        label: 'المنطقة / الحي',
        isRequired: true,
        options: [],
      };
      setFields([...fields, newFieldGov, newFieldDist]);
      toast.success('تم إضافة حقلي المحافظة والحي (مترابطين)');
    }
  };

  // Dropdown options helpers
  const handleAddOption = (fieldId: string) => {
    const text = (newOptionTexts[fieldId] || '').trim();
    if (!text) return;
    const currentField = fields.find((f) => f.id === fieldId);
    if (!currentField) return;

    if (currentField.options.includes(text)) {
      toast.error('هذا الخيار موجود بالفعل');
      return;
    }

    updateField(fieldId, { options: [...currentField.options, text] });
    setNewOptionTexts((prev) => ({ ...prev, [fieldId]: '' }));
  };

  const handleRemoveOption = (fieldId: string, optionIndex: number) => {
    const currentField = fields.find((f) => f.id === fieldId);
    if (!currentField) return;
    updateField(fieldId, {
      options: currentField.options.filter((_, idx) => idx !== optionIndex),
    });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim()) return toast.error('يرجى إدخال عنوان النموذج');
    if (!slug.trim()) return toast.error('يرجى إدخال الرابط المختصر (Slug)');
    if (fields.length === 0) return toast.error('يجب إضافة حقل واحد على الأقل للنموذج');

    // Validate fields
    for (const f of fields) {
      if (!f.label.trim()) return toast.error('يجب تسمية جميع الحقول المضافة للنموذج');
      if (f.type === 'select' && f.options.length === 0) {
        return toast.error(`يرجى كتابة خيارات الحقل المنسدل "${f.label}"`);
      }
    }

    try {
      setSaving(true);
      await updateAdminForm(id, {
        title,
        description,
        slug: slug.trim().toLowerCase(),
        isActive,
        coverImageUrl: coverImageUrl.trim() || undefined,
        startsAt: startsAt ? new Date(startsAt).toISOString() : undefined,
        expiresAt: expiresAt ? new Date(expiresAt).toISOString() : undefined,
        fieldsJson: JSON.stringify(fields),
      });
      toast.success('تم تحديث النموذج بنجاح');
      router.push('/admin/forms');
    } catch (error: any) {
      devConsole.error(error);
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <AdminShellChrome
        activePath="/admin/forms"
        sectionLabel="النماذج المخصصة"
        pageTitle="تعديل النموذج"
      >
        <AdminPageSkeleton />
      </AdminShellChrome>
    );
  }

  return (
    <AdminShellChrome
      activePath="/admin/forms"
      sectionLabel="النماذج المخصصة"
      pageTitle="تعديل النموذج"
      subtitle="قم بتعديل وتحديث حقول وإعدادات النموذج المخصص."
      action={
        <button
          onClick={() => router.push('/admin/forms')}
          className="admin-btn-ghost flex items-center gap-2"
        >
          <ArrowRight className="h-5 w-5" />
          رجوع
        </button>
      }
    >
      <form onSubmit={handleSubmit} className="grid gap-8 lg:grid-cols-12">
        {/* Left Column: Form Live Preview */}
        <div className="lg:col-span-5 space-y-6">
          <div className="admin-panel sticky top-6">
            <div className="flex items-center gap-2 mb-6 border-b border-[var(--admin-border)] pb-4">
              <Eye className="h-5 w-5 text-[var(--admin-primary)]" />
              <h2 className="text-lg font-bold text-[var(--admin-text-strong)]">معاينة مباشرة للنموذج</h2>
            </div>

            <div className="bg-[var(--admin-card-soft)] rounded-[1.5rem] p-6 border border-[var(--admin-border)] min-h-[400px]">
              {coverImageUrl && (
                <div className="mb-4 rounded-xl overflow-hidden h-32 border border-[var(--admin-border)] bg-[var(--admin-bg)]">
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img
                    src={coverImageUrl}
                    alt="غلاف النموذج"
                    className="w-full h-full object-cover"
                    onError={(e) => {
                      (e.target as HTMLImageElement).src = '';
                      toast.error('تعذر تحميل صورة الغلاف في المعاينة، يرجى التأكد من الرابط');
                    }}
                  />
                </div>
              )}

              <div className="mb-6 text-center">
                <h3 className="text-xl font-bold text-[var(--admin-text-strong)]">
                  {title || 'عنوان النموذج الجديد'}
                </h3>
                {description && (
                  <p className="text-xs text-[var(--admin-muted)] mt-2 whitespace-pre-line">{description}</p>
                )}
              </div>

              {fields.length === 0 ? (
                <div className="flex flex-col items-center justify-center h-[250px] text-center opacity-40 select-none">
                  <ClipboardList className="h-12 w-12 mb-3 text-[var(--admin-muted)]" />
                  <p className="text-sm">أضف حقولاً للنموذج من اللوحة الجانبية لرؤية المعاينة</p>
                </div>
              ) : (
                <div className="space-y-4">
                  {fields.map((f, i) => (
                    <div key={f.id} className="space-y-1.5 text-right">
                      <label className="text-xs font-bold text-[var(--admin-text)] flex items-center gap-1">
                        {f.label || `حقل رقم ${i + 1}`}
                        {f.isRequired && <span className="text-rose-500">*</span>}
                      </label>

                      {f.type === 'text' && (
                        <input
                          type="text"
                          disabled
                          placeholder={f.placeholder || 'ادخل النص هنا...'}
                          className="admin-input w-full pointer-events-none opacity-80"
                        />
                      )}

                      {f.type === 'longtext' && (
                        <textarea
                          disabled
                          rows={3}
                          placeholder={f.placeholder || 'ادخل النص هنا...'}
                          className="admin-input w-full pointer-events-none opacity-80 resize-none"
                        />
                      )}

                      {f.type === 'number' && (
                        <input
                          type="number"
                          disabled
                          placeholder={f.placeholder || '0'}
                          className="admin-input w-full pointer-events-none opacity-80"
                        />
                      )}

                      {f.type === 'email' && (
                        <input
                          type="email"
                          disabled
                          placeholder={f.placeholder || 'example@domain.com'}
                          className="admin-input w-full pointer-events-none opacity-80 text-left"
                        />
                      )}

                      {f.type === 'phone' && (
                        <input
                          type="tel"
                          disabled
                          placeholder={f.placeholder || '01xxxxxxxxx'}
                          className="admin-input w-full pointer-events-none opacity-80 text-left"
                        />
                      )}

                      {f.type === 'select' && (
                        <select
                          disabled
                          className="admin-input w-full pointer-events-none opacity-80"
                        >
                          <option value="">{f.placeholder || 'اختر من القائمة...'}</option>
                          {f.options.map((opt, oIdx) => (
                            <option key={oIdx} value={opt}>
                              {opt}
                            </option>
                          ))}
                        </select>
                      )}

                      {f.type === 'governorate' && (
                        <select
                          disabled
                          className="admin-input w-full pointer-events-none opacity-80"
                        >
                          <option value="">اختر المحافظة...</option>
                          {PREVIEW_GOVERNORATES.map((gov, oIdx) => (
                            <option key={oIdx} value={gov}>
                              {gov}
                            </option>
                          ))}
                        </select>
                      )}

                      {f.type === 'district' && (
                        <select
                          disabled
                          className="admin-input w-full pointer-events-none opacity-80"
                        >
                          <option value="">اختر المنطقة / الحي...</option>
                        </select>
                      )}

                      {f.type === 'checkbox' && (
                        <label className="flex items-center gap-2 cursor-pointer pointer-events-none">
                          <input
                            type="checkbox"
                            disabled
                            className="h-4 w-4 rounded border-[var(--admin-border)] bg-[var(--admin-card-strong)] text-[var(--admin-primary)] focus:ring-[var(--admin-primary)]"
                          />
                          <span className="text-sm font-medium">{f.placeholder || 'أوافق على الشروط والأحكام'}</span>
                        </label>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Right Column: Builder Controls */}
        <div className="lg:col-span-7 space-y-6">
          {/* Metadata section */}
          <div className="admin-panel space-y-4">
            <div className="flex items-center gap-2 mb-2 border-b border-[var(--admin-border)] pb-4">
              <Settings className="h-5 w-5 text-[var(--admin-primary)]" />
              <h2 className="text-lg font-bold text-[var(--admin-text-strong)]">إعدادات النموذج العامة</h2>
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-1 text-right">
                <label className="text-xs font-bold text-[var(--admin-text)]">عنوان النموذج</label>
                <input
                  type="text"
                  required
                  placeholder="مثال: تقديم توظيف مدرسين مساعدين"
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                  className="admin-input w-full"
                />
              </div>

              <div className="space-y-1 text-right">
                <label className="text-xs font-bold text-[var(--admin-text)]">الرابط المختصر (Slug)</label>
                <input
                  type="text"
                  required
                  placeholder="مثال: recruitment"
                  value={slug}
                  onChange={(e) => setSlug(e.target.value.replace(/[^a-zA-Z0-9-_]/g, ''))}
                  className="admin-input w-full text-left"
                />
                <span className="text-[10px] text-[var(--admin-muted)]">
                  الرابط العام سيكون: {getAbsoluteLandingUrl(`/forms/${slug || 'slug'}`)}
                </span>
              </div>
            </div>

            <div className="space-y-1 text-right">
              <label className="text-xs font-bold text-[var(--admin-text)]">رابط صورة الغلاف (Cover Image URL)</label>
              <input
                type="text"
                placeholder="مثال: https://images.unsplash.com/photo-... (رابط الصورة الغلاف)"
                value={coverImageUrl}
                onChange={(e) => setCoverImageUrl(e.target.value)}
                className="admin-input w-full text-left"
              />
              <span className="text-[10px] text-[var(--admin-muted)]">
                ضع رابط الصورة التي ترغب بظهورها كغلاف أعلى النموذج (اختياري).
              </span>
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-1 text-right">
                <label className="text-xs font-bold text-[var(--admin-text)]">تاريخ ووقت البدء التلقائي (اختياري)</label>
                <input
                  type="datetime-local"
                  value={startsAt}
                  onChange={(e) => setStartsAt(e.target.value)}
                  className="admin-input w-full text-left"
                />
              </div>

              <div className="space-y-1 text-right">
                <label className="text-xs font-bold text-[var(--admin-text)]">تاريخ ووقت الانتهاء التلقائي (اختياري)</label>
                <input
                  type="datetime-local"
                  value={expiresAt}
                  onChange={(e) => setExpiresAt(e.target.value)}
                  className="admin-input w-full text-left"
                />
              </div>
            </div>

            <div className="space-y-1 text-right">
              <label className="text-xs font-bold text-[var(--admin-text)] block">وصف النموذج</label>
              <textarea
                rows={3}
                placeholder="اكتب تعليمات أو تفاصيل حول هذا النموذج للزوار..."
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                className="admin-input w-full"
              />
            </div>

            <div className="flex items-center gap-3 pt-2">
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={isActive}
                  onChange={(e) => setIsActive(e.target.checked)}
                  className="h-4 w-4 rounded border-[var(--admin-border)] bg-[var(--admin-card-strong)] text-[var(--admin-primary)] focus:ring-[var(--admin-primary)]"
                />
                <span className="text-sm font-bold">تفعيل استقبال التقديمات فوراً</span>
              </label>
            </div>
          </div>

          {/* Quick predefined fields section */}
          <div className="admin-panel space-y-4">
            <div className="flex items-center gap-2 border-b border-[var(--admin-border)] pb-3">
              <ClipboardList className="h-5 w-5 text-[var(--admin-primary)]" />
              <h2 className="text-lg font-bold text-[var(--admin-text-strong)]">حقول جاهزة مسبقاً (إضافة سريعة)</h2>
            </div>
            <div className="flex flex-wrap gap-2.5">
              <button
                type="button"
                onClick={() => addPredefinedField('name')}
                className="px-3.5 py-2 rounded-xl bg-[var(--admin-card-strong)] hover:bg-[var(--admin-hover)] border border-[var(--admin-border)] text-xs font-bold text-[var(--admin-text)] transition"
              >
                + الاسم الكامل
              </button>
              <button
                type="button"
                onClick={() => addPredefinedField('phone')}
                className="px-3.5 py-2 rounded-xl bg-[var(--admin-card-strong)] hover:bg-[var(--admin-hover)] border border-[var(--admin-border)] text-xs font-bold text-[var(--admin-text)] transition"
              >
                + رقم الهاتف (واتساب)
              </button>
              <button
                type="button"
                onClick={() => addPredefinedField('email')}
                className="px-3.5 py-2 rounded-xl bg-[var(--admin-card-strong)] hover:bg-[var(--admin-hover)] border border-[var(--admin-border)] text-xs font-bold text-[var(--admin-text)] transition"
              >
                + البريد الإلكتروني
              </button>
              <button
                type="button"
                onClick={() => addPredefinedField('gov_dist')}
                className="px-3.5 py-2 rounded-xl bg-[var(--admin-primary-soft)] hover:bg-[var(--admin-hover)] border border-[var(--admin-primary-15)] text-xs font-bold text-[var(--admin-primary)] transition"
              >
                + المحافظة والحي (مترابطين)
              </button>
            </div>
          </div>

          {/* Fields Builder list */}
          <div className="admin-panel space-y-6">
            <div className="flex items-center justify-between border-b border-[var(--admin-border)] pb-4">
              <div className="flex items-center gap-2">
                <Plus className="h-5 w-5 text-[var(--admin-primary)]" />
                <h2 className="text-lg font-bold text-[var(--admin-text-strong)]">حقول النموذج المخصصة</h2>
              </div>
              <button
                type="button"
                onClick={addField}
                className="flex items-center gap-1 bg-[var(--admin-primary-soft)] hover:bg-[var(--admin-primary-strong)] hover:text-white text-[var(--admin-primary)] font-bold rounded-2xl px-4 py-2 text-xs transition"
              >
                <Plus className="h-4 w-4" />
                إضافة حقل مخصص
              </button>
            </div>

            {fields.length === 0 ? (
              <div className="py-12 text-center text-[var(--admin-muted)] border border-dashed border-[var(--admin-border)] rounded-2xl">
                لا توجد حقول حتى الآن. انقر على &quot;إضافة حقل&quot; أو استخدم &quot;الحقول الجاهزة مسبقاً&quot; للبدء.
              </div>
            ) : (
              <div className="space-y-6">
                {fields.map((f, index) => (
                  <div
                    key={f.id}
                    className="p-5 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)]/50 relative space-y-4 text-right"
                  >
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-black text-[var(--admin-primary)]">
                        الحقل #{index + 1}
                      </span>
                      <div className="flex gap-1.5">
                        <button
                          type="button"
                          disabled={index === 0}
                          onClick={() => moveField(index, 'up')}
                          className="p-1.5 rounded-lg border border-[var(--admin-border)] hover:bg-[var(--admin-hover)] disabled:opacity-30"
                          title="تحريك لأعلى"
                        >
                          <ArrowUp className="h-3.5 w-3.5" />
                        </button>
                        <button
                          type="button"
                          disabled={index === fields.length - 1}
                          onClick={() => moveField(index, 'down')}
                          className="p-1.5 rounded-lg border border-[var(--admin-border)] hover:bg-[var(--admin-hover)] disabled:opacity-30"
                          title="تحريك لأسفل"
                        >
                          <ArrowDown className="h-3.5 w-3.5" />
                        </button>
                        <button
                          type="button"
                          onClick={() => removeField(f.id)}
                          className="p-1.5 rounded-lg border border-rose-500/20 hover:bg-rose-500/10 text-rose-500"
                          title="حذف الحقل"
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    </div>

                    <div className="grid gap-4 md:grid-cols-3">
                      <div className="space-y-1">
                        <label className="text-xs font-bold text-[var(--admin-text)]">اسم الحقل (Label)</label>
                        <input
                          type="text"
                          required
                          value={f.label}
                          onChange={(e) => updateField(f.id, { label: e.target.value })}
                          className="admin-input w-full"
                          placeholder="مثال: الاسم بالكامل"
                        />
                      </div>

                      <div className="space-y-1">
                        <label className="text-xs font-bold text-[var(--admin-text)]">نوع الحقل</label>
                        <select
                          value={f.type}
                          onChange={(e) =>
                            updateField(f.id, {
                              type: e.target.value as FormFieldType,
                              placeholder: e.target.value === 'checkbox' ? 'أوافق على الشروط والأحكام' : '',
                              options: [],
                            })
                          }
                          className="admin-input w-full"
                        >
                          <option value="text">نص قصير (Text)</option>
                          <option value="longtext">نص طويل (Long Text)</option>
                          <option value="number">رقم (Number)</option>
                          <option value="email">بريد إلكتروني (Email)</option>
                          <option value="phone">رقم هاتف (Phone)</option>
                          <option value="select">قائمة خيارات (Dropdown)</option>
                          <option value="governorate">المحافظة (Egypt Governorates)</option>
                          <option value="district">المنطقة / الحي (Egypt Districts)</option>
                          <option value="checkbox">مربع خيار (Checkbox)</option>
                        </select>
                      </div>

                      <div className="space-y-1">
                        <label className="text-xs font-bold text-[var(--admin-text)]">
                          {f.type === 'checkbox' ? 'نص الخيار' : 'نص توضيحي (Placeholder)'}
                        </label>
                        <input
                          type="text"
                          value={f.placeholder}
                          onChange={(e) => updateField(f.id, { placeholder: e.target.value })}
                          className="admin-input w-full"
                          placeholder={f.type === 'checkbox' ? 'مثال: أوافق على شروط التوظيف' : 'مثال: اكتب هنا...'}
                          disabled={f.type === 'governorate' || f.type === 'district'}
                        />
                      </div>
                    </div>

                    {f.type === 'select' && (
                      <div className="space-y-2 border-t border-[var(--admin-border)] pt-3">
                        <label className="text-xs font-bold text-[var(--admin-text)]">
                          خيارات القائمة المنسدلة (إضافة عنصر تلو الآخر)
                        </label>
                        <div className="flex gap-2">
                          <input
                            type="text"
                            value={newOptionTexts[f.id] || ''}
                            onChange={(e) =>
                              setNewOptionTexts((prev) => ({ ...prev, [f.id]: e.target.value }))
                            }
                            onKeyDown={(e) => {
                              if (e.key === 'Enter') {
                                e.preventDefault();
                                handleAddOption(f.id);
                              }
                            }}
                            className="admin-input flex-1"
                            placeholder="اكتب خياراً ثم اضغط إضافة أو مفتاح Enter..."
                          />
                          <button
                            type="button"
                            onClick={() => handleAddOption(f.id)}
                            className="px-4 py-2 bg-[var(--admin-primary)] text-white rounded-xl text-xs font-bold hover:opacity-90"
                          >
                            إضافة خيار
                          </button>
                        </div>
                        {f.options.length > 0 && (
                          <div className="flex flex-wrap gap-1.5 mt-2 bg-[var(--admin-card-strong)] p-2 rounded-xl border border-[var(--admin-border)]">
                            {f.options.map((opt, optIdx) => (
                              <div
                                key={optIdx}
                                className="flex items-center gap-1.5 bg-[var(--admin-bg)] px-2.5 py-1 rounded-lg text-xs font-bold border border-[var(--admin-border)] text-[var(--admin-text)]"
                              >
                                <span>{opt}</span>
                                <button
                                  type="button"
                                  onClick={() => handleRemoveOption(f.id, optIdx)}
                                  className="text-rose-500 hover:text-rose-700 font-bold ml-1 text-sm leading-none"
                                >
                                  &times;
                                </button>
                              </div>
                            ))}
                          </div>
                        )}
                      </div>
                    )}

                    <div className="flex items-center gap-3 pt-1">
                      <label className="flex items-center gap-2 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={f.isRequired}
                          onChange={(e) => updateField(f.id, { isRequired: e.target.checked })}
                          className="h-4 w-4 rounded border-[var(--admin-border)] bg-[var(--admin-card-strong)] text-[var(--admin-primary)] focus:ring-[var(--admin-primary)]"
                        />
                        <span className="text-xs font-bold">هذا الحقل مطلوب (Required)</span>
                      </label>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="flex gap-4 justify-end">
            <button
              type="button"
              onClick={() => router.push('/admin/forms')}
              className="admin-btn-ghost"
              disabled={saving}
            >
              إلغاء
            </button>
            <button
              type="submit"
              className="admin-btn-primary disabled:opacity-50"
              disabled={saving}
            >
              {saving ? 'جاري الحفظ...' : 'حفظ ونشر النموذج'}
            </button>
          </div>
        </div>
      </form>
    </AdminShellChrome>
  );
}
