'use client';

import { useRef, useState } from 'react';
import { FileImage, Loader2, ScanSearch, UploadCloud } from 'lucide-react';
import toast from 'react-hot-toast';
import { adminService, type AssessmentOcrQuestionDto } from '@/services/admin-service';
import { QuestionEditor, type InlineExamQuestionDto } from './QuestionEditor';
import NeumorphButton from '@/components/ui/neumorph-button';

type OcrDraft = InlineExamQuestionDto & { confidence: number };

interface OcrQuestionImportProps {
  nextOrder: number;
  onImport: (questions: InlineExamQuestionDto[]) => Promise<void> | void;
}

function toDraft(question: AssessmentOcrQuestionDto, order: number): OcrDraft {
  return {
    text: question.text,
    type: question.type,
    points: question.points || 1,
    order,
    options: question.options ?? [],
    confidence: question.confidence,
  };
}

function canImport(drafts: OcrDraft[]) {
  return drafts.length > 0
    && drafts.every((question) => question.text.replace(/<[^>]+>/g, '').trim().length > 0
      && (question.type !== 'MCQ'
        || (question.options.length >= 2 && question.options.some((option) => option.isCorrect))));
}

function getValidOcrFiles(files?: FileList | null): File[] | null {
  const selectedFiles = files ? Array.from(files) : [];
  if (selectedFiles.length === 0) return null;
  if (selectedFiles.length > 20) {
    toast.error('يمكن اختيار 20 صورة بحد أقصى.');
    return null;
  }
  if (selectedFiles.some((file) => !file.type.startsWith('image/') && file.type !== 'application/pdf')) {
    toast.error('اختار صور JPG أو PNG أو WEBP أو ملفات PDF فقط.');
    return null;
  }
  if (selectedFiles.some((file) => file.size > 8 * 1024 * 1024) || selectedFiles.reduce((sum, file) => sum + file.size, 0) > 32 * 1024 * 1024) {
    toast.error('كل صورة حتى 8 ميجابايت والإجمالي حتى 32 ميجابايت.');
    return null;
  }
  return selectedFiles;
}

function toInlineQuestion(draft: OcrDraft): InlineExamQuestionDto {
  return {
    text: draft.text,
    type: draft.type,
    points: draft.points,
    order: draft.order,
    options: draft.options,
    audioUrl: draft.audioUrl,
    imageUrl: draft.imageUrl,
    writtenCorrection: draft.writtenCorrection,
    hintText: draft.hintText,
    baseText: draft.baseText,
    mistakeStartIndex: draft.mistakeStartIndex,
    mistakeEndIndex: draft.mistakeEndIndex,
  };
}

export function OcrQuestionImport({ nextOrder, onImport }: OcrQuestionImportProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [drafts, setDrafts] = useState<OcrDraft[]>([]);
  const [scanning, setScanning] = useState(false);
  const [importing, setImporting] = useState(false);

  async function handleFiles(files?: FileList | null) {
    const selectedFiles = getValidOcrFiles(files);
    if (!selectedFiles) return;

    try {
      setScanning(true);
      const extracted = await adminService.extractAssessmentQuestionsFromImages(selectedFiles);
      if (extracted.length === 0) {
        toast.error('لم يتم العثور على أسئلة واضحة في الصورة.');
        return;
      }
      setDrafts(extracted.map((question, index) => toDraft(question, nextOrder + index)));
      toast.success(`تم استخراج ${extracted.length} سؤال للمراجعة.`);
    } catch (error: any) {
      toast.error(error?.response?.data?.message || 'تعذر قراءة الصورة. تأكد من إعداد Cloud Vision.');
    } finally {
      setScanning(false);
      if (inputRef.current) inputRef.current.value = '';
    }
  }

  async function acceptDrafts() {
    if (!canImport(drafts)) {
      toast.error('راجع نص الأسئلة، وأضف إجابة صحيحة لكل سؤال اختيارات قبل الإضافة.');
      return;
    }

    try {
      setImporting(true);
      await onImport(drafts.map(toInlineQuestion));
      setDrafts([]);
    } finally {
      setImporting(false);
    }
  }

  return (
    <section className="rounded-2xl border border-dashed border-[var(--admin-primary)]/50 bg-[var(--admin-primary)]/5 p-4">
      <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div>
          <h3 className="flex items-center gap-2 font-black text-[var(--admin-text)]">
            <ScanSearch className="h-5 w-5 text-[var(--admin-primary)]" />
            إضافة أسئلة بالصورة
          </h3>
          <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">
            ارفع صور صفحات الورقة أو ملف PDF، راجع الأسئلة هنا، ثم أضفها لنفس المحتوى.
          </p>
        </div>
        <label className="inline-flex min-h-11 cursor-pointer items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-black text-[var(--admin-primary-contrast)] hover:brightness-110">
          {scanning ? <Loader2 className="h-4 w-4 animate-spin" /> : <UploadCloud className="h-4 w-4" />}
          {scanning ? 'جارٍ استخراج الأسئلة...' : 'اختيار الصور أو PDF'}
          <input
            ref={inputRef}
            type="file"
            accept="image/jpeg,image/png,image/webp,application/pdf"
            multiple
            className="sr-only"
            disabled={scanning || importing}
            onChange={(event) => void handleFiles(event.target.files)}
          />
        </label>
      </div>

      {drafts.length > 0 && (
        <div className="mt-5 space-y-4 border-t border-[var(--admin-primary)]/20 pt-4">
          <div className="flex items-center justify-between gap-3">
            <p className="flex items-center gap-2 text-sm font-black text-[var(--admin-text)]">
              <FileImage className="h-4 w-4 text-[var(--admin-primary)]" />
              أسئلة مستخرجة تحتاج مراجعة ({drafts.length})
            </p>
            <NeumorphButton type="button" size="sm" intent="primary" loading={importing} onClick={() => void acceptDrafts()}>
              إضافة الأسئلة للمحتوى
            </NeumorphButton>
          </div>
          {drafts.map((question, index) => (
            <div key={`${question.order}-${index}`} className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-2">
              <QuestionEditor
                question={question}
                index={index}
                onChange={(_, updated) => setDrafts((current) => current.map((item, itemIndex) => itemIndex === index ? { ...updated, confidence: question.confidence } : item))}
                onRemove={() => setDrafts((current) => current.filter((_, itemIndex) => itemIndex !== index).map((item, itemIndex) => ({ ...item, order: nextOrder + itemIndex })))}
              />
              <p className="px-2 pb-2 text-xs font-bold text-[var(--admin-muted)]">
                دقة القراءة التقريبية: {Math.round(question.confidence * 100)}% — راجع الإجابة الصحيحة يدويًا.
              </p>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
