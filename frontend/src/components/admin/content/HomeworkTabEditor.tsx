'use client';

import { useState, useEffect } from 'react';
import { adminService } from '@/services/admin-service';
import { contentService } from '@/services/content-service';
import { motion, AnimatePresence } from 'framer-motion';
import toast from 'react-hot-toast';
import { Checkbox, Label } from '@/components/ui/checkbox';
import { NumberField } from '@/components/ui/number-field';
import NeumorphButton from '@/components/ui/neumorph-button';

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
  const [totalScore, setTotalScore] = useState(100);
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
        setTotalScore(detail.homework.totalScore || 100);
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
        totalScore,
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
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <div className="space-y-2">
                    <label className="text-sm font-semibold text-[var(--admin-text)]">عنوان الواجب</label>
                    <input type="text" value={title} onChange={e => setTitle(e.target.value)} required className="admin-input text-right" dir="rtl" />
                  </div>
                  <NumberField value={totalScore} onChange={setTotalScore} minValue={1}>
                    <NumberField.Label className="text-sm font-semibold text-[var(--admin-text)] text-right block w-full mb-2">الدرجة النهائية</NumberField.Label>
                    <NumberField.Group className="h-[46px] w-full bg-[var(--admin-card)] hover:shadow-none">
                      <NumberField.DecrementButton />
                      <NumberField.Input className="bg-[var(--admin-card)]" />
                      <NumberField.IncrementButton />
                    </NumberField.Group>
                  </NumberField>
                  <NumberField value={passPoints} onChange={setPassPoints} minValue={0} maxValue={totalScore}>
                    <NumberField.Label className="text-sm font-semibold text-[var(--admin-text)] text-right block w-full mb-2">درجة النجاح</NumberField.Label>
                    <NumberField.Group className="h-[46px] w-full bg-[var(--admin-card)] hover:shadow-none">
                      <NumberField.DecrementButton />
                      <NumberField.Input className="bg-[var(--admin-card)]" />
                      <NumberField.IncrementButton />
                    </NumberField.Group>
                  </NumberField>
                </div>

                <div className="space-y-2">
                  <label className="text-sm font-semibold text-[var(--admin-text)]">التعليمات</label>
                  <textarea value={instructions} onChange={e => setInstructions(e.target.value)} rows={3} className="admin-input text-right" dir="rtl" />
                </div>

                <Checkbox id="isMandatory" isSelected={isMandatory} onChange={setIsMandatory}>
                  <Checkbox.Control>
                    <Checkbox.Indicator />
                  </Checkbox.Control>
                  <Checkbox.Content>
                    <Label className="text-sm font-semibold text-[var(--admin-text)]">اجباري لفتح الدرس التالي</Label>
                  </Checkbox.Content>
                </Checkbox>

                <div className="pt-4 border-t border-[var(--admin-border)]">
                  <div className="flex justify-between items-center mb-4">
                    <h4 className="font-bold text-lg text-[var(--admin-text)]">الأسئلة ({questions.length})</h4>
                    <NeumorphButton type="button" onClick={addQuestion} intent="ghost" size="sm">
                      + إضافة سؤال مقالي
                    </NeumorphButton>
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
                            <NumberField value={q.maxPoints} onChange={val => updateQuestion(idx, 'maxPoints', val)} minValue={1} className="w-32">
                              <NumberField.Group className="h-[42px] w-full bg-[var(--admin-card)] hover:shadow-none">
                                <NumberField.DecrementButton className="w-8 flex-shrink-0" />
                                <NumberField.Input className="bg-[var(--admin-card)] text-xs p-1" />
                                <NumberField.IncrementButton className="w-8 flex-shrink-0" />
                              </NumberField.Group>
                            </NumberField>
                            <label className="text-sm font-bold text-[var(--admin-muted)] mt-1 whitespace-nowrap">الدرجة:</label>
                          </div>
                        </div>
                        <NeumorphButton type="button" onClick={() => removeQuestion(idx)} intent="danger" size="sm">
                          × حذف
                        </NeumorphButton>
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
            <NeumorphButton type="button" onClick={onClose} intent="ghost" size="md">إلغاء</NeumorphButton>
            <NeumorphButton type="submit" disabled={saving || loading} loading={saving} intent="primary" size="md" pill>
              حفظ الواجب
            </NeumorphButton>
          </div>
        </form>
      </motion.div>
    </motion.div>
  );
}
