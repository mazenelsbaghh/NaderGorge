'use client';

import { devConsole } from '@/utils/dev-console';
import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { AdminShellChrome } from '@/components/admin/AdminShellChrome';
import { AdminBackButton } from '@/components/admin/AdminBackButton';
import { QuestionEditor, InlineExamQuestionDto } from '@/components/admin/QuestionEditor';
import { Plus, Save, AlertCircle } from 'lucide-react';
import { adminService, ExamDashboardDto } from '@/services/admin-service';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';

export default function AddExamQuestionPageClient(props: { params: { id: string } }) {
  const params = props.params;
  const router = useRouter();

  const [examData, setExamData] = useState<ExamDashboardDto | null>(null);
  const [loadingContext, setLoadingContext] = useState(true);

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

  const [questions, setQuestions] = useState<InlineExamQuestionDto[]>([]);
  const [currentQuestion, setCurrentQuestion] = useState<InlineExamQuestionDto>(getDefaultQuestion(1));
  const [saving, setSaving] = useState(false);

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

  const handleAddQuestionToList = () => {
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

    setQuestions([...questions, currentQuestion]);
    const nextOrder = (examData?.questionCount || 0) + questions.length + 2;
    setCurrentQuestion(getDefaultQuestion(nextOrder));
    toast.success('تمت إضافة السؤال للقائمة المؤقتة، لا تنس الحفظ');
  };

  const handleRemoveQuestion = (index: number) => {
    setQuestions(questions.filter((_, i) => i !== index));
  };

  const handleSubmit = async () => {
     if (questions.length === 0) {
        toast.error('يجب تقديم سؤال واحد على الأقل للحفظ');
        return;
     }
     
     try {
       setSaving(true);
       const cleanQuestions = questions.map((question) => {
         const cleanQuestion = { ...question };
         delete cleanQuestion.audioFile;
         return cleanQuestion;
       });
       await adminService.addQuestionsToExam(params.id, { questions: cleanQuestions });
       toast.success('تم رفع الأسئلة وإضافتها للامتحان بنجاح!');
       router.back();
     } catch (err: any) {
        toast.error(err.response?.data?.message || 'حدث خطأ أثناء حفظ الأسئلة');
     } finally {
        setSaving(false);
     }
  };

  return (
    <AdminShellChrome
      activePath="/admin/content"
      sectionLabel="إدارة المحتوى ▸ تعديل الامتحان"
      pageTitle="إضافة أسئلة أُخرى"
      subtitle={`إرفاق أسئلة إضافية لامتحان موجود مسبقاً (${params.id.split('-')[0]})`}
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

            {/* Added Questions List */}
            {questions.length > 0 && (
            <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-sm animate-in fade-in zoom-in-95">
              <div className="p-6 border-b border-[var(--admin-border)] flex flex-col md:flex-row md:items-center justify-between gap-4">
                <h3 className="text-lg font-bold text-[var(--admin-text)]">
                  الأسئلة الجديدة المراد رفعها ({questions.length})
                </h3>
                <span className="bg-[var(--admin-background)] px-3 py-1 rounded-full text-sm font-bold text-[var(--admin-muted)] border border-[var(--admin-border)]">
                  إجمالي النقاط المضافة: {questions.reduce((sum, q) => sum + (q.points || 0), 0)}
                </span>
              </div>
              
              <div className="p-6 flex flex-col gap-3">
                  {questions.map((q, index) => (
                    <div key={index} className="flex justify-between items-center p-4 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-background)]">
                       <div className="flex gap-4 items-center">
                          <span className="w-8 h-8 rounded-full bg-[var(--admin-primary)]/10 text-[var(--admin-primary)] flex items-center justify-center font-bold">{(examData.questionCount || 0) + index + 1}</span>
                          <p className="font-bold text-sm md:text-base">{q.text.substring(0, 50)}{q.text.length > 50 ? '...' : ''}</p>
                       </div>
                       <button 
                         onClick={() => handleRemoveQuestion(index)}
                         className="text-red-500 hover:bg-red-50 p-2 rounded-lg text-sm font-bold"
                       >
                          حذف الإضافة
                       </button>
                    </div>
                  ))}
              </div>
            </div>
            )}

            {/* Add New Question Section */}
            <div className="rounded-2xl border border-[var(--admin-primary)] bg-[var(--admin-card)] shadow-sm overflow-hidden mt-2">
              <div className="bg-[var(--admin-primary)]/10 p-4 border-b border-[var(--admin-primary)]/20">
                 <h3 className="font-bold text-[var(--admin-primary)] text-center">إضافة سؤال جديد للقائمة</h3>
              </div>
              <div className="p-6">
                 <QuestionEditor
                    question={currentQuestion}
                    index={0}
                    onChange={(_, q) => setCurrentQuestion(q)}
                    onRemove={() => {}} // Disabled here
                 />
                 <NeumorphButton
                    type="button"
                    onClick={handleAddQuestionToList}
                    intent="ghost"
                    size="lg"
                    fullWidth
                    className="mt-6 border-dashed border-[var(--admin-primary)] text-[var(--admin-primary)] bg-[var(--admin-primary)]/5 hover:bg-[var(--admin-primary)]/10"
                 >
                    <Plus className="w-5 h-5" />
                    تحضير السؤال للرفع
                 </NeumorphButton>
              </div>
            </div>

            <div className="flex flex-col-reverse md:flex-row justify-end pt-4 mt-8 gap-4">
              <NeumorphButton
                type="button"
                onClick={() => router.back()}
                intent="ghost"
                size="xl"
                pill
                className="w-full md:w-auto"
              >
                رجوع وإلغاء
              </NeumorphButton>
              <NeumorphButton
                type="button"
                onClick={handleSubmit}
                disabled={saving || questions.length === 0}
                loading={saving}
                intent="primary"
                size="xl"
                pill
                className="w-full md:w-auto"
              >
                <Save className="w-5 h-5" />
                حفظ وإرفاق الأسئلة للامتحان
              </NeumorphButton>
            </div>
          </>
        )}
      </div>
    </AdminShellChrome>
  );
}
