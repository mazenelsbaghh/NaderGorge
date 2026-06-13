'use client';

import type { CSSProperties } from 'react';
import { Trash2, Plus, Check } from 'lucide-react';
import { NumberField } from '@/components/ui/number-field';
import { Dropdown } from '@/components/ui/dropdown';
import dynamic from 'next/dynamic';
import { FindTheMistakeBuilder } from './FindTheMistakeBuilder';
import 'react-quill-new/dist/quill.snow.css';

const ReactQuill = dynamic(() => import('react-quill-new'), { 
  ssr: false,
  loading: () => <div className="h-32 w-full animate-pulse bg-[var(--admin-border)] rounded-xl" />
});

export interface InlineExamOptionDto {
  text: string;
  isCorrect: boolean;
}

export interface InlineExamQuestionDto {
  text: string;
  type: 'MCQ' | 'Essay' | 'FindTheMistake';
  points: number;
  order: number;
  options: InlineExamOptionDto[];
  audioUrl?: string;
  audioFile?: File | null;
  writtenCorrection?: string;
  hintText?: string;
  baseText?: string;
  mistakeStartIndex?: number | null;
  mistakeEndIndex?: number | null;
}

interface QuestionEditorProps {
  question: InlineExamQuestionDto;
  index: number;
  onChange: (index: number, updated: InlineExamQuestionDto) => void;
  onRemove: (index: number) => void;
}

const modules = {
  toolbar: [
    [{ 'header': [1, 2, 3, false] }],
    ['bold', 'italic', 'underline', 'strike'],
    [{ 'color': [] }, { 'background': [] }],
    [{ 'list': 'ordered'}, { 'list': 'bullet' }],
    ['clean']
  ],
};

const formats = [
  'header',
  'bold', 'italic', 'underline', 'strike',
  'color', 'background',
  'list'
];

export function QuestionEditor({ question, index, onChange, onRemove }: QuestionEditorProps) {
  const handlePropChange = (
    field: keyof InlineExamQuestionDto,
    value: InlineExamQuestionDto[keyof InlineExamQuestionDto]
  ) => {
    onChange(index, { ...question, [field]: value });
  };

  const handleAddOption = () => {
    const newOptions = [...question.options, { text: '', isCorrect: false }];
    onChange(index, { ...question, options: newOptions });
  };

  const handleUpdateOption = (optIndex: number, text: string) => {
    const newOptions = question.options.map((opt, i) => 
      i === optIndex ? { ...opt, text } : opt
    );
    onChange(index, { ...question, options: newOptions });
  };

  const handleToggleCorrectOption = (optIndex: number) => {
    const newOptions = question.options.map((opt, i) => ({
      ...opt,
      isCorrect: i === optIndex ? !opt.isCorrect : opt.isCorrect, // Keep it multi-select friendly or just single. Let's do multi-select friendly. Wait, usually MCQ has 1 correct. If we want single, we map and set false to others. We'll leave it multi as backend supports it.
    }));
    onChange(index, { ...question, options: newOptions });
  };

  const handleRemoveOption = (optIndex: number) => {
    const newOptions = question.options.filter((_, i) => i !== optIndex);
    onChange(index, { ...question, options: newOptions });
  };

  return (
    <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 shadow-sm transition-all relative">
      <div className="absolute top-4 left-4">
        <button
          type="button"
          onClick={() => onRemove(index)}
          className="text-red-500 hover:bg-red-500/10 w-11 h-11 flex items-center justify-center rounded-xl transition-colors"
          title="حذف السؤال"
        >
          <Trash2 className="w-5 h-5" />
        </button>
      </div>

      <div className="flex flex-col gap-6 w-full pl-0 pt-10 md:pt-0 md:pl-12">
        {/* Controls Row */}
        <div className="flex flex-wrap items-end justify-between gap-4 border-b border-[var(--admin-border)] pb-4">
          <div className="flex items-center gap-4 text-xs font-bold text-[var(--admin-muted)]">
            <span className="flex items-center justify-center w-8 h-8 rounded-lg bg-[var(--admin-primary)]/10 text-[var(--admin-primary)]">
              {index + 1}
            </span>
            تعديل السؤال
          </div>
          
          <div className="flex flex-wrap gap-4">
            <div className="flex-1 min-w-[120px] space-y-2">
              <Dropdown
                label="نوع السؤال"
                value={question.type}
                onChange={(v) => handlePropChange('type', v as string)}
                size="sm"
                options={[
                  { value: 'MCQ', label: 'اختيارات (MCQ)' },
                  { value: 'Essay', label: 'مقال (Essay)' },
                  { value: 'FindTheMistake', label: 'اكتشف الغلطة' },
                ]}
              />
            </div>

            <div className="flex-1 min-w-[120px] space-y-2">
              <NumberField
                minValue={1}
                value={question.points}
                onChange={(val) => handlePropChange('points', val)}
              >
                <NumberField.Label className="text-[10px] uppercase tracking-wider font-bold text-[var(--admin-muted)] block mb-2">عدد الدرجات</NumberField.Label>
                <NumberField.Group className="h-[46px] bg-[var(--admin-background)] border-[var(--admin-border)]">
                  <NumberField.DecrementButton className="shrink-0" />
                  <NumberField.Input className="px-1 text-sm font-bold text-[var(--admin-text)] bg-transparent" />
                  <NumberField.IncrementButton className="shrink-0" />
                </NumberField.Group>
              </NumberField>
            </div>
          </div>
        </div>

        {question.type === 'FindTheMistake' ? (
          <FindTheMistakeBuilder
            baseText={question.baseText || ''}
            startIndex={question.mistakeStartIndex ?? null}
            endIndex={question.mistakeEndIndex ?? null}
            onChange={(bt, st, ed) => {
              onChange(index, {
                ...question,
                baseText: bt,
                mistakeStartIndex: st,
                mistakeEndIndex: ed,
                text: 'اكتشف الغلطة: ' + bt,
                options: st !== null && ed !== null
                  ? [
                      { text: bt.substring(st, ed), isCorrect: true },
                      { text: '---', isCorrect: false },
                    ]
                  : question.options,
              });
            }}
          />
        ) : (
          <div className="w-full flex-col flex space-y-2 relative">
            <div
              className="rounded-xl overflow-hidden border border-[var(--admin-border)] focus-within:border-[var(--admin-primary)] focus-within:ring-1 focus-within:ring-[var(--admin-primary)] transition-all bg-[var(--admin-card)] text-[var(--admin-text)]"
              style={{
                '--ql-toolbar-bg': 'var(--admin-background)',
                '--ql-border': 'var(--admin-border)',
                '--ql-text': 'var(--admin-text)',
              } as CSSProperties}
            >
              <style>{`
                .ql-toolbar.ql-snow { border: none !important; border-bottom: 1px solid var(--ql-border) !important; background: var(--ql-toolbar-bg) !important; }
                .ql-container.ql-snow { border: none !important; }
                .ql-editor { min-height: 200px; font-size: 15px; color: var(--ql-text); }
                .ql-snow .ql-stroke { stroke: var(--ql-text); }
                .ql-snow .ql-fill, .ql-snow .ql-stroke.ql-fill { fill: var(--ql-text); }
                .ql-snow .ql-picker { color: var(--ql-text); }
                .ql-snow .ql-picker-options { background-color: var(--ql-toolbar-bg); border-color: var(--ql-border); }
              `}</style>
              <ReactQuill
                theme="snow"
                value={question.text}
                onChange={(content) => handlePropChange('text', content)}
                modules={modules}
                formats={formats}
                className="w-full"
                placeholder="اكتب نص السؤال هنا..."
              />
            </div>
          </div>
        )}

        <div className="grid grid-cols-1 gap-3 md:grid-cols-2 border-t border-[var(--admin-border)] pt-4">
          <textarea
            rows={2}
            value={question.hintText || ''}
            onChange={(e) => handlePropChange('hintText', e.target.value)}
            placeholder="تلميح للمساعدة (يظهر للطالب بدون خصم درجات)"
            className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-background)] px-4 py-2.5 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)] transition-all resize-none"
          />
          <textarea
            rows={2}
            value={question.writtenCorrection || ''}
            onChange={(e) => handlePropChange('writtenCorrection', e.target.value)}
            placeholder="تصحيح نصي (يظهر بعد الإجابة)"
            className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-background)] px-4 py-2.5 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)] transition-all resize-none"
          />
        </div>

        <div className="flex flex-col gap-2 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 relative overflow-hidden">
          <span className="text-[10px] uppercase tracking-wider font-bold text-[var(--admin-muted)] block">شرح صوتي (اختياري)</span>
          <input
            type="file"
            accept="audio/*"
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) handlePropChange('audioFile', file);
            }}
            className="text-sm font-bold text-[var(--admin-muted)] file:mr-4 file:py-2 file:px-4 file:rounded-xl file:border file:border-[var(--admin-primary)] file:text-sm file:font-semibold file:bg-[var(--admin-primary)]/10 file:text-[var(--admin-primary)] hover:file:bg-[var(--admin-primary)] hover:file:text-white transition-all cursor-pointer"
          />
        </div>

        {question.type === 'MCQ' && (
          <div className="mt-4 border-t border-[var(--admin-border)] pt-4">
            <p className="text-xs font-bold text-[var(--admin-muted)] mb-3">الخيارات (حدد الإجابة الصحيحة)</p>
            <div className="flex flex-col gap-2">
              {question.options.map((opt, optIndex) => (
                <div key={optIndex} className="flex items-center gap-3">
                  <button
                    type="button"
                    onClick={() => handleToggleCorrectOption(optIndex)}
                    className={`w-11 h-11 shrink-0 rounded-xl flex items-center justify-center transition-colors border ${
                      opt.isCorrect
                        ? 'bg-green-500/20 text-green-500 border-green-500/50'
                        : 'bg-[var(--admin-background)] text-[var(--admin-muted)] border-[var(--admin-border)] hover:bg-[var(--admin-border)]'
                    }`}
                    title={opt.isCorrect ? "إجابة صحيحة" : "إجابة خاطئة"}
                  >
                    <Check className={`w-5 h-5 ${opt.isCorrect ? 'opacity-100' : 'opacity-0'}`} />
                  </button>
                  <input
                    type="text"
                    value={opt.text}
                    onChange={(e) => handleUpdateOption(optIndex, e.target.value)}
                    placeholder={`الخيار ${optIndex + 1}`}
                    className={`flex-1 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-background)] px-4 py-2.5 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] transition-all ${
                        opt.isCorrect ? 'border-green-500/50' : ''
                    }`}
                    required
                  />
                  <button
                    type="button"
                    onClick={() => handleRemoveOption(optIndex)}
                    className="text-red-500 hover:bg-red-500/10 w-11 h-11 flex items-center justify-center rounded-xl transition-colors shrink-0"
                  >
                    <Trash2 className="w-5 h-5" />
                  </button>
                </div>
              ))}

              <button
                type="button"
                onClick={handleAddOption}
                className="self-start mt-2 flex items-center gap-2 text-xs font-bold text-[var(--admin-primary)] hover:text-white hover:bg-[var(--admin-primary)] px-4 py-2 rounded-lg transition-all border border-[var(--admin-primary)]"
              >
                <Plus className="w-4 h-4" />
                إضافة خيار
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
