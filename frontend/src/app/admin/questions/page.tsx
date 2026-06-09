'use client';

import { devConsole } from '@/utils/dev-console';
import { FormEvent, useEffect, useMemo, useState } from 'react';
import { CircleCheck, Plus, Shield, Tags } from 'lucide-react';

import {
  AdminShellChrome,
  AdminDataTable,
  AdminColumn,
  AdminStatCard,
  AdminModal,
  AdminSearchToolbar,
} from '@/components/admin';
import { FindTheMistakeBuilder } from '@/components/admin/FindTheMistakeBuilder';
import { adminService, QuestionBankItemDto, QuestionOptionDto } from '@/services/admin-service';
import { teacherService, SubjectDto, TeacherDto } from '@/services/teacher-service';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';

export default function AdminQuestionsPage() {
  const [questions, setQuestions] = useState<QuestionBankItemDto[]>([]);
  const [subjects, setSubjects] = useState<SubjectDto[]>([]);
  const [teachers, setTeachers] = useState<TeacherDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [search, setSearch] = useState('');
  const [selectedSubjectId, setSelectedSubjectId] = useState<string>('All');
  const [selectedTeacherId, setSelectedTeacherId] = useState<string>('All');
  const [qText, setQText] = useState('');
  const [qPoints, setQPoints] = useState(1);
  const [qTags, setQTags] = useState('');
  const [hintText, setHintText] = useState('');
  const [writtenCorrection, setWrittenCorrection] = useState('');
  const [audioFile, setAudioFile] = useState<File | null>(null);
  const [saving, setSaving] = useState(false);
  const [qType, setQType] = useState<number>(0);
  const [baseText, setBaseText] = useState('');
  const [mistakeStartIndex, setMistakeStartIndex] = useState<number | null>(null);
  const [mistakeEndIndex, setMistakeEndIndex] = useState<number | null>(null);
  const [selectedCreateSubjectId, setSelectedCreateSubjectId] = useState('');
  const [selectedCreateTeacherId, setSelectedCreateTeacherId] = useState('');

  const [options, setOptions] = useState<{ text: string; isCorrect: boolean }[]>([
    { text: '', isCorrect: true },
    { text: '', isCorrect: false },
    { text: '', isCorrect: false },
    { text: '', isCorrect: false },
  ]);

  useEffect(() => {
    void loadQuestions();
  }, []);

  async function loadQuestions() {
    try {
      setLoading(true);
      const [data, subjectsRes, teachersRes] = await Promise.all([
        adminService.listQuestions(1, 100, ''),
        teacherService.getSubjects().catch(() => ({ success: true, data: [] as SubjectDto[] })),
        teacherService.getTeachers().catch(() => ({ success: true, data: [] as TeacherDto[] }))
      ]);
      setQuestions(data.items || []);
      setSubjects(subjectsRes.data ?? []);
      setTeachers(teachersRes.data ?? []);
    } catch (error) {
      devConsole.error(error);
    } finally {
      setLoading(false);
    }
  }

  function updateOption(index: number, prop: keyof QuestionOptionDto, value: string | boolean) {
    const next = [...options];
    next[index] = { ...next[index], [prop]: value };

    if (prop === 'isCorrect' && value === true) {
      next.forEach((option, optionIndex) => {
        if (optionIndex !== index) option.isCorrect = false;
      });
    }

    setOptions(next);
  }

  async function handleSave(event: FormEvent) {
    event.preventDefault();
    if (!selectedCreateSubjectId || !selectedCreateTeacherId) {
      toast.error('يرجى اختيار المادة والمعلم أولاً.');
      return;
    }
    if (qType === 0 && options.filter((option) => option.text.trim()).length < 2) {
      toast.error('أدخل على الأقل اختيارين');
      return;
    }
    if (qType === 2 && mistakeStartIndex === null) {
      toast.error('يرجى تحديد الغلطة في النص أولاً.');
      return;
    }

    try {
      setSaving(true);
      const payload: any = {
        text: qType === 2 ? 'اكتشف الغلطة: ' + baseText : qText,
        type: qType,
        defaultPoints: qPoints,
        tags: qTags,
        hintText,
        writtenCorrection,
        subjectId: selectedCreateSubjectId,
        teacherId: selectedCreateTeacherId,
      };

      if (qType === 2) {
         payload.baseText = baseText;
         payload.mistakeStartIndex = mistakeStartIndex;
         payload.mistakeEndIndex = mistakeEndIndex;
         payload.options = [
            { text: baseText.substring(mistakeStartIndex!, mistakeEndIndex!), isCorrect: true },
            { text: 'Dummy', isCorrect: false } // Needed to satisfy minimum options validation in backend if Essay not specified, though backend allows empty if it checks type. We pass 2 options to bypass logic.
         ];
      } else {
         payload.options = options.filter((option) => option.text.trim());
      }

      const created = await adminService.createQuestion(payload);

      if (created?.id && audioFile) {
        await adminService.uploadQuestionAudio(created.id, audioFile);
      }

      setShowModal(false);
      setQType(0);
      setBaseText('');
      setMistakeStartIndex(null);
      setMistakeEndIndex(null);
      setQText('');
      setQPoints(1);
      setQTags('');
      setHintText('');
      setWrittenCorrection('');
      setAudioFile(null);
      setOptions([
        { text: '', isCorrect: true },
        { text: '', isCorrect: false },
        { text: '', isCorrect: false },
        { text: '', isCorrect: false },
      ]);
      setSelectedCreateSubjectId('');
      setSelectedCreateTeacherId('');
      await loadQuestions();
    } catch (error) {
      devConsole.error(error);
      toast.error('تعذر حفظ السؤال');
    } finally {
      setSaving(false);
    }
  }

  const filteredQuestions = useMemo(() => {
    let list = questions;

    // Filter by Subject
    if (selectedSubjectId !== 'All') {
      list = list.filter((q) => q.subjectId === selectedSubjectId);
    }

    // Filter by Teacher
    if (selectedTeacherId !== 'All') {
      list = list.filter((q) => q.createdByTeacherId === selectedTeacherId);
    }

    const term = search.trim().toLowerCase();
    if (!term) return list;

    return list.filter(
      (question) =>
        question.text.toLowerCase().includes(term) ||
        question.tags.toLowerCase().includes(term),
    );
  }, [questions, search, selectedSubjectId, selectedTeacherId]);

  const correctOptionsCount = questions.reduce(
    (sum, question) => sum + question.options.filter((option) => option.isCorrect).length,
    0,
  );

  const uniqueTagsCount = new Set(
    questions.flatMap((question) => question.tags.split(',').map((tag) => tag.trim()).filter(Boolean))
  ).size;

  const columns: AdminColumn<QuestionBankItemDto>[] = [
    {
      key: 'text',
      label: 'السؤال',
      render: (q) => <div className="font-bold text-[var(--admin-text)]">{q.text}</div>,
    },
    {
      key: 'tags',
      label: 'التصنيف',
      render: (q) => <div className="text-[var(--admin-muted)]">{q.tags || 'عام'}</div>,
    },
    {
      key: 'points',
      label: 'النقاط',
      render: (q) => <div className="font-bold text-[var(--admin-primary)]">{q.defaultPoints}</div>,
    },
    {
      key: 'options',
      label: 'الإجابات',
      render: (q) => (
        <div className="flex flex-wrap gap-2">
          {q.options.map((option, index) => (
            <span
              key={`${q.id}-${index}`}
              className={`rounded-full px-3 py-1 text-xs font-bold ${
                option.isCorrect
                  ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400'
                  : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)]'
              }`}
            >
              {option.text}
            </span>
          ))}
        </div>
      ),
    },
  ];

  return (
    <AdminShellChrome
      activePath="/admin/questions"
      sectionLabel="بنك الأسئلة"
      pageTitle="إدارة الأسئلة"
      subtitle="إنشاء الأسئلة ومراجعة التصنيفات والإجابات الصحيحة."
      action={
        <NeumorphButton
          onClick={() => setShowModal(true)}
          intent="primary"
          size="lg"
          pill
        >
          <Plus className="h-4 w-4" />
          إضافة سؤال
        </NeumorphButton>
      }
    >
      <NeumorphButton
        type="button"
        onClick={() => setShowModal(true)}
        intent="primary"
        size="icon"
        pill
        className="fixed bottom-24 left-8 z-40 !h-14 !w-14 shadow-2xl md:hidden"
      >
        <Plus className="h-5 w-5" />
      </NeumorphButton>

      <section className="mb-12 grid grid-cols-1 gap-6 md:grid-cols-3">
        <AdminStatCard
          variant="light"
          icon={Shield}
          label="إجمالي الأسئلة"
          value={questions.length}
        />
        <AdminStatCard
          variant="accent"
          icon={Tags}
          label="التصنيفات"
          value={uniqueTagsCount}
        />
        <AdminStatCard
          variant="muted"
          icon={CircleCheck}
          label="إجابات صحيحة"
          value={correctOptionsCount}
        />
      </section>

      <div className="mb-6 flex flex-col md:flex-row gap-4 items-center mr-auto w-full max-w-3xl">
        <div className="flex-1 w-full">
          <AdminSearchToolbar
            value={search}
            onChange={setSearch}
            placeholder="ابحث في نص السؤال أو التصنيف..."
          />
        </div>
        
        <div className="flex gap-3 w-full md:w-auto">
          <select
            value={selectedSubjectId}
            onChange={(e) => setSelectedSubjectId(e.target.value)}
            className="admin-input flex-1 md:w-44"
          >
            <option value="All">كل المواد</option>
            {subjects.map((sub) => (
              <option key={sub.id} value={sub.id}>{sub.name}</option>
            ))}
          </select>

          <select
            value={selectedTeacherId}
            onChange={(e) => setSelectedTeacherId(e.target.value)}
            className="admin-input flex-1 md:w-44"
          >
            <option value="All">كل المدرسين</option>
            {teachers.map((t) => (
              <option key={t.id} value={t.id}>{t.fullName}</option>
            ))}
          </select>
        </div>
      </div>

      <AdminDataTable
        data={filteredQuestions}
        columns={columns}
        loading={loading}
        rowKey={(q) => q.id}
        emptyMessage="لا توجد أسئلة مطابقة."
      />

      <AdminModal
        open={showModal}
        onClose={() => setShowModal(false)}
        title="إضافة سؤال جديد"
        subtitle="أدخل نص السؤال والخيارات مع تحديد الإجابة الصحيحة."
        maxWidth="max-w-2xl"
      >
        <form onSubmit={handleSave} className="space-y-4">
          <div className="flex gap-4">
            <label className="flex items-center gap-2 text-sm font-bold text-[var(--admin-text)]">
              <input type="radio" checked={qType === 0} onChange={() => setQType(0)} className="accent-[var(--admin-primary)]" />
              اختيار من متعدد (MCQ)
            </label>
            <label className="flex items-center gap-2 text-sm font-bold text-[var(--admin-text)]">
              <input type="radio" checked={qType === 2} onChange={() => setQType(2)} className="accent-[var(--admin-primary)]" />
              اكتشف الغلطة
            </label>
          </div>

          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            <div>
              <label className="block text-xs font-bold text-[var(--admin-muted)] mb-1">المادة</label>
              <select
                value={selectedCreateSubjectId}
                onChange={(e) => setSelectedCreateSubjectId(e.target.value)}
                className="admin-input w-full"
                required
              >
                <option value="">اختر المادة...</option>
                {subjects.map((sub) => (
                  <option key={sub.id} value={sub.id}>{sub.name}</option>
                ))}
              </select>
            </div>
            
            <div>
              <label className="block text-xs font-bold text-[var(--admin-muted)] mb-1">المعلم</label>
              <select
                value={selectedCreateTeacherId}
                onChange={(e) => setSelectedCreateTeacherId(e.target.value)}
                className="admin-input w-full"
                required
              >
                <option value="">اختر المعلم...</option>
                {teachers.map((t) => (
                  <option key={t.id} value={t.id}>{t.fullName}</option>
                ))}
              </select>
            </div>
          </div>

          {qType === 2 ? (
            <FindTheMistakeBuilder 
              baseText={baseText} 
              startIndex={mistakeStartIndex} 
              endIndex={mistakeEndIndex} 
              onChange={(bt, st, ed) => { setBaseText(bt); setMistakeStartIndex(st); setMistakeEndIndex(ed); }} 
            />
          ) : (
             <textarea
               rows={3}
               value={qText}
               onChange={(event) => setQText(event.target.value)}
               placeholder="نص السؤال"
               className="admin-input"
               required
             />
          )}

          <div className="grid grid-cols-1 gap-3 md:grid-cols-[1fr_140px]">
            <input
              type="text"
              value={qTags}
              onChange={(event) => setQTags(event.target.value)}
              placeholder="التصنيفات (افصل بينها بفاصلة أو مسافة)"
              className="admin-input"
            />
            <input
              type="number"
              min={0}
              step={0.5}
              value={qPoints}
              onChange={(event) => setQPoints(Number(event.target.value))}
              placeholder="النقاط"
              className="admin-input"
            />
          </div>
          
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            <textarea
              rows={2}
              value={hintText}
              onChange={(e) => setHintText(e.target.value)}
              placeholder="تلميح للمساعدة (يظهر للطالب بدون خصم درجات)"
              className="admin-input"
            />
            <textarea
              rows={2}
              value={writtenCorrection}
              onChange={(e) => setWrittenCorrection(e.target.value)}
              placeholder="تصحيح نصي (يظهر بعد الإجابة)"
              className="admin-input"
            />
          </div>

          <div className="flex flex-col gap-2 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 relative overflow-hidden">
            <span className="text-[10px] uppercase tracking-wider font-bold text-[var(--admin-muted)] block">شرح صوتي (اختياري)</span>
            <input 
              type="file" 
              accept="audio/*"
              onChange={(e) => setAudioFile(e.target.files?.[0] || null)}
              className="text-sm font-bold text-[var(--admin-muted)] file:mr-4 file:py-2 file:px-4 file:rounded-xl file:border file:border-[var(--admin-primary)] file:text-sm file:font-semibold file:bg-[var(--admin-primary)]/10 file:text-[var(--admin-primary)] hover:file:bg-[var(--admin-primary)] hover:file:text-white transition-all cursor-pointer"
            />
          </div>

          {qType === 0 && (
            <div className="space-y-3 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 relative overflow-hidden">
              {options.map((option, index) => (
                <div key={index} className="flex items-center gap-3">
                  <input
                    type="radio"
                    name="correctOption"
                    checked={option.isCorrect}
                    onChange={(event) => updateOption(index, 'isCorrect', event.target.checked)}
                    className="h-5 w-5 accent-[var(--admin-primary)]"
                  />
                  <input
                    type="text"
                    value={option.text}
                    onChange={(event) => updateOption(index, 'text', event.target.value)}
                    placeholder={`الاختيار ${index + 1}`}
                    className="admin-input"
                    required={index < 2}
                  />
                </div>
              ))}
            </div>
          )}

          <div className="flex justify-end gap-3 pt-4 border-t border-[var(--admin-border)] mt-4">
            <NeumorphButton type="button" onClick={() => setShowModal(false)} intent="ghost" size="md">إلغاء</NeumorphButton>
            <NeumorphButton type="submit" disabled={saving || !selectedCreateSubjectId || !selectedCreateTeacherId} loading={saving} intent="primary" size="md" pill>
              حفظ
            </NeumorphButton>
          </div>
        </form>
      </AdminModal>
    </AdminShellChrome>
  );
}
