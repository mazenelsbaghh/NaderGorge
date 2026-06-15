'use client';

import { devConsole } from '@/utils/dev-console';
import { useEffect, useState, useCallback, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { 
  BookCheck, 
  FileQuestion, 
  GraduationCap, 
  LayoutList, 
  Timer, 
  Plus, 
  BarChart3, 
  Trash2, 
  Edit, 
  User as UserIcon, 
  Save, 
  X,
  AlertCircle,
  HelpCircle,
  Play
} from 'lucide-react';
import { 
  AdminShellChrome, 
  AdminStatCard, 
  AdminPageSkeleton, 
  AdminBackButton 
} from '@/components/admin';
import { QuestionEditor, InlineExamQuestionDto } from '@/components/admin/QuestionEditor';
import { adminService, type ExamDashboardDto } from '@/services/admin-service';
import NeumorphButton from '@/components/ui/neumorph-button';
import toast from 'react-hot-toast';

export default function ExamProfilePageClient({ id }: { id: string }) {
  const router = useRouter();
  const [data, setData] = useState<ExamDashboardDto | null>(null);
  const [loading, setLoading] = useState(true);
  
  // Tabs: 'questions' | 'attempts'
  const [activeTab, setActiveTab] = useState<'questions' | 'attempts'>('questions');
  
  // Search query for student attempts
  const [searchQuery, setSearchQuery] = useState('');

  // Editing state
  const [editingQuestionId, setEditingQuestionId] = useState<string | null>(null);
  const [editingQuestionData, setEditingQuestionData] = useState<InlineExamQuestionDto | null>(null);
  const [savingEdit, setSavingEdit] = useState(false);

  // Adding question state
  const [isAddingQuestion, setIsAddingQuestion] = useState(false);
  const [addingQuestionData, setAddingQuestionData] = useState<InlineExamQuestionDto | null>(null);
  const [savingAdd, setSavingAdd] = useState(false);

  const loadData = useCallback(async () => {
    try {
      setLoading(true);
      const dashboardData = await adminService.getExamDashboard(id);
      setData(dashboardData || null);
    } catch (error) {
      devConsole.error(error);
      toast.error('أخفق تحميل بيانات الامتحان');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Average score calculator
  const averageScore = useMemo(() => {
    if (!data?.attempts || data.attempts.length === 0) return 0;
    const total = data.attempts.reduce((sum, a) => sum + (a.scoreAchieved || 0), 0);
    return Math.round((total / data.attempts.length) * 100) / 100;
  }, [data?.attempts]);

  // Filtered attempts
  const filteredAttempts = useMemo(() => {
    if (!data?.attempts) return [];
    if (!searchQuery.trim()) return data.attempts;
    const q = searchQuery.toLowerCase().trim();
    return data.attempts.filter(a => 
      a.studentName?.toLowerCase().includes(q) || 
      a.studentPhone?.toLowerCase().includes(q)
    );
  }, [data?.attempts, searchQuery]);

  // Map backend DTO to QuestionEditor DTO
  const mapDtoToQuestion = (q: any, order: number): InlineExamQuestionDto => ({
    text: q.text,
    type: q.type === 'MCQ' || q.type === 'Essay' || q.type === 'FindTheMistake' ? q.type : 'MCQ',
    points: q.points,
    order: order,
    options: q.options ? q.options.map((o: any) => ({ text: o.text, isCorrect: o.isCorrect })) : [],
    audioUrl: q.audioUrl || '',
    writtenCorrection: q.writtenCorrection || '',
    hintText: q.hintText || '',
    baseText: q.baseText || '',
    mistakeStartIndex: q.mistakeStartIndex,
    mistakeEndIndex: q.mistakeEndIndex
  });

  const handleEditClick = (q: any, idx: number) => {
    setEditingQuestionId(q.examQuestionId);
    setEditingQuestionData(mapDtoToQuestion(q, idx + 1));
  };

  const handleEditCancel = () => {
    setEditingQuestionId(null);
    setEditingQuestionData(null);
  };

  const handleEditSave = async (questionBankItemId: string) => {
    if (!editingQuestionData || !editingQuestionId) return;

    if (editingQuestionData.type !== 'FindTheMistake' && !editingQuestionData.text.trim()) {
      toast.error('يرجى كتابة نص السؤال');
      return;
    }

    try {
      setSavingEdit(true);

      let finalAudioUrl = editingQuestionData.audioUrl;
      // 1. Upload audio if new audio file is selected
      if (editingQuestionData.audioFile) {
        toast.loading('جاري رفع الملف الصوتي للسؤال...', { id: 'audio-upload' });
        try {
          const uploadedUrl = await adminService.uploadQuestionAudio(questionBankItemId, editingQuestionData.audioFile);
          finalAudioUrl = uploadedUrl;
          toast.success('تم رفع الملف الصوتي بنجاح', { id: 'audio-upload' });
        } catch (uploadErr) {
          toast.error('فشل رفع الملف الصوتي، سيتم المتابعة بدون تحديث الصوت', { id: 'audio-upload' });
        }
      }

      // 2. Map payload
      const payload = {
        text: editingQuestionData.text,
        points: editingQuestionData.points,
        audioUrl: finalAudioUrl,
        writtenCorrection: editingQuestionData.writtenCorrection,
        hintText: editingQuestionData.hintText,
        baseText: editingQuestionData.baseText,
        mistakeStartIndex: editingQuestionData.mistakeStartIndex,
        mistakeEndIndex: editingQuestionData.mistakeEndIndex,
        options: editingQuestionData.options
      };

      await adminService.updateExamQuestion(id, editingQuestionId, payload);
      toast.success('تم حفظ تعديلات السؤال بنجاح');
      
      setEditingQuestionId(null);
      setEditingQuestionData(null);
      
      // Refresh dashboard
      const refreshed = await adminService.getExamDashboard(id);
      setData(refreshed || null);
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'أخفق حفظ تعديلات السؤال');
    } finally {
      setSavingEdit(false);
    }
  };

  const handleDeleteQuestion = async (examQuestionId: string) => {
    if (!window.confirm('هل أنت متأكد من رغبتك في حذف هذا السؤال نهائياً من الامتحان؟')) return;

    try {
      await adminService.deleteExamQuestion(id, examQuestionId);
      toast.success('تم حذف السؤال بنجاح');
      // Refresh dashboard
      const refreshed = await adminService.getExamDashboard(id);
      setData(refreshed || null);
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'أخفق حذف السؤال');
    }
  };

  const handleAddQuestionClick = () => {
    const nextOrder = (data?.questionCount || 0) + 1;
    setAddingQuestionData({
      text: '',
      type: 'MCQ',
      points: 1,
      order: nextOrder,
      options: [
        { text: '', isCorrect: true },
        { text: '', isCorrect: false },
        { text: '', isCorrect: false },
        { text: '', isCorrect: false },
      ],
    });
    setIsAddingQuestion(true);
  };

  const handleAddQuestionCancel = () => {
    setIsAddingQuestion(false);
    setAddingQuestionData(null);
  };

  const handleAddQuestionSave = async () => {
    if (!addingQuestionData) return;

    if (addingQuestionData.type !== 'FindTheMistake' && !addingQuestionData.text.trim()) {
      toast.error('يرجى كتابة نص السؤال');
      return;
    }

    try {
      setSavingAdd(true);
      
      let finalAudioUrl = addingQuestionData.audioUrl;
      
      // We will first add the question to get an ID if we wanted to upload audio, 
      // but since adding expects questions payload, we add questions first.
      // If we want audio, we upload it after or just use a standard flow.
      // For addQuestionsToExam, we can send the clean question first:
      const cleanQuestion = { ...addingQuestionData };
      delete cleanQuestion.audioFile;

      // Call addQuestionsToExam
      await adminService.addQuestionsToExam(id, { questions: [cleanQuestion] });
      toast.success('تمت إضافة السؤال بنجاح');
      
      setIsAddingQuestion(false);
      setAddingQuestionData(null);

      // Refresh dashboard to display the new question
      const refreshed = await adminService.getExamDashboard(id);
      setData(refreshed || null);
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'أخفق إضافة السؤال الجديد');
    } finally {
      setSavingAdd(false);
    }
  };

  if (loading) {
    return (
      <AdminShellChrome
        activePath="/admin/content"
        sectionLabel="إدارة المحتوى"
        pageTitle="بروفايل الامتحان التفصيلي"
        subtitle="جاري تحميل البيانات..."
      >
        <AdminPageSkeleton />
      </AdminShellChrome>
    );
  }

  if (!data) {
    return (
      <AdminShellChrome
        activePath="/admin/content"
        sectionLabel="إدارة المحتوى"
        pageTitle="الامتحان غير موجود"
        subtitle="أخفق العثور على الامتحان المطلوب"
        action={<AdminBackButton />}
      >
        <div className="rounded-3xl border border-red-200 bg-red-50 p-8 shadow-sm text-center">
          <AlertCircle className="mx-auto h-12 w-12 text-red-500 mb-4" />
          <p className="text-red-700 font-bold">لم يتم العثور على تفاصيل الامتحان أو ليس لديك الصلاحيات اللازمة للوصول إليه.</p>
        </div>
      </AdminShellChrome>
    );
  }

  return (
    <AdminShellChrome
      activePath="/admin/content"
      sectionLabel="إدارة المحتوى ▸ بروفايل الامتحان"
      pageTitle={data.title}
      subtitle={data.description || 'استعراض تحليلات الامتحان، إحصائيات الأسئلة، ومحاولات الطلاب بالكامل.'}
      action={<AdminBackButton />}
    >
      <div className="space-y-8">
        
        {/* Statistics Cards */}
        <section className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4">
          <AdminStatCard variant="light" icon={FileQuestion} label="عدد الأسئلة" value={data.questionCount} />
          <AdminStatCard variant="accent" icon={GraduationCap} label="الدرجة النهائية" value={data.totalScore} />
          <AdminStatCard variant="muted" icon={GraduationCap} label="درجة النجاح" value={data.passingScore} />
          <AdminStatCard variant="light" icon={Timer} label="زمن الامتحان" value={data.durationMinutes ? `${data.durationMinutes} د` : 'غير محدد'} />
          <AdminStatCard variant="accent" icon={BookCheck} label="إجمالي الطلاب" value={data.attempts?.length || 0} />
          <AdminStatCard variant="light" icon={BarChart3} label="متوسط الدرجات" value={averageScore} />
        </section>

        {/* Tab Switcher */}
        <div className="flex border-b border-[var(--admin-border)] gap-2">
          <button
            onClick={() => setActiveTab('questions')}
            className={`pb-3 px-4 font-bold text-sm transition-all border-b-2 flex items-center gap-2 ${
              activeTab === 'questions'
                ? 'border-[var(--admin-primary)] text-[var(--admin-primary)]'
                : 'border-transparent text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
            }`}
          >
            <LayoutList size={16} />
            الأسئلة والتحليلات ({data.questionCount})
          </button>
          <button
            onClick={() => setActiveTab('attempts')}
            className={`pb-3 px-4 font-bold text-sm transition-all border-b-2 flex items-center gap-2 ${
              activeTab === 'attempts'
                ? 'border-[var(--admin-primary)] text-[var(--admin-primary)]'
                : 'border-transparent text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
            }`}
          >
            <BookCheck size={16} />
            محاولات الطلاب ({data.attempts?.length || 0})
          </button>
        </div>

        {/* Questions Tab */}
        {activeTab === 'questions' && (
          <div className="space-y-6">
            <div className="flex justify-between items-center bg-[var(--admin-card)] p-4 rounded-3xl border border-[var(--admin-border)] shadow-sm">
              <h3 className="text-lg font-bold text-[var(--admin-text)]">
                الأسئلة المرفقة وإحصائيات الطلاب عليها
              </h3>
              {!isAddingQuestion && (
                <NeumorphButton
                  type="button"
                  onClick={handleAddQuestionClick}
                  intent="primary"
                  size="md"
                  pill
                >
                  <Plus className="w-4 h-4 ml-2" /> إضافة سؤال جديد
                </NeumorphButton>
              )}
            </div>

            {/* Inline Adding Question Form */}
            {isAddingQuestion && addingQuestionData && (
              <div className="rounded-2xl border border-[var(--admin-primary)] bg-[var(--admin-card)] shadow-md overflow-hidden animate-in slide-in-from-top-4 duration-300">
                <div className="bg-[var(--admin-primary)]/10 px-6 py-4 border-b border-[var(--admin-primary)]/20 flex justify-between items-center">
                  <span className="font-bold text-[var(--admin-primary)]">إضافة سؤال جديد للامتحان</span>
                  <button onClick={handleAddQuestionCancel} className="text-[var(--admin-muted)] hover:text-red-500">
                    <X size={20} />
                  </button>
                </div>
                <div className="p-6">
                  <QuestionEditor
                    question={addingQuestionData}
                    index={data.questionCount}
                    onChange={(_, q) => setAddingQuestionData(q)}
                    onRemove={handleAddQuestionCancel}
                  />
                  <div className="flex justify-end gap-3 mt-6">
                    <NeumorphButton
                      type="button"
                      onClick={handleAddQuestionCancel}
                      intent="ghost"
                      size="md"
                      pill
                    >
                      إلغاء
                    </NeumorphButton>
                    <NeumorphButton
                      type="button"
                      onClick={handleAddQuestionSave}
                      disabled={savingAdd}
                      loading={savingAdd}
                      intent="primary"
                      size="md"
                      pill
                    >
                      <Save className="w-4 h-4 ml-2" /> حفظ وإضافة السؤال
                    </NeumorphButton>
                  </div>
                </div>
              </div>
            )}

            {/* Questions List */}
            <div className="space-y-4">
              {data.questions && data.questions.length > 0 ? (
                data.questions.map((q, idx) => {
                  const isEditing = editingQuestionId === q.examQuestionId;

                  if (isEditing && editingQuestionData) {
                    return (
                      <div key={q.examQuestionId} className="rounded-2xl border border-amber-500 bg-[var(--admin-card)] shadow-md overflow-hidden animate-in zoom-in-95">
                        <div className="bg-amber-500/10 px-6 py-4 border-b border-amber-500/20 flex justify-between items-center">
                          <span className="font-bold text-amber-600 dark:text-amber-400">تعديل السؤال رقم {idx + 1}</span>
                          <button onClick={handleEditCancel} className="text-[var(--admin-muted)] hover:text-red-500">
                            <X size={20} />
                          </button>
                        </div>
                        <div className="p-6">
                          <QuestionEditor
                            question={editingQuestionData}
                            index={idx}
                            onChange={(_, updated) => setEditingQuestionData(updated)}
                            onRemove={handleEditCancel}
                          />
                          <div className="flex justify-end gap-3 mt-6">
                            <NeumorphButton
                              type="button"
                              onClick={handleEditCancel}
                              intent="ghost"
                              size="md"
                              pill
                            >
                              إلغاء
                            </NeumorphButton>
                            <NeumorphButton
                              type="button"
                              onClick={() => handleEditSave(q.questionBankItemId)}
                              disabled={savingEdit}
                              loading={savingEdit}
                              intent="primary"
                              size="md"
                              pill
                            >
                              <Save className="w-4 h-4 ml-2" /> حفظ التعديلات
                            </NeumorphButton>
                          </div>
                        </div>
                      </div>
                    );
                  }

                  return (
                    <div 
                      key={q.examQuestionId} 
                      className="group relative rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 transition-all hover:border-[var(--admin-primary)] hover:shadow-md"
                    >
                      <div className="flex flex-col xl:flex-row xl:items-start justify-between gap-6">
                        
                        {/* Question Details */}
                        <div className="flex gap-4 flex-1">
                          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-[var(--admin-card-strong)] text-sm font-bold text-[var(--admin-text)] shadow-sm">
                            {idx + 1}
                          </div>
                          <div className="flex-1 space-y-3">
                            <div className="text-[var(--admin-text)] font-semibold text-base leading-relaxed break-words" dangerouslySetInnerHTML={{ __html: q.text }} />
                            
                            {q.baseText && (
                              <p className="text-[var(--admin-muted)] mt-2 text-sm italic border-r-2 border-[var(--admin-border)] pr-3 bg-[var(--admin-background)] py-1 rounded">
                                {q.baseText}
                              </p>
                            )}

                            {/* Render Options for MCQ */}
                            {q.type === 'MCQ' && q.options && q.options.length > 0 && (
                              <div className="grid grid-cols-1 md:grid-cols-2 gap-2 mt-3 pl-4">
                                {q.options.map((opt: any) => (
                                  <div 
                                    key={opt.id} 
                                    className={`flex items-center gap-2 px-3 py-2 rounded-xl border text-xs font-semibold ${
                                      opt.isCorrect 
                                        ? 'bg-green-500/10 border-green-500/30 text-green-600 dark:text-green-400' 
                                        : 'bg-[var(--admin-background)] border-[var(--admin-border)] text-[var(--admin-muted)]'
                                    }`}
                                  >
                                    <span className={`w-2.5 h-2.5 rounded-full ${opt.isCorrect ? 'bg-green-500' : 'bg-zinc-300'}`} />
                                    <span>{opt.text}</span>
                                  </div>
                                ))}
                              </div>
                            )}

                            {/* Hint and written correction */}
                            {(q.hintText || q.writtenCorrection || q.audioUrl) && (
                              <div className="flex flex-wrap gap-4 text-xs mt-3 bg-[var(--admin-background)] p-3 rounded-xl border border-[var(--admin-border)]">
                                {q.hintText && (
                                  <div>
                                    <span className="font-bold text-[var(--admin-muted)]">التلميح: </span>
                                    <span className="text-[var(--admin-text)]">{q.hintText}</span>
                                  </div>
                                )}
                                {q.writtenCorrection && (
                                  <div>
                                    <span className="font-bold text-[var(--admin-muted)]">التصحيح المكتوب: </span>
                                    <span className="text-[var(--admin-text)]">{q.writtenCorrection}</span>
                                  </div>
                                )}
                                {q.audioUrl && (
                                  <div className="flex items-center gap-1">
                                    <span className="font-bold text-[var(--admin-muted)]">تفسير صوتي: </span>
                                    <audio src={q.audioUrl} className="h-6 w-32" controls />
                                  </div>
                                )}
                              </div>
                            )}

                            <div className="mt-4 flex flex-wrap gap-3">
                              <span className="inline-flex items-center gap-1.5 rounded-md bg-[var(--admin-card-strong)] px-2.5 py-1 text-xs font-semibold text-[var(--admin-muted)]">
                                {q.type === 'MCQ' ? 'اختيار من متعدد' : q.type === 'Essay' ? 'مقال' : q.type === 'FindTheMistake' ? 'اكتشف الغلطة' : q.type}
                              </span>
                              <span className="inline-flex items-center gap-1.5 rounded-md bg-[var(--admin-primary)]/10 px-2.5 py-1 text-xs font-bold text-[var(--admin-primary)] border border-[var(--admin-primary)]/20">
                                {q.points} {q.points === 1 ? 'درجة' : 'درجات'}
                              </span>
                            </div>
                          </div>
                        </div>

                        {/* Right side stats & controls */}
                        <div className="flex flex-col gap-4 xl:w-64 shrink-0">
                          {/* Statistics Container */}
                          <div className="rounded-xl bg-[var(--admin-card-soft)] border border-[var(--admin-border)] p-4">
                            <div className="flex items-center gap-1.5 mb-2">
                              <BarChart3 className="h-4 w-4 text-[var(--admin-primary)]" />
                              <span className="text-xs font-bold text-[var(--admin-text)]">تحليل إجابات الطلاب</span>
                            </div>
                            {q.totalAttempts && q.totalAttempts > 0 ? (
                              <div className="space-y-2">
                                <div className="flex items-center justify-between text-xs">
                                  <span className="text-[var(--admin-muted)]">نسبة الصحة:</span>
                                  <span className="font-bold text-green-600 dark:text-green-400">{q.correctPercentage}%</span>
                                </div>
                                <div className="w-full h-1.5 bg-[var(--admin-bg)] rounded-full overflow-hidden border border-[var(--admin-border)]">
                                  <div 
                                    className="h-full bg-green-500 rounded-full" 
                                    style={{ width: `${q.correctPercentage}%` }}
                                  />
                                </div>
                                <div className="grid grid-cols-3 gap-1 text-[10px] font-mono text-[var(--admin-muted)] text-center pt-1 border-t border-[var(--admin-border)]/50">
                                  <div>
                                    <div className="font-bold text-green-600 dark:text-green-400">{q.correctCount}</div>
                                    <div>صح</div>
                                  </div>
                                  <div>
                                    <div className="font-bold text-red-500">{q.wrongCount}</div>
                                    <div>خطأ</div>
                                  </div>
                                  <div>
                                    <div className="font-bold text-[var(--admin-text)]">{q.totalAttempts}</div>
                                    <div>إجمالي</div>
                                  </div>
                                </div>
                              </div>
                            ) : (
                              <div className="space-y-2">
                                <p className="text-xs font-bold text-[var(--admin-muted)]">لا توجد إحصائيات</p>
                                <p className="text-[10px] leading-relaxed text-[var(--admin-muted)] opacity-70">
                                  لم يقم أي طالب بإرسال إجابة على هذا السؤال بعد.
                                </p>
                              </div>
                            )}
                          </div>

                          {/* Action Buttons */}
                          <div className="flex justify-end gap-2 border-t border-[var(--admin-border)]/30 pt-3">
                            <button
                              onClick={() => handleEditClick(q, idx)}
                              className="inline-flex items-center gap-1 text-xs font-bold text-amber-600 dark:text-amber-400 hover:bg-amber-500/10 px-3 py-1.5 rounded-lg border border-amber-500/20"
                            >
                              <Edit size={14} />
                              تعديل
                            </button>
                            <button
                              onClick={() => handleDeleteQuestion(q.examQuestionId)}
                              className="inline-flex items-center gap-1 text-xs font-bold text-red-600 hover:bg-red-500/10 px-3 py-1.5 rounded-lg border border-red-500/20"
                            >
                              <Trash2 size={14} />
                              حذف
                            </button>
                          </div>
                        </div>
                      </div>
                    </div>
                  );
                })
              ) : (
                <div className="text-center py-16 bg-[var(--admin-card)] rounded-3xl border border-dashed border-[var(--admin-border)]">
                  <HelpCircle className="mx-auto h-12 w-12 text-[var(--admin-muted)] opacity-60 mb-3" />
                  <p className="text-[var(--admin-text)] font-bold mb-1">لا توجد أسئلة مضافة</p>
                  <p className="text-xs text-[var(--admin-muted)] mb-4">هذا الامتحان خالي من الأسئلة في الوقت الحالي.</p>
                  <NeumorphButton
                    type="button"
                    onClick={handleAddQuestionClick}
                    intent="primary"
                    size="sm"
                    pill
                  >
                    <Plus className="w-4 h-4 ml-1" /> إضافة أول سؤال
                  </NeumorphButton>
                </div>
              )}
            </div>
          </div>
        )}

        {/* Attempts Tab */}
        {activeTab === 'attempts' && (
          <div className="space-y-6">
            
            {/* Search Box */}
            <div className="flex items-center bg-[var(--admin-card)] rounded-2xl border border-[var(--admin-border)] px-4 py-2.5 w-full max-w-md shadow-sm">
              <UserIcon className="text-[var(--admin-muted)] w-5 h-5 ml-2.5" />
              <input
                type="text"
                placeholder="ابحث باسم الطالب أو رقم الهاتف..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="bg-transparent border-none outline-none text-sm text-[var(--admin-text)] placeholder:text-[var(--admin-muted)] w-full text-right"
                dir="rtl"
              />
            </div>

            {/* Attempts Table */}
            <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] overflow-hidden shadow-sm">
              {filteredAttempts.length > 0 ? (
                <div className="overflow-x-auto">
                  <table className="w-full text-right border-collapse" dir="rtl">
                    <thead>
                      <tr className="bg-[var(--admin-card-strong)] border-b border-[var(--admin-border)] text-xs font-bold text-[var(--admin-muted)]">
                        <th className="p-4">اسم الطالب</th>
                        <th className="p-4">رقم الهاتف</th>
                        <th className="p-4">الدرجة</th>
                        <th className="p-4 text-center">التقييم</th>
                        <th className="p-4 text-center">انتهى الوقت</th>
                        <th className="p-4">تاريخ المحاولة</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-[var(--admin-border)]">
                      {filteredAttempts.map((attempt, index) => (
                        <tr 
                          key={`${attempt.studentId}-${index}`}
                          className="text-sm text-[var(--admin-text)] hover:bg-[var(--admin-background)]/50 transition-colors"
                        >
                          <td className="p-4 font-bold flex items-center gap-2">
                            <span className="w-7 h-7 rounded-full bg-[var(--admin-primary)]/10 text-[var(--admin-primary)] flex items-center justify-center text-xs font-bold">{index + 1}</span>
                            {attempt.studentName}
                          </td>
                          <td className="p-4 font-mono text-xs text-[var(--admin-muted)]">{attempt.studentPhone}</td>
                          <td className="p-4 font-bold">
                            <span className={attempt.isPassed ? 'text-green-600 dark:text-green-400' : 'text-red-500'}>
                              {attempt.scoreAchieved} / {data.totalScore}
                            </span>
                          </td>
                          <td className="p-4 text-center">
                            <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-bold ${
                              attempt.isPassed
                                ? 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400'
                                : 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400'
                            }`}>
                              {attempt.evaluation || (attempt.isPassed ? 'ناجح' : 'راسب')}
                            </span>
                          </td>
                          <td className="p-4 text-center">
                            {attempt.isTimeExpired ? (
                              <span className="text-xs font-semibold text-red-500 bg-red-100 dark:bg-red-900/20 px-2 py-0.5 rounded-md">نعم</span>
                            ) : (
                              <span className="text-xs font-semibold text-[var(--admin-muted)]">لا</span>
                            )}
                          </td>
                          <td className="p-4 text-xs text-[var(--admin-muted)] font-medium">
                            {attempt.submittedAt ? new Date(attempt.submittedAt).toLocaleString('ar-EG') : '—'}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <div className="text-center py-12 text-[var(--admin-muted)]">
                  {searchQuery ? 'لا توجد نتائج تطابق بحثك.' : 'لا توجد محاولات مسجلة لهذا الامتحان حتى الآن.'}
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </AdminShellChrome>
  );
}
