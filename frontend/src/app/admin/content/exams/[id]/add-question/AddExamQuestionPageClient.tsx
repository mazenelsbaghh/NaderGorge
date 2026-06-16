'use client';

import { devConsole } from '@/utils/dev-console';
import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { AdminShellChrome } from '@/components/admin/AdminShellChrome';
import { AdminBackButton } from '@/components/admin/AdminBackButton';
import { QuestionEditor, InlineExamQuestionDto } from '@/components/admin/QuestionEditor';
import { Plus, AlertCircle, Trash2 } from 'lucide-react';
import { adminService, ExamDashboardDto } from '@/services/admin-service';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';

export default function AddExamQuestionPageClient(props: { params: { id: string } }) {
  const params = props.params;
  const router = useRouter();

  const [examData, setExamData] = useState<ExamDashboardDto | null>(null);
  const [loadingContext, setLoadingContext] = useState(true);
  const [saveStatus, setSaveStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle');

  const getDefaultQuestion = (order: number): InlineExamQuestionDto => ({
    text: '',
    type: 'MCQ',
    points: 1,
    order,
    options: [
      { text: '', isCorrect: true },
      { text: '', isCorrect: false },
      { text: '', isCorrect: false },
      { text: '', isCorrect: false },
    ],
  });

  const [currentQuestion, setCurrentQuestion] = useState<InlineExamQuestionDto>(getDefaultQuestion(1));

  useEffect(() => {
    adminService.getExamDashboard(params.id)
      .then(data => {
        setExamData(data);
        setCurrentQuestion(getDefaultQuestion(data.questionCount + 1));
      })
      .catch((err) => {
        toast.error('حدث خطأ أثناء تحميل بيانات الامتحان');
        devConsole.error(err);
      })
      .finally(() => setLoadingContext(false));
  }, [params.id]);

  const handleAddQuestionAndSave = async () => {
    if (currentQuestion.type !== 'FindTheMistake' && !currentQuestion.text.trim()) {
      toast.error('يرجى كتابة نص السؤال');
      return;
    }
    if (currentQuestion.type === 'MCQ') {
      if (currentQuestion.options.length < 2) {
        toast.error('السؤال يجب أن يحتوي على خيارين على الأقل');
        return;
      }
      if (!currentQuestion.options.some((o) => o.isCorrect)) {
        toast.error('السؤال يجب أن يحتوي على خيار واحد صحيح على الأقل');
        return;
      }
    }
    if (currentQuestion.type === 'FindTheMistake') {
      if (!currentQuestion.baseText?.trim()) {
        toast.error('يرجى إدخال النص لسؤال اكتشف الغلطة');
        return;
      }
      if (currentQuestion.mistakeStartIndex === null || currentQuestion.mistakeStartIndex === undefined) {
        toast.error('يرجى تحديد الغلطة في النص');
        return;
      }
    }

    try {
      setSaveStatus('saving');
      
      const cleanQuestion = { ...currentQuestion };
      delete cleanQuestion.audioFile;

      // Save question immediately
      await adminService.addQuestionsToExam(params.id, { questions: [cleanQuestion] });
      
      setSaveStatus('saved');
      toast.success('تم حفظ وإضافة السؤال للامتحان بنجاح!');
      
      // Reload exam stats
      const updatedData = await adminService.getExamDashboard(params.id);
      setExamData(updatedData);
      setCurrentQuestion(getDefaultQuestion(updatedData.questionCount + 1));
    } catch (err: any) {
      setSaveStatus('error');
      toast.error(err.response?.data?.message || 'فشل حفظ السؤال تلقائياً، يرجى المحاولة مجدداً');
    }
  };

  const handleRemoveQuestion = async (examQuestionId: string) => {
    if (!window.confirm('هل أنت متأكد من رغبتك في حذف هذا السؤال؟')) return;
    
    try {
      await adminService.deleteExamQuestion(params.id, examQuestionId);
      toast.success('تم حذف السؤال بنجاح');
      
      const updatedData = await adminService.getExamDashboard(params.id);
      setExamData(updatedData);
      setCurrentQuestion(getDefaultQuestion(updatedData.questionCount + 1));
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'أخفق حذف السؤال');
    }
  };

  return (
    <AdminShellChrome
      activePath="/admin/content"
      sectionLabel="إدارة المحتوى ▸ تعديل الامتحان"
      pageTitle="إضافة أسئلة أُخرى"
      subtitle={`إرفاق أسئلة إضافية مع الحفظ التلقائي الفوري`}
      action={<AdminBackButton />}
    >
      <div className="flex flex-col gap-6">
        
        {loadingContext ? (
           <div className="p-6 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] text-center text-[var(--admin-muted)] animate-pulse font-bold">
              جارٍ تحميل سياق الامتحان...
           </div>
        ) : !examData ? (
           <div className="p-6 rounded-2xl border border-red-200 bg-red-50 text-red-600 font-bold flex items-center justify-center gap-2">
              <AlertCircle className="w-5 h-5" /> لم يتم العثور على الامتحان
           </div>
        ) : (
          <>
            {/* Exam Context Banner */}
            <div className="rounded-2xl border border-[var(--admin-primary)]/20 bg-gradient-to-r from-[var(--admin-primary)]/5 to-transparent p-6 flex flex-col md:flex-row justify-between md:items-center gap-4">
               <div>
                  <h2 className="text-xl font-black text-[var(--admin-text)] mb-1">
                    إضافة أسئلة لـ: <span className="text-[var(--admin-primary)]">{examData.title}</span>
                  </h2>
                  <p className="text-sm font-bold text-[var(--admin-muted)]">
                     مجموع الأسئلة الحالي: {examData.questionCount} أسئلة | الدرجة الحالية المكتملة: {examData.totalScore}
                  </p>
               </div>
            </div>

            {/* Existing Questions List */}
            {examData.questions && examData.questions.length > 0 && (
            <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-sm animate-in fade-in zoom-in-95">
              <div className="p-6 border-b border-[var(--admin-border)] flex flex-col md:flex-row md:items-center justify-between gap-4">
                <h3 className="text-lg font-bold text-[var(--admin-text)]">
                  أسئلة الامتحان الحالية ({examData.questions.length})
                </h3>
              </div>
              
              <div className="p-6 flex flex-col gap-3">
                  {examData.questions.map((q, index) => (
                    <div key={q.examQuestionId} className="flex justify-between items-center p-4 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-background)]">
                       <div className="flex gap-4 items-center">
                          <span className="w-8 h-8 rounded-full bg-[var(--admin-primary)]/10 text-[var(--admin-primary)] flex items-center justify-center font-bold">{index + 1}</span>
                          <p className="font-bold text-sm md:text-base" dangerouslySetInnerHTML={{ __html: q.text.substring(0, 80) + (q.text.length > 80 ? '...' : '') }} />
                       </div>
                       <button 
                         onClick={() => handleRemoveQuestion(q.examQuestionId)}
                         className="text-red-500 hover:bg-red-50 px-3 py-1.5 rounded-lg text-sm font-bold flex items-center gap-1 border border-red-200/50"
                       >
                          <Trash2 size={14} />
                          حذف السؤال
                       </button>
                    </div>
                  ))}
              </div>
            </div>
            )}

            {/* Add New Question Section */}
            <div className="rounded-2xl border border-[var(--admin-primary)] bg-[var(--admin-card)] shadow-sm overflow-hidden mt-2">
              <div className="bg-[var(--admin-primary)]/10 p-4 border-b border-[var(--admin-primary)]/20 flex items-center justify-between">
                 <h3 className="font-bold text-[var(--admin-primary)]">إضافة سؤال جديد (يتم حفظه تلقائياً)</h3>
                 
                 {/* Auto-Save Indicator Component */}
                 <div className="text-xs font-bold">
                   {saveStatus === 'saving' && <span className="text-blue-500 animate-pulse">⏳ جارٍ الحفظ...</span>}
                   {saveStatus === 'saved' && <span className="text-green-500">✓ تم الحفظ بنجاح</span>}
                   {saveStatus === 'error' && <span className="text-red-500">✕ خطأ في الحفظ</span>}
                 </div>
              </div>
              <div className="p-6">
                 <QuestionEditor
                    question={currentQuestion}
                    index={examData.questionCount}
                    onChange={(_, q) => {
                      setCurrentQuestion(q);
                      if (saveStatus !== 'idle') setSaveStatus('idle');
                    }}
                    onRemove={() => {}} // Disabled here
                 />
                 <NeumorphButton
                    type="button"
                    onClick={handleAddQuestionAndSave}
                    disabled={saveStatus === 'saving'}
                    loading={saveStatus === 'saving'}
                    intent="ghost"
                    size="lg"
                    fullWidth
                    className="mt-6 border-dashed border-[var(--admin-primary)] text-[var(--admin-primary)] bg-[var(--admin-primary)]/5 hover:bg-[var(--admin-primary)]/10"
                 >
                    <Plus className="w-5 h-5" />
                    حفظ وإرفاق السؤال تلقائياً للامتحان
                 </NeumorphButton>
              </div>
            </div>

            <div className="flex justify-end pt-4 mt-8">
              <NeumorphButton
                type="button"
                onClick={() => router.back()}
                intent="primary"
                size="xl"
                pill
                className="w-full md:w-auto px-8"
              >
                إنهاء والعودة لبروفايل الامتحان
              </NeumorphButton>
            </div>
          </>
        )}
      </div>
    </AdminShellChrome>
  );
}
