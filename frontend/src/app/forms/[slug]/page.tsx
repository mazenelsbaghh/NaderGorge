'use client';

import { useEffect, useState, use } from 'react';
import { useRouter } from 'next/navigation';
import { motion, AnimatePresence } from 'framer-motion';
import { ClipboardCheck, AlertCircle, CheckCircle2, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';

import { getPublicForm, submitPublicForm, FormFieldConfig, PublicFormDto } from '@/services/forms-service';

interface PublicFormPageProps {
  params: Promise<{ slug: string }>;
}

export default function PublicFormPage({ params }: PublicFormPageProps) {
  const { slug } = use(params);
  const router = useRouter();

  const [form, setForm] = useState<PublicFormDto | null>(null);
  const [fields, setFields] = useState<FormFieldConfig[]>([]);
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [errors, setErrors] = useState<Record<string, string>>({});
  
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);

  useEffect(() => {
    const loadForm = async () => {
      try {
        setLoading(true);
        const data = await getPublicForm(slug);
        setForm(data);
        setFields(JSON.parse(data.fieldsJson || '[]'));
        
        // Initialize default answers
        const initialAnswers: Record<string, string> = {};
        const parsedFields: FormFieldConfig[] = JSON.parse(data.fieldsJson || '[]');
        parsedFields.forEach((f) => {
          initialAnswers[f.id] = '';
        });
        setAnswers(initialAnswers);
      } catch (error) {
        console.error('Error loading form:', error);
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
          // Allow spaces, digits, and leading +
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
    } catch (error) {
      console.error('Submission error:', error);
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="flex h-screen w-screen items-center justify-center bg-[#faf2e6] text-[#2c1708]">
        <div className="text-center space-y-4">
          <Loader2 className="h-10 w-10 animate-spin text-[#9a6933] mx-auto" />
          <p className="text-sm font-bold tracking-wider font-cairo">جاري تحميل النموذج...</p>
        </div>
      </div>
    );
  }

  if (!form) {
    return (
      <div className="flex min-h-screen w-screen items-center justify-center bg-[#faf2e6] p-6 text-[#2c1708] font-cairo" dir="rtl">
        <div className="max-w-md w-full text-center bg-[#fcf6ea] p-8 rounded-[2rem] border border-[#f0e4ce] shadow-2xl space-y-6">
          <div className="w-16 h-16 rounded-full bg-rose-500/10 flex items-center justify-center mx-auto text-rose-500">
            <AlertCircle className="h-8 w-8" />
          </div>
          <div className="space-y-2">
            <h1 className="text-2xl font-black text-[#7f5427]">النموذج غير متوفر</h1>
            <p className="text-sm text-[#7a644d]">
              عذراً، هذا النموذج غير موجود أو تم إغلاق باب التسجيل به حالياً من قبل الإدارة.
            </p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-[#faf2e6] py-12 px-4 md:px-8 text-[#2c1708] font-cairo relative overflow-hidden" dir="rtl">
      {/* Background Orbs and Grid */}
      <div className="absolute inset-0 z-0 pointer-events-none bg-[radial-gradient(circle_at_top,rgba(154,105,51,0.06),transparent_50%)]" />
      <div className="absolute top-0 right-0 w-[40rem] h-[40rem] bg-gradient-to-b from-[#f2dfbc]/30 to-transparent rounded-full filter blur-3xl opacity-40 -translate-y-1/2 translate-x-1/3" />
      
      <div className="max-w-xl mx-auto relative z-10">
        <AnimatePresence mode="wait">
          {!submitted ? (
            <motion.div
              key="form-container"
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -20 }}
              transition={{ duration: 0.4 }}
              className="bg-[#fcf6ea] rounded-[2.5rem] border border-[#f0e4ce] p-8 md:p-10 shadow-[0_20px_60px_rgba(44,23,8,0.05)] backdrop-blur-2xl"
            >
              {/* Header */}
              <div className="text-center mb-8 border-b border-[#f0e4ce] pb-6">
                <div className="w-12 h-12 rounded-full bg-gradient-to-br from-[#9a6933] to-[#7f5427] text-white flex items-center justify-center mx-auto shadow-md mb-4">
                  <ClipboardCheck className="h-6 w-6" />
                </div>
                <h1 className="text-2xl md:text-3xl font-black text-[#7f5427] tracking-tight">{form.title}</h1>
                {form.description && (
                  <p className="text-xs text-[#7a644d] mt-3 leading-relaxed whitespace-pre-line">{form.description}</p>
                )}
              </div>

              {/* Form Content */}
              <form onSubmit={handleSubmit} className="space-y-6">
                {fields.map((field) => {
                  const hasError = !!errors[field.id];
                  return (
                    <div key={field.id} className="space-y-2 text-right">
                      <label className="text-xs font-bold text-[#7f5427] flex items-center gap-1.5 px-1">
                        {field.label}
                        {field.isRequired && <span className="text-rose-500 font-bold">*</span>}
                      </label>

                      {field.type === 'text' && (
                        <input
                          type="text"
                          value={answers[field.id] || ''}
                          onChange={(e) => handleInputChange(field.id, e.target.value)}
                          placeholder={field.placeholder || 'اكتب هنا...'}
                          className={`w-full bg-[#faf2e6]/50 border rounded-2xl px-4 py-3.5 text-sm transition focus:outline-none focus:ring-2 focus:ring-[#9a6933]/20 ${
                            hasError ? 'border-rose-500 focus:border-rose-500' : 'border-[#f0e4ce] focus:border-[#9a6933]'
                          }`}
                        />
                      )}

                      {field.type === 'longtext' && (
                        <textarea
                          rows={4}
                          value={answers[field.id] || ''}
                          onChange={(e) => handleInputChange(field.id, e.target.value)}
                          placeholder={field.placeholder || 'اكتب بالتفصيل هنا...'}
                          className={`w-full bg-[#faf2e6]/50 border rounded-2xl px-4 py-3.5 text-sm transition focus:outline-none focus:ring-2 focus:ring-[#9a6933]/20 resize-none ${
                            hasError ? 'border-rose-500 focus:border-rose-500' : 'border-[#f0e4ce] focus:border-[#9a6933]'
                          }`}
                        />
                      )}

                      {field.type === 'number' && (
                        <input
                          type="number"
                          value={answers[field.id] || ''}
                          onChange={(e) => handleInputChange(field.id, e.target.value)}
                          placeholder={field.placeholder || '0'}
                          className={`w-full bg-[#faf2e6]/50 border rounded-2xl px-4 py-3.5 text-sm transition focus:outline-none focus:ring-2 focus:ring-[#9a6933]/20 ${
                            hasError ? 'border-rose-500 focus:border-rose-500' : 'border-[#f0e4ce] focus:border-[#9a6933]'
                          }`}
                        />
                      )}

                      {field.type === 'email' && (
                        <input
                          type="email"
                          value={answers[field.id] || ''}
                          onChange={(e) => handleInputChange(field.id, e.target.value)}
                          placeholder={field.placeholder || 'name@domain.com'}
                          className={`w-full bg-[#faf2e6]/50 border rounded-2xl px-4 py-3.5 text-sm transition focus:outline-none focus:ring-2 focus:ring-[#9a6933]/20 text-left ${
                            hasError ? 'border-rose-500 focus:border-rose-500' : 'border-[#f0e4ce] focus:border-[#9a6933]'
                          }`}
                        />
                      )}

                      {field.type === 'phone' && (
                        <input
                          type="tel"
                          value={answers[field.id] || ''}
                          onChange={(e) => handleInputChange(field.id, e.target.value)}
                          placeholder={field.placeholder || '01000000000'}
                          className={`w-full bg-[#faf2e6]/50 border rounded-2xl px-4 py-3.5 text-sm transition focus:outline-none focus:ring-2 focus:ring-[#9a6933]/20 text-left ${
                            hasError ? 'border-rose-500 focus:border-rose-500' : 'border-[#f0e4ce] focus:border-[#9a6933]'
                          }`}
                        />
                      )}

                      {field.type === 'select' && (
                        <select
                          value={answers[field.id] || ''}
                          onChange={(e) => handleInputChange(field.id, e.target.value)}
                          className={`w-full bg-[#faf2e6]/50 border rounded-2xl px-4 py-3.5 text-sm transition focus:outline-none focus:ring-2 focus:ring-[#9a6933]/20 appearance-none ${
                            hasError ? 'border-rose-500 focus:border-rose-500' : 'border-[#f0e4ce] focus:border-[#9a6933]'
                          }`}
                        >
                          <option value="">{field.placeholder || 'اختر من القائمة...'}</option>
                          {field.options.map((opt, oIdx) => (
                            <option key={oIdx} value={opt}>
                              {opt}
                            </option>
                          ))}
                        </select>
                      )}

                      {field.type === 'checkbox' && (
                        <label className="flex items-start gap-2.5 cursor-pointer py-1 px-1">
                          <input
                            type="checkbox"
                            checked={answers[field.id] === 'true'}
                            onChange={(e) => handleInputChange(field.id, e.target.checked ? 'true' : '')}
                            className="h-4 w-4 mt-0.5 rounded border-[#f0e4ce] bg-[#faf2e6] text-[#9a6933] focus:ring-[#9a6933]/20"
                          />
                          <span className="text-xs font-medium text-[#7a644d] select-none leading-relaxed">
                            {field.placeholder || 'أوافق على الشروط والأحكام'}
                          </span>
                        </label>
                      )}

                      {hasError && (
                        <motion.div
                          initial={{ opacity: 0, y: -5 }}
                          animate={{ opacity: 1, y: 0 }}
                          className="text-[10px] text-rose-500 font-bold flex items-center gap-1 mt-1 px-1"
                        >
                          <AlertCircle className="h-3 w-3 shrink-0" />
                          <span>{errors[field.id]}</span>
                        </motion.div>
                      )}
                    </div>
                  );
                })}

                <div className="pt-4">
                  <button
                    type="submit"
                    disabled={submitting}
                    className="w-full bg-gradient-to-r from-[#9a6933] to-[#7f5427] text-white rounded-2xl py-4 font-black shadow-lg shadow-[#7f5427]/15 hover:shadow-[#7f5427]/25 transition flex items-center justify-center gap-2 cursor-pointer disabled:opacity-50"
                  >
                    {submitting ? (
                      <>
                        <Loader2 className="h-5 w-5 animate-spin" />
                        <span>جاري إرسال البيانات...</span>
                      </>
                    ) : (
                      <span>إرسال الطلب الآن</span>
                    )}
                  </button>
                </div>
              </form>
            </motion.div>
          ) : (
            /* Success State */
            <motion.div
              key="success-container"
              initial={{ opacity: 0, scale: 0.95 }}
              animate={{ opacity: 1, scale: 1 }}
              transition={{ duration: 0.4 }}
              className="bg-[#fcf6ea] rounded-[2.5rem] border border-[#f0e4ce] p-10 text-center shadow-[0_20px_60px_rgba(44,23,8,0.05)] space-y-6"
            >
              <div className="w-20 h-20 rounded-full bg-emerald-500/10 text-emerald-500 flex items-center justify-center mx-auto shadow-sm">
                <CheckCircle2 className="h-10 w-10 animate-bounce" />
              </div>

              <div className="space-y-3">
                <h2 className="text-2xl font-black text-[#7f5427]">تم استلام طلبك بنجاح!</h2>
                <p className="text-sm text-[#7a644d] leading-relaxed max-w-sm mx-auto">
                  شكرًا لاهتمامك. لقد تم تسجيل إجاباتك بنجاح في النظام، وسنقوم بمراجعة البيانات وتفاصيل طلبك والتواصل معك في أقرب وقت ممكن.
                </p>
              </div>

              <div className="pt-4 border-t border-[#f0e4ce]">
                <p className="text-[10px] uppercase font-black tracking-[0.2em] text-[#9a6933]">
                  نظام الإدارة - مؤسسة نادِر جُورج
                </p>
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    </div>
  );
}
