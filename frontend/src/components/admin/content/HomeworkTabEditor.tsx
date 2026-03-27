'use client';

import { useState, useEffect } from 'react';
import { adminService } from '@/services/admin-service';
import { contentService } from '@/services/content-service';
import { motion, AnimatePresence } from 'framer-motion';
import toast from 'react-hot-toast';

interface HomeworkTabEditorProps {
  lessonId: string;
  onClose: () => void;
}

export function HomeworkTabEditor({ lessonId, onClose }: HomeworkTabEditorProps) {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  
  const [title, setTitle] = useState('');
  const [instructions, setInstructions] = useState('');
  const [isMandatory, setIsMandatory] = useState(true);
  const [passPoints, setPassPoints] = useState(50);
  const [questions, setQuestions] = useState<{ id: string; text: string; order: number; maxPoints: number }[]>([]);

  useEffect(() => {
    loadHomework();
  }, [lessonId]);

  async function loadHomework() {
    try {
      setLoading(true);
      const res = await contentService.getLessonDetail(lessonId);
      const detail = res.data?.data;
      if (detail?.homework) {
        setTitle(detail.homework.title);
        setInstructions(detail.homework.instructions);
        setIsMandatory(detail.homework.isMandatory);
        setPassPoints(detail.homework.requiredPointsToPass);
        setQuestions(detail.homework.questions || []);
      } else {
        setTitle('Homework for Lesson');
        setQuestions([]);
      }
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  }

  function addQuestion() {
    setQuestions([
      ...questions,
      { id: Date.now().toString(), text: '', order: questions.length + 1, maxPoints: 10 }
    ]);
  }

  function updateQuestion(index: number, field: string, value: string | number) {
    const updated = [...questions];
    updated[index] = { ...updated[index], [field]: value };
    setQuestions(updated);
  }

  function removeQuestion(index: number) {
    const updated = [...questions];
    updated.splice(index, 1);
    setQuestions(updated);
  }

  async function handleSave(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    try {
      await adminService.attachHomework(lessonId, {
        title,
        instructions,
        isMandatory,
        requiredPointsToPass: passPoints,
        questions: questions.map((q, i) => ({
          text: q.text,
          order: i + 1,
          maxPoints: q.maxPoints
        }))
      });
      onClose();
    } catch (e) {
      toast.error('فشل في حفظ الواجب');
    } finally {
      setSaving(false);
    }
  }

  return (
    <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} className="fixed inset-0 z-50 flex items-center justify-center bg-[color:rgba(44,23,8,0.5)] backdrop-blur-sm p-4 overflow-y-auto">
      <motion.div initial={{ scale: 0.95 }} animate={{ scale: 1 }} exit={{ scale: 0.95 }} className="w-full max-w-3xl rounded-[24px] bg-[var(--admin-card)] shadow-[0_28px_70px_var(--admin-shadow)] my-8 border border-[var(--admin-border)]">
        <form onSubmit={handleSave} className="flex flex-col h-full max-h-[85vh]">
          <div className="p-6 border-b border-[var(--admin-border)] flex justify-between items-center bg-[var(--admin-card-soft)] rounded-t-[24px]">
            <h3 className="text-xl font-bold text-[var(--admin-text)]">إدارة واجب الدرس</h3>
            <button type="button" onClick={onClose} className="text-[var(--admin-muted)] hover:text-[var(--admin-text)] transition">&times; إغلاق</button>
          </div>
          
          <div className="p-6 overflow-y-auto space-y-6 flex-1">
            {loading ? (
              <div className="animate-pulse text-center py-10 text-[var(--admin-muted)]">جار التنزيل...</div>
            ) : (
              <>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <label className="text-sm font-semibold text-[var(--admin-text)]">عنوان الواجب</label>
                    <input type="text" value={title} onChange={e => setTitle(e.target.value)} required className="admin-input text-right" dir="rtl" />
                  </div>
                  <div className="space-y-2">
                    <label className="text-sm font-semibold text-[var(--admin-text)]">درجة النجاح</label>
                    <input type="number" value={passPoints} onChange={e => setPassPoints(Number(e.target.value))} min={0} className="admin-input" />
                  </div>
                </div>

                <div className="space-y-2">
                  <label className="text-sm font-semibold text-[var(--admin-text)]">التعليمات</label>
                  <textarea value={instructions} onChange={e => setInstructions(e.target.value)} rows={3} className="admin-input text-right" dir="rtl" />
                </div>

                <div className="flex items-center gap-2">
                  <input type="checkbox" id="isMandatory" checked={isMandatory} onChange={e => setIsMandatory(e.target.checked)} className="h-5 w-5 rounded accent-[var(--admin-primary)]" />
                  <label htmlFor="isMandatory" className="text-sm font-semibold text-[var(--admin-text)]">اجباري لفتح الدرس التالي</label>
                </div>

                <div className="pt-4 border-t border-[var(--admin-border)]">
                  <div className="flex justify-between items-center mb-4">
                    <h4 className="font-bold text-lg text-[var(--admin-text)]">الأسئلة ({questions.length})</h4>
                    <button type="button" onClick={addQuestion} className="bg-[var(--admin-primary-15)] text-[var(--admin-primary)] px-4 py-2 rounded-xl text-sm font-bold transition hover:bg-[var(--admin-primary)] hover:text-[var(--admin-primary-contrast)]">
                      + إضافة سؤال مقالي
                    </button>
                  </div>

                  <div className="space-y-4">
                    {questions.map((q, idx) => (
                      <div key={q.id} className="p-4 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] flex gap-4 rtl:flex-row-reverse">
                        <div className="flex-1 space-y-3">
                          <input 
                            type="text" 
                            placeholder="نص السؤال" 
                            value={q.text} 
                            onChange={e => updateQuestion(idx, 'text', e.target.value)} 
                            required 
                            className="admin-input text-right" 
                            dir="rtl"
                          />
                          <div className="flex items-center gap-3">
                            <label className="text-sm text-[var(--admin-muted)]">الدرجة:</label>
                            <input 
                              type="number" 
                              value={q.maxPoints} 
                              onChange={e => updateQuestion(idx, 'maxPoints', Number(e.target.value))} 
                              className="w-24 admin-input"
                            />
                          </div>
                        </div>
                        <button type="button" onClick={() => removeQuestion(idx)} className="text-[#cf6d5b] hover:text-[#b5483a] p-2 transition">
                          &times; حذف
                        </button>
                      </div>
                    ))}
                    {questions.length === 0 && (
                      <p className="text-center text-[var(--admin-muted)] py-6 text-sm">لا توجد أسئلة بعد.</p>
                    )}
                  </div>
                </div>
              </>
            )}
          </div>

          <div className="p-6 border-t border-[var(--admin-border)] flex justify-end gap-3 bg-[var(--admin-card-soft)] rounded-b-[24px]">
            <button type="button" onClick={onClose} className="px-5 py-2 font-semibold text-[var(--admin-muted)] hover:text-[var(--admin-text)] transition">إلغاء</button>
            <button type="submit" disabled={saving || loading} className="admin-btn-primary">
              {saving ? 'جاري الحفظ...' : 'حفظ الواجب'}
            </button>
          </div>
        </form>
      </motion.div>
    </motion.div>
  );
}
