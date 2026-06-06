'use client';

import { useEffect, useState, use } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { ClipboardCheck, AlertCircle, CheckCircle2, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';

import { getPublicForm, submitPublicForm, FormFieldConfig, PublicFormDto } from '@/services/forms-service';
import { GOVERNORATE_DISTRICTS, getDistrictsForGovernorate } from '@/data/governorate-districts';

interface PublicFormPageProps {
  params: Promise<{ slug: string }>;
}

const inputBaseClass =
  'w-full rounded-2xl border bg-[var(--admin-card-soft)] px-4 py-3.5 text-sm text-[var(--admin-text)] transition-colors placeholder:text-[var(--admin-muted)]/80 focus:outline-none focus:ring-2 focus:ring-[var(--admin-primary-15)]';
const fieldBorderClass = (hasError: boolean) =>
  hasError
    ? 'border-[var(--admin-danger)] focus:border-[var(--admin-danger)]'
    : 'border-[var(--admin-border)] focus:border-[var(--admin-primary)]';

export default function PublicFormPage({ params }: PublicFormPageProps) {
  const { slug } = use(params);

  const [form, setForm] = useState<PublicFormDto | null>(null);
  const [fields, setFields] = useState<FormFieldConfig[]>([]);
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [errors, setErrors] = useState<Record<string, string>>({});
  
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');

  useEffect(() => {
    const loadForm = async () => {
      try {
        setLoading(true);
        const data = await getPublicForm(slug);
        const parsedFields: FormFieldConfig[] = JSON.parse(data.fieldsJson || '[]');
        setForm(data);
        setFields(parsedFields);

        const initialAnswers: Record<string, string> = {};
        parsedFields.forEach((f) => {
          initialAnswers[f.id] = '';
        });
        setAnswers(initialAnswers);
      } catch (error: any) {
        setForm(null);
        const message = error.response?.data?.message || 'عذراً، هذا النموذج غير موجود أو تم إغلاق باب التسجيل به حالياً من قبل الإدارة.';
        setErrorMessage(message);
      } finally {
        setLoading(false);
      }
    };
    loadForm();
  }, [slug]);

  const handleInputChange = (fieldId: string, value: string) => {
    setAnswers((prev) => ({ ...prev, [fieldId]: value }));
    if (errors[fieldId]) {
      setErrors((prev) => {
        const next = { ...prev };
        delete next[fieldId];
        return next;
      });
    }
  };

  const validate = () => {
    const nextErrors: Record<string, string> = {};
    let isValid = true;

    fields.forEach((field) => {
      const val = (answers[field.id] || '').trim();

      if (field.isRequired && !val) {
        nextErrors[field.id] = 'هذا الحقل مطلوب';
        isValid = false;
      } else if (val) {
        if (field.type === 'email') {
          const emailRegex = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;
          if (!emailRegex.test(val)) {
            nextErrors[field.id] = 'صيغة البريد الإلكتروني غير صالحة';
            isValid = false;
          }
        } else if (field.type === 'phone') {
          const phoneRegex = /^[0-9+ ]{8,15}$/;
          if (!phoneRegex.test(val)) {
            nextErrors[field.id] = 'رقم الهاتف غير صالح (يجب أن يحتوي على أرقام فقط وبطول مناسب)';
            isValid = false;
          }
        }
      }
    });

    setErrors(nextErrors);
    return isValid;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validate()) {
      toast.error('يرجى تصحيح الأخطاء قبل التقديم');
      return;
    }

    try {
      setSubmitting(true);
      await submitPublicForm(slug, answers);
      setSubmitted(true);
      toast.success('تم إرسال طلبك بنجاح');
    } catch {
      toast.error('تعذر إرسال الطلب. حاول مرة أخرى.');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="flex h-screen w-screen items-center justify-center bg-[var(--admin-bg)] text-[var(--admin-text)]">
        <div className="text-center space-y-4">
          <Loader2 className="h-10 w-10 animate-spin text-[var(--admin-primary)] mx-auto" />
          <p className="text-sm font-bold tracking-wider font-cairo">جاري تحميل النموذج...</p>
        </div>
      </div>
    );
  }

  if (!form) {
    return (
      <div className="flex min-h-screen w-screen items-center justify-center bg-[var(--admin-bg)] p-6 text-[var(--admin-text)] font-cairo" dir="rtl">
        <div className="max-w-md w-full text-center bg-[var(--admin-card)] p-8 rounded-2xl border border-[var(--admin-border)] shadow-sm space-y-6">
          <div className="w-16 h-16 rounded-full bg-[var(--admin-danger-10)] flex items-center justify-center mx-auto text-[var(--admin-danger)]">
            <AlertCircle className="h-8 w-8" />
          </div>
          <div className="space-y-2">
            <h1 className="text-2xl font-black text-[var(--admin-text)]">النموذج غير متوفر</h1>
            <p className="text-sm text-[var(--admin-muted)] leading-relaxed">
              {errorMessage}
            </p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-[var(--admin-bg)] py-12 px-4 md:px-8 text-[var(--admin-text)] font-cairo relative overflow-hidden" dir="rtl">
      <div className="absolute inset-x-0 top-0 z-0 h-40 pointer-events-none bg-[linear-gradient(180deg,var(--admin-primary-15),transparent)]" />

      <div className="max-w-xl mx-auto relative z-10">
        <AnimatePresence mode="wait">
          {!submitted ? (
            <motion.div
              key="form-container"
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -20 }}
              transition={{ duration: 0.4 }}
              className="bg-[var(--admin-card)] rounded-2xl border border-[var(--admin-border)] p-8 md:p-10 shadow-sm"
            >
              {form.coverImageUrl && (
                <div className="mb-6 rounded-2xl overflow-hidden h-48 border border-[var(--admin-border)] shadow-inner bg-[var(--admin-bg)]">
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img
                    src={form.coverImageUrl.startsWith('/') ? `${process.env.NEXT_PUBLIC_BACKEND_URL || ''}${form.coverImageUrl}` : form.coverImageUrl}
                    alt={form.title}
                    className="w-full h-full object-cover"
                  />
                </div>
              )}

              {/* Header */}
              <div className="text-center mb-8 border-b border-[var(--admin-border)] pb-6">
                {!form.coverImageUrl && (
                  <div className="w-12 h-12 rounded-full bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] flex items-center justify-center mx-auto shadow-sm mb-4">
                    <ClipboardCheck className="h-6 w-6" />
                  </div>
                )}
                <h1 className="text-2xl md:text-3xl font-black text-[var(--admin-text)] tracking-tight">{form.title}</h1>
                {form.description && (
                  <p className="text-xs text-[var(--admin-muted)] mt-3 leading-relaxed whitespace-pre-line">{form.description}</p>
                )}
              </div>

              {/* Form Content */}
              <form onSubmit={handleSubmit} className="space-y-6">
                {fields.map((field) => {
                  const hasError = !!errors[field.id];
                  const fieldInputId = `public-form-${field.id}`;
                  const fieldErrorId = `${fieldInputId}-error`;
                  return (
                    <div key={field.id} className="space-y-2 text-right">
                      <label htmlFor={fieldInputId} className="text-xs font-bold text-[var(--admin-text)] flex items-center gap-1.5 px-1">
                        {field.label}
                        {field.isRequired && <span className="text-[var(--admin-danger)] font-bold">*</span>}
                      </label>

                      {field.type === 'text' && (
                        <input
                          id={fieldInputId}
                          type="text"
                          value={answers[field.id] || ''}
                          onChange={(e) => handleInputChange(field.id, e.target.value)}
                          placeholder={field.placeholder || 'اكتب هنا...'}
                          aria-invalid={hasError}
                          aria-describedby={hasError ? fieldErrorId : undefined}
                          className={`${inputBaseClass} ${fieldBorderClass(hasError)}`}
                        />
                      )}

                      {field.type === 'longtext' && (
                        <textarea
                          id={fieldInputId}
                          rows={4}
                          value={answers[field.id] || ''}
                          onChange={(e) => handleInputChange(field.id, e.target.value)}
                          placeholder={field.placeholder || 'اكتب بالتفصيل هنا...'}
                          aria-invalid={hasError}
                          aria-describedby={hasError ? fieldErrorId : undefined}
                          className={`${inputBaseClass} resize-none ${fieldBorderClass(hasError)}`}
                        />
                      )}

                      {field.type === 'number' && (
                        <input
                          id={fieldInputId}
                          type="number"
                          value={answers[field.id] || ''}
                          onChange={(e) => handleInputChange(field.id, e.target.value)}
                          placeholder={field.placeholder || '0'}
                          aria-invalid={hasError}
                          aria-describedby={hasError ? fieldErrorId : undefined}
                          className={`${inputBaseClass} ${fieldBorderClass(hasError)}`}
                        />
                      )}

                      {field.type === 'email' && (
                        <input
                          id={fieldInputId}
                          type="email"
                          value={answers[field.id] || ''}
                          onChange={(e) => handleInputChange(field.id, e.target.value)}
                          placeholder={field.placeholder || 'name@domain.com'}
                          aria-invalid={hasError}
                          aria-describedby={hasError ? fieldErrorId : undefined}
                          className={`${inputBaseClass} text-left ${fieldBorderClass(hasError)}`}
                        />
                      )}

                      {field.type === 'phone' && (
                        <input
                          id={fieldInputId}
                          type="tel"
                          value={answers[field.id] || ''}
                          onChange={(e) => handleInputChange(field.id, e.target.value)}
                          placeholder={field.placeholder || '01000000000'}
                          aria-invalid={hasError}
                          aria-describedby={hasError ? fieldErrorId : undefined}
                          className={`${inputBaseClass} text-left ${fieldBorderClass(hasError)}`}
                        />
                      )}

                      {field.type === 'select' && (
                        <select
                          id={fieldInputId}
                          value={answers[field.id] || ''}
                          onChange={(e) => handleInputChange(field.id, e.target.value)}
                          aria-invalid={hasError}
                          aria-describedby={hasError ? fieldErrorId : undefined}
                          className={`${inputBaseClass} appearance-none ${fieldBorderClass(hasError)}`}
                        >
                          <option value="">{field.placeholder || 'اختر من القائمة...'}</option>
                          {field.options.map((opt, oIdx) => (
                            <option key={oIdx} value={opt}>
                              {opt}
                            </option>
                          ))}
                        </select>
                      )}

                      {field.type === 'governorate' && (
                        <select
                          id={fieldInputId}
                          value={answers[field.id] || ''}
                          onChange={(e) => {
                            handleInputChange(field.id, e.target.value);
                            const districtField = fields.find((fd) => fd.type === 'district');
                            if (districtField) {
                              handleInputChange(districtField.id, '');
                            }
                          }}
                          aria-invalid={hasError}
                          aria-describedby={hasError ? fieldErrorId : undefined}
                          className={`${inputBaseClass} ${fieldBorderClass(hasError)}`}
                        >
                          <option value="">اختر المحافظة...</option>
                          {Object.keys(GOVERNORATE_DISTRICTS).map((gov) => (
                            <option key={gov} value={gov}>
                              {gov}
                            </option>
                          ))}
                        </select>
                      )}

                      {field.type === 'district' && (() => {
                        const govField = fields.find((fd) => fd.type === 'governorate');
                        const selectedGov = govField ? answers[govField.id] : '';
                        const districts = selectedGov ? getDistrictsForGovernorate(selectedGov) : [];
                        return (
                          <select
                            id={fieldInputId}
                            value={answers[field.id] || ''}
                            onChange={(e) => handleInputChange(field.id, e.target.value)}
                            disabled={!selectedGov}
                            aria-invalid={hasError}
                            aria-describedby={hasError ? fieldErrorId : undefined}
                            className={`${inputBaseClass} ${fieldBorderClass(hasError)}`}
                          >
                            <option value="">
                              {selectedGov ? 'اختر المنطقة / الحي...' : 'اختر المحافظة أولاً'}
                            </option>
                            {districts.map((dist) => (
                              <option key={dist} value={dist}>
                                {dist}
                              </option>
                            ))}
                          </select>
                        );
                      })()}

                      {field.type === 'checkbox' && (
                        <label className="flex items-start gap-2.5 cursor-pointer py-1 px-1">
                          <input
                            id={fieldInputId}
                            type="checkbox"
                            checked={answers[field.id] === 'true'}
                            onChange={(e) => handleInputChange(field.id, e.target.checked ? 'true' : '')}
                            aria-invalid={hasError}
                            aria-describedby={hasError ? fieldErrorId : undefined}
                            className="h-4 w-4 mt-0.5 rounded border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-[var(--admin-primary)] focus:ring-[var(--admin-primary-15)]"
                          />
                          <span className="text-xs font-medium text-[var(--admin-muted)] select-none leading-relaxed">
                            {field.placeholder || 'أوافق على الشروط والأحكام'}
                          </span>
                        </label>
                      )}

                      {hasError && (
                        <motion.div
                          id={fieldErrorId}
                          initial={{ opacity: 0, y: -5 }}
                          animate={{ opacity: 1, y: 0 }}
                          role="alert"
                          className="text-[10px] text-[var(--admin-danger)] font-bold flex items-center gap-1 mt-1 px-1"
                        >
                          <AlertCircle className="h-3 w-3 shrink-0" />
                          <span>{errors[field.id]}</span>
                        </motion.div>
                      )}
                    </div>
                  );
                })}

                <button
                  type="submit"
                  disabled={submitting}
                  className="w-full flex items-center justify-center gap-2 bg-[var(--admin-primary)] text-white hover:bg-[var(--admin-primary-strong)] h-12 rounded-2xl font-bold transition-all hover:shadow-lg hover:shadow-[var(--admin-primary-15)] disabled:opacity-50"
                >
                  {submitting ? (
                    <>
                      <Loader2 className="h-4 w-4 animate-spin" />
                      <span>جاري إرسال الطلب...</span>
                    </>
                  ) : (
                    <span>تقديم الطلب</span>
                  )}
                </button>
              </form>
            </motion.div>
          ) : (
            <motion.div
              key="success-container"
              initial={{ opacity: 0, scale: 0.95 }}
              animate={{ opacity: 1, scale: 1 }}
              className="bg-[var(--admin-card)] rounded-2xl border border-[var(--admin-border)] p-10 text-center shadow-sm space-y-6"
            >
              <div className="w-16 h-16 rounded-full bg-emerald-500/10 text-emerald-500 flex items-center justify-center mx-auto">
                <CheckCircle2 className="h-10 w-10" />
              </div>
              <div className="space-y-2">
                <h2 className="text-2xl font-black text-[var(--admin-text)]">تم استلام طلبك بنجاح!</h2>
                <p className="text-sm text-[var(--admin-muted)]">
                  نشكرك على ملء النموذج. تم تسجيل بياناتك بنجاح وسنقوم بمراجعتها في أقرب وقت.
                </p>
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    </div>
  );
}
