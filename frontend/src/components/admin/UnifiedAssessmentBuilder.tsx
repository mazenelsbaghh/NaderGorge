'use client';

import { useState } from 'react';
import { Save, BookOpen, Video, Shuffle } from 'lucide-react';
import { adminService } from '@/services/admin-service';
import { NumberField } from '@/components/ui/number-field';
import { Checkbox, Label } from '@/components/ui/checkbox';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';

interface UnifiedAssessmentBuilderProps {
  type: 'exam' | 'homework';
  lessonId: string;
  videos?: { id: string; title: string }[];
  onSuccess?: () => void;
  forceTargetType?: 'Lesson' | 'Video';
}

export function UnifiedAssessmentBuilder({ 
  type, 
  lessonId, 
  videos = [], 
  onSuccess,
  forceTargetType
}: UnifiedAssessmentBuilderProps) {
  const isExam = type === 'exam';

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [totalScore, setTotalScore] = useState(100);
  const [passingScore, setPassingScore] = useState(50);
  const [durationMinutes, setDurationMinutes] = useState<number | undefined>();
  const [displayQuestionCount, setDisplayQuestionCount] = useState<number | undefined>();
  const [targetType, setTargetType] = useState<'Lesson' | 'Video'>(forceTargetType || 'Lesson');
  const [targetVideoId, setTargetVideoId] = useState(videos.length > 0 ? videos[0].id : '');
  
  // New Toggles
  const [isMandatory, setIsMandatory] = useState(true);
  const [isRandomized, setIsRandomized] = useState(false);
  
  const [saving, setSaving] = useState(false);



  const validate = () => {
    if (!title.trim()) {
      toast.error(isExam ? 'يرجى إدخال عنوان للامتحان' : 'يرجى إدخال عنوان للواجب');
      return false;
    }

    if (isExam && targetType === 'Video' && !targetVideoId) {
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

  const handleNextStep = () => {
    if (validate()) {
      handleSubmit();
    }
  };

  async function handleSubmit(e?: React.FormEvent) {
    if (e) e.preventDefault();
    if (!validate()) return;

    try {
      setSaving(true);
      
      if (isExam) {
        // Exam payload mapping
        const payload = {
          title,
          description,
          totalScore,
          passingScore,
          isMandatory,
          isRandomized,
          displayQuestionCount: displayQuestionCount ? Number(displayQuestionCount) : undefined,
          durationMinutes: durationMinutes ? Number(durationMinutes) : undefined,
          targetVideoId: targetType === 'Video' ? targetVideoId : undefined,
          lessonId: targetType === 'Lesson' ? lessonId : undefined, // Assuming adminService uses target object or sends this
          // The current adminService.createInlineExam uses { target: { type, id } } in the old code. We will refactor backend to accept this flat or keep the old format:
          target: {
            type: targetType,
            id: targetType === 'Lesson' ? lessonId : targetVideoId,
          },
          questions: [],
        };
        await adminService.createInlineExam(payload);
        toast.success('تم إنشاء الامتحان وإضافته بنجاح');
      } else {
        const payload = {
          title,
          instructions: description,
          isMandatory,
          isRandomized,
          totalScore,
          requiredPointsToPass: passingScore,
          questions: [],
        };
        await adminService.attachHomework(lessonId, payload as any);
        toast.success('تم إنشاء الواجب بنجاح');
      }
      
      // Reset
      setTitle('');
      setDescription('');
      setDurationMinutes(undefined);
      setDisplayQuestionCount(undefined);
      setIsMandatory(true);
      setIsRandomized(false);
      onSuccess?.();
    } catch (error: any) {
      const msg = error.response?.data?.message || `حدث خطأ أثناء حفظ ${isExam ? 'الامتحان' : 'الواجب'}`;
      toast.error(msg);
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <form onSubmit={(e) => { e.preventDefault(); handleNextStep(); }} className="flex flex-col gap-6 relative">
        <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
          <div className="flex flex-col gap-6 mb-6 max-w-3xl">
            <div className="space-y-2">
              <label className="text-sm font-bold text-[var(--admin-muted)] text-right block w-full" dir="auto">
                عنوان {isExam ? 'الامتحان' : 'الواجب'}
              </label>
              <input
                type="text"
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                placeholder={`مثال: ${isExam ? 'الاختبار الأول' : 'الواجب الأسبوعي'} على الدرس`}
                className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-background)] px-4 py-3 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] transition-all"
              />
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
              {isExam && (
                <div className="space-y-4">
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

                  <NumberField
                    minValue={1}
                    value={displayQuestionCount}
                    onChange={setDisplayQuestionCount}
                  >
                    <NumberField.Label className="text-sm font-bold text-[var(--admin-muted)] text-right block w-full mb-2" dir="auto">عدد الأسئلة المعروضة للطالب (اختياري)</NumberField.Label>
                    <NumberField.Group className="h-[46px]">
                      <NumberField.DecrementButton className="shrink-0" />
                      <NumberField.Input 
                        placeholder="عرض كل الأسئلة المضافة"
                        className="px-2 text-sm text-[var(--admin-text)] font-semibold placeholder:text-sm placeholder:font-normal placeholder:opacity-50" 
                      />
                      <NumberField.IncrementButton className="shrink-0" />
                    </NumberField.Group>
                  </NumberField>
                </div>
              )}

              <div className="grid grid-cols-2 gap-4 col-span-1 sm:col-span-1">
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

            <div className="space-y-4 pt-2">
              <div className="flex items-center">
                <Checkbox isSelected={isMandatory} onChange={setIsMandatory}>
                  <Checkbox.Control>
                    <Checkbox.Indicator />
                  </Checkbox.Control>
                  <Checkbox.Content>
                    <Label className="font-bold">{isExam ? 'الامتحان إلزامي' : 'الواجب إلزامي'} <span className="text-xs font-normal opacity-70 block">لا يمكن فتح الدرس التالي حتى يتم اجتيازه</span></Label>
                  </Checkbox.Content>
                </Checkbox>
              </div>

              <div className="flex items-center">
                <Checkbox isSelected={isRandomized} onChange={setIsRandomized}>
                  <Checkbox.Control>
                    <Checkbox.Indicator />
                  </Checkbox.Control>
                  <Checkbox.Content>
                    <Label className="font-bold flex items-center gap-2">ترتيب أسئلة عشوائي <Shuffle className="w-4 h-4 text-blue-500" /></Label>
                  </Checkbox.Content>
                </Checkbox>
              </div>
            </div>

            <div className="space-y-2">
              <label className="text-sm font-bold text-[var(--admin-muted)] text-right block w-full" dir="auto">وصف أو تعليمات (اختياري)</label>
              <textarea
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="يرجى الإجابة على جميع الأسئلة..."
                className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-background)] px-4 py-3 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] transition-all h-24 resize-none"
              />
            </div>

            {/* Target Scope for Exams */}
            {isExam && (
              <div className="pt-4 border-t border-[var(--admin-border)] block w-full">
                {!forceTargetType && (
                  <>
                    <label className="text-sm font-bold text-[var(--admin-text)] mb-4 block">ارتباط الامتحان (الهدف)</label>
                    <div className="flex flex-col sm:flex-row gap-3">
                      <button
                        type="button"
                        onClick={() => setTargetType('Lesson')}
                        className={`flex-1 flex items-center justify-start gap-4 p-4 rounded-xl border transition-all ${
                          targetType === 'Lesson'
                            ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)]/10 text-[var(--admin-primary)]'
                            : 'border-[var(--admin-border)] bg-[var(--admin-background)] text-[var(--admin-muted)] hover:border-[var(--admin-primary)]/50'
                        }`}
                      >
                        <BookOpen className="w-5 h-5 opacity-80" />
                        <div className="text-right">
                          <span className="block font-bold">اختبار الحصة ككل</span>
                          <span className="text-xs opacity-80">امتحان عام شامل نهاية الحصة</span>
                        </div>
                      </button>
                      
                      <button
                        type="button"
                        onClick={() => setTargetType('Video')}
                        className={`flex-1 flex items-center justify-start gap-4 p-4 rounded-xl border transition-all ${
                          targetType === 'Video'
                            ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)]/10 text-[var(--admin-primary)]'
                            : 'border-[var(--admin-border)] bg-[var(--admin-background)] text-[var(--admin-muted)] hover:border-[var(--admin-primary)]/50'
                        }`}
                      >
                        <Video className="w-5 h-5 opacity-80" />
                        <div className="text-right">
                          <span className="block font-bold">اختبار لفيديو (Pop Quiz)</span>
                          <span className="text-xs opacity-80">يظهر بعد انتهاء الطالب من الفيديو</span>
                        </div>
                      </button>
                    </div>
                  </>
                )}

                {targetType === 'Video' && (
                  <div className="mt-4 animate-in slide-in-from-top-2">
                    <label className="text-xs font-bold text-[var(--admin-muted)] block mb-2">تحديد الفيديو المستهدف</label>
                    {videos.length === 0 ? (
                      <div className="p-3 text-sm text-yellow-600 bg-yellow-50 rounded-lg">
                        لا توجد فيديوهات مضافة للحصة بعد.
                      </div>
                    ) : (
                      <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-background)] p-3">
                        <div className="max-h-48 space-y-2 overflow-y-auto pr-1">
                          {videos.map((video) => {
                            const isSelected = video.id === targetVideoId;
                            return (
                              <button
                                key={video.id}
                                type="button"
                                onClick={() => setTargetVideoId(video.id)}
                                className={`w-full rounded-xl border px-4 py-3 text-right transition-all ${
                                  isSelected
                                    ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)] text-[var(--admin-card)] shadow-md'
                                    : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-text)] hover:border-[var(--admin-primary)]/40'
                                }`}
                              >
                                <span className="block text-sm font-bold truncate">{video.title}</span>
                              </button>
                            );
                          })}
                        </div>
                      </div>
                    )}
                  </div>
                )}
              </div>
            )}


            
            <div className="mt-8 pt-6 border-t border-[var(--admin-border)] flex justify-end">
              <NeumorphButton
                type="submit"
                disabled={saving}
                loading={saving}
                intent="primary"
                size="xl"
                pill
                className="w-full sm:w-auto px-10 shadow-lg shadow-blue-500/20"
              >
                <Save className="w-5 h-5 ml-2" />
                {saving 
                  ? 'يتم الحفظ...' 
                  : isExam 
                    ? targetType === 'Video' 
                      ? 'إنشاء امتحان الفيديو (Pop Quiz)' 
                      : 'إنشاء امتحان الحصة ككل' 
                    : 'إنشاء الواجب الأساسي'}
              </NeumorphButton>
            </div>
          </div>
        </div>
      </form>
    </div>
  );
}
