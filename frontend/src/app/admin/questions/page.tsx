'use client';

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
import { formatCompactNumber } from '@/components/admin/admin-utils';
import { adminService, QuestionBankItemDto, QuestionOptionDto } from '@/services/admin-service';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';

export default function AdminQuestionsPage() {
  const [questions, setQuestions] = useState<QuestionBankItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [search, setSearch] = useState('');
  const [qText, setQText] = useState('');
  const [qPoints, setQPoints] = useState(1);
  const [qTags, setQTags] = useState('');
  const [saving, setSaving] = useState(false);
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
      const data = await adminService.listQuestions(1, 100, '');
      setQuestions(data.items);
    } catch (error) {
      console.error(error);
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
    if (options.filter((option) => option.text.trim()).length < 2) {
      toast.error('أدخل على الأقل اختيارين');
      return;
    }

    try {
      setSaving(true);
      await adminService.createQuestion({
        text: qText,
        defaultPoints: qPoints,
        tags: qTags,
        options: options.filter((option) => option.text.trim()),
      });
      setShowModal(false);
      setQText('');
      setQPoints(1);
      setQTags('');
      setOptions([
        { text: '', isCorrect: true },
        { text: '', isCorrect: false },
        { text: '', isCorrect: false },
        { text: '', isCorrect: false },
      ]);
      await loadQuestions();
    } catch (error) {
      console.error(error);
      toast.error('تعذر حفظ السؤال');
    } finally {
      setSaving(false);
    }
  }

  const filteredQuestions = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return questions;

    return questions.filter(
      (question) =>
        question.text.toLowerCase().includes(term) ||
        question.tags.toLowerCase().includes(term),
    );
  }, [questions, search]);

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

      <AdminSearchToolbar
        value={search}
        onChange={setSearch}
        placeholder="ابحث في نص السؤال أو التصنيف..."
      />

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
          <textarea
            rows={3}
            value={qText}
            onChange={(event) => setQText(event.target.value)}
            placeholder="نص السؤال"
            className="admin-input"
            required
          />
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
          <div className="flex justify-end gap-3 pt-4 border-t border-[var(--admin-border)] mt-4">
            <NeumorphButton type="button" onClick={() => setShowModal(false)} intent="ghost" size="md">إلغاء</NeumorphButton>
            <NeumorphButton type="submit" disabled={saving} loading={saving} intent="primary" size="md" pill>
              حفظ
            </NeumorphButton>
          </div>
        </form>
      </AdminModal>
    </AdminShellChrome>
  );
}
