'use client';

import { useState } from 'react';
import { Plus, Save, BookOpen, Video } from 'lucide-react';
import { adminService } from '@/services/admin-service';
import { NumberField } from '@/components/ui/number-field';
import toast from 'react-hot-toast';
import { QuestionEditor, InlineExamQuestionDto } from './QuestionEditor';
import NeumorphButton from '@/components/ui/neumorph-button';

interface InlineExamEditorProps {
  lessonId: string;
  videos: { id: string; title: string }[];
  onSuccess?: () => void;
}

export function InlineExamEditor({ lessonId, videos, onSuccess }: InlineExamEditorProps) {
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [totalScore, setTotalScore] = useState(100);
  const [passingScore, setPassingScore] = useState(50);
  const [durationMinutes, setDurationMinutes] = useState<number | undefined>();
  const [timePerQuestionSeconds, setTimePerQuestionSeconds] = useState<number | undefined>();
  const [targetType, setTargetType] = useState<'Lesson' | 'Video'>('Lesson');
  const [targetVideoId, setTargetVideoId] = useState(videos.length > 0 ? videos[0].id : '');
  
  const [questions, setQuestions] = useState<InlineExamQuestionDto[]>([]);
  const [saving, setSaving] = useState(false);
  
  const [step, setStep] = useState<1 | 2>(1);

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

  const handleNextStep = () => {
    if (!title.trim()) {
      toast.error('يرجى إدخال عنوان للامتحان');
      return;
    }
    if (targetType === 'Video' && !targetVideoId) {
      toast.error('يرجى تحديد فيديو لربط الامتحان به');
      return;
    }
    if (totalScore <= 0) {
      toast.error('الدرجة النهائية يجب أن تكون أكبر من صفر');
      return;
    }
    if (passingScore > totalScore) {
      toast.error('درجة النجاح لا يمكن أن تكون أكبر من الدرجة النهائية');
      return;
    }
    // Step validated, we can submit
    handleSubmit();
  };

  const handleAddQuestionToList = () => {
    if (!currentQuestion.text.trim()) {
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

    setQuestions([...questions, currentQuestion]);
    setCurrentQuestion(getDefaultQuestion(questions.length + 2));
    toast.success('تمت إضافة السؤال للقائمة');
  };
  const handleQuestionChange = (index: number, updated: InlineExamQuestionDto) => {
    const newQuestions = [...questions];
    newQuestions[index] = updated;
    setQuestions(newQuestions);
  };

  const handleRemoveQuestion = (index: number) => {
    setQuestions(questions.filter((_, i) => i !== index));
  };

  const validate = () => {
    if (!title.trim()) {
      toast.error('يرجى إدخال عنوان للامتحان');
      return false;
    }

    if (targetType === 'Video' && !targetVideoId) {
      toast.error('يرجى تحديد فيديو لربط الامتحان به');
      return false;
    }

    if (totalScore <= 0) {
      toast.error('الدرجة النهائية يجب أن تكون أكبر من صفر');
      return false;
    }

    if (passingScore > totalScore) {
      toast.error('درجة النجاح لا يمكن أن تكون أكبر من الدرجة النهائية');
      return false;
    }

    return true;
  };

  async function handleSubmit(e?: React.FormEvent) {
    if (e) e.preventDefault();
    if (!validate()) return;

    try {
      setSaving(true);
      const payload = {
        title,
        description,
        totalScore,
        passingScore,
        durationMinutes: durationMinutes ? Number(durationMinutes) : undefined,
        timePerQuestionSeconds: timePerQuestionSeconds ? Number(timePerQuestionSeconds) : undefined,
        target: {
          type: targetType,
          id: targetType === 'Lesson' ? lessonId : targetVideoId,
        },
        questions,
      };

      await adminService.createInlineExam(payload);
      toast.success('تم إنشاء الامتحان وإضافته بنجاح');
      
      // Reset
      setTitle('');
      setDescription('');
      setDurationMinutes(undefined);
      setTimePerQuestionSeconds(undefined);
      setQuestions([]);
      setCurrentQuestion(getDefaultQuestion(1));
      setStep(1);
      onSuccess?.();
    } catch (error: any) {
      const msg = error.response?.data?.message || 'حدث خطأ أثناء حفظ الامتحان';
      toast.error(msg);
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <form onSubmit={(e) => { e.preventDefault(); handleNextStep(); }} className="flex flex-col gap-6">
          {/* Exam Header Meta */}
          <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
            <h3 className="text-lg font-bold text-[var(--admin-text)] mb-6">إعدادات الامتحان الأساسية</h3>
        
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-6">
          <div className="space-y-4">
            <div className="space-y-2">
              <label className="text-sm font-bold text-[var(--admin-muted)] text-right block w-full text-right" dir="auto">عنوان الامتحان</label>
              <input
                type="text"
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                placeholder="مثال: الاختبار الأول على الدرس التمهيدي"
                className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-background)] px-4 py-3 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] transition-all"
              />
            </div>
            <div className="space-y-2">
              <NumberField
                minValue={1}
                value={durationMinutes}
                onChange={setDurationMinutes}
              >
                <NumberField.Label className="text-sm font-bold text-[var(--admin-muted)] text-right block w-full mb-2" dir="auto">وقت الامتحان الكلي (بالدقائق) (اختياري)</NumberField.Label>
                <NumberField.Group className="h-[46px]">
                  <NumberField.DecrementButton className="shrink-0" />
                  <NumberField.Input 
                    placeholder="امتحان بدون زمن كلي"
                    className="px-2 text-sm text-[var(--admin-text)] font-semibold placeholder:text-sm placeholder:font-normal placeholder:opacity-50" 
                  />
                  <NumberField.IncrementButton className="shrink-0" />
                </NumberField.Group>
              </NumberField>
            </div>
          </div>

          <div className="space-y-4">
            <div className="space-y-2">
              <NumberField
                minValue={1}
                value={timePerQuestionSeconds}
                onChange={setTimePerQuestionSeconds}
              >
                <NumberField.Label className="text-sm font-bold text-[var(--admin-muted)] text-right block w-full mb-2" dir="auto">وقت السؤال الواحد (بالثواني) (اختياري)</NumberField.Label>
                <NumberField.Group className="h-[46px]">
                  <NumberField.DecrementButton className="shrink-0" />
                  <NumberField.Input 
                    placeholder="امتحان بدون زمن محدد للسؤال"
                    className="px-2 text-sm text-[var(--admin-text)] font-semibold placeholder:text-sm placeholder:font-normal placeholder:opacity-50" 
                  />
                  <NumberField.IncrementButton className="shrink-0" />
                </NumberField.Group>
              </NumberField>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <NumberField
                minValue={1}
                value={totalScore}
                onChange={setTotalScore}
              >
                <NumberField.Label className="text-sm font-bold text-[var(--admin-muted)] text-right block w-full mb-2" dir="auto">الدرجة النهائية</NumberField.Label>
                <NumberField.Group className="h-[46px]">
                  <NumberField.DecrementButton />
                  <NumberField.Input />
                  <NumberField.IncrementButton />
                </NumberField.Group>
              </NumberField>
              
              <NumberField
                minValue={1}
                value={passingScore}
                onChange={setPassingScore}
                maxValue={totalScore}
              >
                <NumberField.Label className="text-sm font-bold text-[var(--admin-muted)] text-right block w-full mb-2" dir="auto">درجة النجاح</NumberField.Label>
                <NumberField.Group className="h-[46px]">
                  <NumberField.DecrementButton />
                  <NumberField.Input />
                  <NumberField.IncrementButton />
                </NumberField.Group>
              </NumberField>
            </div>
          </div>
        </div>

        <div className="space-y-2 mb-6">
          <label className="text-sm font-bold text-[var(--admin-muted)] text-right block w-full text-right" dir="auto">وصف أو تعليمات (اختياري)</label>
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="يرجى الإجابة على جميع الأسئلة..."
            className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-background)] px-4 py-3 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] transition-all h-24 resize-none"
          />
        </div>

        {/* Target Scope */}
        <div className="p-4 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-background)]">
          <label className="text-sm font-bold text-[var(--admin-text)] mb-4 block">ارتباط الامتحان (الهدف)</label>
          <div className="flex flex-wrap gap-4">
            <button
              type="button"
              onClick={() => setTargetType('Lesson')}
              className={`flex-1 min-w-[200px] flex items-center justify-center gap-3 p-4 rounded-xl border transition-all ${
                targetType === 'Lesson'
                  ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)]/10 text-[var(--admin-primary)]'
                  : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)] hover:border-[var(--admin-primary)]/50'
              }`}
            >
              <BookOpen className="w-5 h-5" />
              <div className="text-right">
                <span className="block font-bold">اختبار الحصة ككل</span>
                <span className="text-xs opacity-80">يظهر كاختبار نهائي بنهاية الحصة</span>
              </div>
            </button>
            
            <button
              type="button"
              onClick={() => setTargetType('Video')}
              className={`flex-1 min-w-[200px] flex items-center justify-center gap-3 p-4 rounded-xl border transition-all ${
                targetType === 'Video'
                  ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)]/10 text-[var(--admin-primary)]'
                  : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)] hover:border-[var(--admin-primary)]/50'
              }`}
            >
              <Video className="w-5 h-5" />
              <div className="text-right">
                <span className="block font-bold">اختبار للفيديو (Pop Quiz)</span>
                <span className="text-xs opacity-80">يظهر بعد انتهاء الطالب من الفيديو المحدد</span>
              </div>
            </button>
          </div>

          {targetType === 'Video' && (
            <div className="mt-4 animate-in slide-in-from-top-2">
              <label className="text-xs font-bold text-[var(--admin-muted)] block mb-2">تحديد الفيديو المستهدف</label>
              {videos.length === 0 ? (
                <div className="p-3 text-sm text-yellow-600 bg-yellow-50 rounded-lg">
                  لا توجد فيديوهات مسجلة في هذه الحصة حتى الآن. يرجى إضافة فيديو أولاً.
                </div>
              ) : (
                <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-3">
                  <div className="mb-3 rounded-xl bg-[var(--admin-background)] px-4 py-3 text-right">
                    <span className="block text-xs font-bold text-[var(--admin-muted)]">الفيديو المختار</span>
                    <span className="mt-1 block text-sm font-bold text-[var(--admin-text)]">
                      {videos.find((video) => video.id === targetVideoId)?.title || 'اختر الفيديو المطلوب'}
                    </span>
                  </div>

                  <div className="max-h-56 space-y-2 overflow-y-auto pr-1">
                    {videos.map((video) => {
                      const isSelected = video.id === targetVideoId;

                      return (
                        <button
                          key={video.id}
                          type="button"
                          onClick={() => setTargetVideoId(video.id)}
                          className={`w-full rounded-xl border px-4 py-3 text-right transition-all ${
                            isSelected
                              ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)]/10 text-[var(--admin-primary)] shadow-sm'
                              : 'border-[var(--admin-border)] bg-[var(--admin-background)] text-[var(--admin-text)] hover:border-[var(--admin-primary)]/40'
                          }`}
                        >
                          <span className="block text-sm font-bold">{video.title}</span>
                          <span className="mt-1 block text-xs opacity-75">
                            {isSelected ? 'الفيديو المحدد حاليًا' : 'اضغط لاختيار هذا الفيديو'}
                          </span>
                        </button>
                      );
                    })}
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      <div className="flex flex-col md:flex-row justify-end pt-4 gap-4">
        <NeumorphButton
          type="submit"
          disabled={saving}
          loading={saving}
          intent="primary"
          size="xl"
          pill
          fullWidth
          className="md:w-auto"
        >
          إنشاء الامتحان المدمج
        </NeumorphButton>
      </div>
      </form>
    </div>
  );
}
