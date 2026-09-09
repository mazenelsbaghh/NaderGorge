'use client';

import { devConsole } from '@/utils/dev-console';
import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { AdminPage } from '@/components/admin/AdminShellChrome';
import { AdminBackButton } from '@/components/admin/AdminBackButton';
import { TeacherShellChrome } from '@/components/teacher/TeacherShellChrome';
import { QuestionEditor, InlineExamQuestionDto } from '@/components/admin/QuestionEditor';
import { OcrQuestionImport } from '@/components/admin/OcrQuestionImport';
import { Plus, Save, AlertCircle, Trash2, ArrowRight, Pencil } from 'lucide-react';
import { adminService, HomeworkDashboardDto } from '@/services/admin-service';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';
import { questionTextToPlainText } from '@/lib/question-text';
import { getApiErrorSummary } from '@/lib/api-errors';

export default function AddHomeworkQuestionPageClient(props: { params: { id: string }; surface?: 'admin' | 'teacher'; initialQuestionId?: string }) {
  const params = props.params;
  const surface = props.surface ?? 'admin';
  const router = useRouter();

  const [homeworkData, setHomeworkData] = useState<HomeworkDashboardDto | null>(null);
  const [loadingContext, setLoadingContext] = useState(true);

  const getDefaultQuestion = (order: number): InlineExamQuestionDto => ({
    text: '',
    type: 'Essay',
    points: 10,
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
  const [saveError, setSaveError] = useState<string | null>(null);
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const lockedReason = homeworkData?.submissions.length
    ? 'لا يمكن تعديل الأسئلة بعد بدء الطلاب في الواجب، حفاظًا على إجاباتهم ونتائجهم.'
    : homeworkData?.isActive ? 'عطّل الواجب أولًا من بروفايله قبل تعديل الأسئلة.' : null;

  useEffect(() => {
    adminService.getHomeworkDashboard(params.id)
      .then(data => {
        setHomeworkData(data);
        // Map existing questions to the editor state
        if (data.questions && data.questions.length > 0) {
          const mapped = data.questions.map((q, idx) => ({
            text: q.text,
            type: q.type,
            points: q.points,
            order: idx + 1,
            options: q.possibleAnswers ? q.possibleAnswers.map((ansText) => ({
              text: ansText,
              isCorrect: ansText === q.correctAnswerKey
            })) : [],
            audioUrl: q.audioUrl || undefined,
            imageUrl: q.imageUrl || undefined,
            writtenCorrection: q.writtenCorrection || undefined,
            hintText: q.hintText || undefined,
            baseText: q.baseText || undefined,
            mistakeStartIndex: q.mistakeStartIndex,
            mistakeEndIndex: q.mistakeEndIndex
          }));
          setQuestions(mapped);
          const selectedIndex = data.questions.findIndex(question => question.homeworkQuestionId === props.initialQuestionId);
          setEditingIndex(selectedIndex >= 0 ? selectedIndex : null);
          setCurrentQuestion(getDefaultQuestion(mapped.length + 1));
        } else {
          setCurrentQuestion(getDefaultQuestion(1));
        }
      })
      .catch((err) => {
        toast.error('حدث خطأ أثناء تحميل بيانات الواجب');
        devConsole.error(err);
      })
      .finally(() => setLoadingContext(false));
  }, [params.id, props.initialQuestionId]);

  useEffect(() => {
    if (editingIndex !== null) document.getElementById(`homework-edit-${editingIndex}`)?.scrollIntoView({ block: 'center' });
  }, [editingIndex]);

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
    const nextOrder = questions.length + 2;
    setCurrentQuestion(getDefaultQuestion(nextOrder));
    toast.success('تمت إضافة السؤال للقائمة المؤقتة، لا تنس الحفظ');
  };

  const handleRemoveQuestion = (index: number) => {
    setQuestions(questions.filter((_, i) => i !== index));
    setEditingIndex(null);
  };

  const handleOcrImport = (importedQuestions: InlineExamQuestionDto[]) => {
    setQuestions((current) => [
      ...current,
      ...importedQuestions.map((question, index) => ({
        ...question,
        order: current.length + index + 1,
      })),
    ]);
    toast.success(`تمت إضافة ${importedQuestions.length} سؤال للقائمة.`);
  };

  const handleSubmit = async () => {
     if (!homeworkData) return;
     if (lockedReason) {
       toast.error(lockedReason);
       return;
     }
     if (questions.length === 0) {
        toast.error('يجب تقديم سؤال واحد على الأقل للحفظ');
        return;
     }
     
     try {
       setSaving(true);
       setSaveError(null);
       const cleanQuestions = questions.map((q, idx) => ({
         text: q.text,
         order: idx + 1,
         points: q.points,
         type: q.type,
         options: q.options || [],
         audioUrl: q.audioUrl,
         imageUrl: q.imageUrl,
         writtenCorrection: q.writtenCorrection,
         hintText: q.hintText,
         baseText: q.baseText,
         mistakeStartIndex: q.mistakeStartIndex,
         mistakeEndIndex: q.mistakeEndIndex
       }));

       await adminService.attachHomework(homeworkData.lessonId, {
         title: homeworkData.title,
         instructions: homeworkData.description || '',
         isMandatory: homeworkData.isMandatory,
         isRandomized: homeworkData.isRandomized,
         totalScore: homeworkData.totalScore,
         requiredPointsToPass: homeworkData.passingScore,
         questions: cleanQuestions
       });

       toast.success('تم حفظ أسئلة الواجب بنجاح!');
       router.back();
     } catch (error: unknown) {
        setSaveError(getApiErrorSummary(error, 'حدث خطأ أثناء حفظ أسئلة الواجب'));
     } finally {
        setSaving(false);
     }
  };

  const content = (
      <div className="flex flex-col gap-6">
        
        {loadingContext ? (
           <div className="p-6 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] text-center text-[var(--admin-muted)] animate-pulse font-bold">
              جارٍ تحميل سياق الواجب...
           </div>
        ) : !homeworkData ? (
           <div className="p-6 rounded-2xl border border-red-200 bg-red-50 text-red-600 font-bold flex items-center justify-center gap-2">
              <AlertCircle className="w-5 h-5" /> لم يتم العثور على الواجب
           </div>
        ) : (
           <>
             {lockedReason && <p role="status" className="rounded-xl border border-[var(--admin-border)] p-4 text-sm font-bold">{lockedReason}</p>}
             <fieldset disabled={Boolean(lockedReason) || saving} className="min-w-0 space-y-6 disabled:opacity-60">
             {/* Homework Context Banner */}
             <div className="rounded-2xl border border-[var(--admin-primary)]/20 bg-gradient-to-r from-[var(--admin-primary)]/5 to-transparent p-6 flex flex-col md:flex-row justify-between md:items-center gap-4">
                <div>
                   <h2 className="text-xl font-black text-[var(--admin-text)] mb-1">
                     إدارة أسئلة: <span className="text-[var(--admin-primary)]">{homeworkData.title}</span>
                   </h2>
                   <p className="text-sm font-bold text-[var(--admin-muted)]">
                      مجموع الأسئلة: {questions.length} أسئلة | الدرجة الإجمالية: {homeworkData.totalScore}
                   </p>
                </div>
             </div>

             {/* Added Questions List */}
             {questions.length > 0 && (
             <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-sm">
               <div className="p-6 border-b border-[var(--admin-border)] flex flex-col md:flex-row md:items-center justify-between gap-4">
                 <h3 className="text-lg font-bold text-[var(--admin-text)]">
                   أسئلة الواجب الحالية ({questions.length})
                 </h3>
                 <span className="bg-[var(--admin-background)] px-3 py-1 rounded-full text-sm font-bold text-[var(--admin-muted)] border border-[var(--admin-border)]">
                   إجمالي النقاط: {questions.reduce((sum, q) => sum + (q.points || 0), 0)}
                 </span>
               </div>
               
               <div className="p-6 flex flex-col gap-3">
                   {questions.map((q, index) => (
                     <div key={index} id={`homework-edit-${index}`} className="min-w-0 space-y-4 p-4 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-background)]">
                       <div className="flex flex-wrap justify-between items-center gap-3">
                        <div className="flex min-w-0 flex-1 gap-4 items-center">
                           <span className="w-8 h-8 rounded-full bg-[var(--admin-primary)]/10 text-[var(--admin-primary)] flex items-center justify-center font-bold">{index + 1}</span>
                           <div className="min-w-0 break-words font-bold text-sm md:text-base">{questionTextToPlainText(q.text).slice(0, 100)}{questionTextToPlainText(q.text).length > 100 ? '...' : ''}</div>
                        </div>
                        <button type="button" onClick={() => setEditingIndex(editingIndex === index ? null : index)} className="admin-btn-ghost inline-flex min-h-11 items-center gap-2" aria-expanded={editingIndex === index} aria-label={`تعديل السؤال ${index + 1}`}><Pencil className="h-4 w-4" />{editingIndex === index ? 'إغلاق التعديل' : 'تعديل السؤال'}</button>
                        <button 
                          type="button"
                          onClick={() => handleRemoveQuestion(index)}
                          className="text-red-500 hover:bg-red-50 p-2 rounded-lg text-sm font-bold flex items-center gap-1"
                        >
                           <Trash2 className="w-4 h-4" /> حذف السؤال
                        </button>
                       </div>
                       {editingIndex === index && <div className="min-w-0 border-t border-[var(--admin-border)] pt-4">
                         <QuestionEditor question={q} index={index} onChange={(_, updatedQuestion) => setQuestions(current => current.map((question, questionIndex) => questionIndex === index ? updatedQuestion : question))} onRemove={() => handleRemoveQuestion(index)} />
                         <p className="mt-3 text-sm font-bold text-[var(--admin-muted)]">التعديل مؤقت حتى تضغط «حفظ التغييرات بالكامل».</p>
                       </div>}
                     </div>
                   ))}
               </div>
             </div>
             )}

             <OcrQuestionImport
               nextOrder={questions.length + 1}
               onImport={handleOcrImport}
             />

             {/* Add New Question Section */}
             <div className="rounded-2xl border border-[var(--admin-primary)] bg-[var(--admin-card)] shadow-sm overflow-hidden mt-2">
               <div className="bg-[var(--admin-primary)]/10 p-4 border-b border-[var(--admin-primary)]/20">
                  <h3 className="font-bold text-[var(--admin-primary)] text-center">إضافة سؤال للواجب</h3>
               </div>
               <div className="p-6">
                  <QuestionEditor
                     question={currentQuestion}
                     index={questions.length}
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
                     إضافة هذا السؤال للواجب
                  </NeumorphButton>
               </div>
             </div>

             {saveError && <p role="alert" className="rounded-xl border border-[var(--admin-danger)] p-4 font-bold text-[var(--admin-danger)]">{saveError}</p>}
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
                 حفظ التغييرات بالكامل
               </NeumorphButton>
             </div>
             </fieldset>
           </>
        )}
      </div>
  );

  if (surface === 'teacher') {
    return (
      <TeacherShellChrome
        activePath="/teacher/packages"
        sectionLabel="المحتوى الدراسي ▸ أسئلة الواجب"
        pageTitle="أسئلة الواجب"
        subtitle={`إدارة أسئلة واجب الحصة (${params.id.split('-')[0]})`}
        action={
          <button type="button" onClick={() => router.back()} className="admin-btn-ghost inline-flex items-center gap-2">
            <ArrowRight className="h-4 w-4" />
            رجوع
          </button>
        }
      >
        {content}
      </TeacherShellChrome>
    );
  }

  return (
    <AdminPage
      activePath="/admin/content"
      sectionLabel="إدارة المحتوى ▸ تعديل الواجب"
      pageTitle="أسئلة الواجب"
      subtitle={`إرفاق أسئلة إضافية لواجب موجود مسبقاً (${params.id.split('-')[0]})`}
      action={<AdminBackButton />}
    >
      {content}
    </AdminPage>
  );
}
