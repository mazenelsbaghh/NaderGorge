'use client';

import { useState } from 'react';
import { Layers, FileText, Calendar, PlayCircle, BookOpen, Award, Wallet, Info } from 'lucide-react';
import type { CodeType } from '@/services/code-service';

// ── Types ────────────────────────────────────────────────────────────────────
export interface CodeTypeSelection {
  codeType: CodeType;
  packageId?: string;
  termId?: string;
  contentSectionId?: string;
  lessonId?: string;
  examId?: string;
  videoTargetIds?: string[];
  balanceAmount?: number;
  discountPercentage?: number;
  expiresAt?: string;
}

interface CodeTypeSelectorProps {
  value: CodeTypeSelection;
  onChange: (value: CodeTypeSelection) => void;
  errors?: Record<string, string>;
}

// ── Code Types Metadata ──────────────────────────────────────────────────────
const CODE_TYPES: { type: CodeType; label: string; icon: any; description: string }[] = [
  { type: 'Package', label: 'كورس / باكدج', icon: Layers, description: 'كود يفتح الكورس كامل (السنة كاملة)' },
  { type: 'Term', label: 'ترم', icon: Calendar, description: 'كود يفتح ترم كامل بجميع شهوره' },
  { type: 'Month', label: 'شهر / قسم', icon: BookOpen, description: 'كود يفتح شهر دراسي كامل' },
  { type: 'Lesson', label: 'حصة', icon: FileText, description: 'كود لفتح حصة محددة' },
  { type: 'Video', label: 'مجموعة فيديوهات', icon: PlayCircle, description: 'يفتح فيديو أو أكثر بحرية' },
  { type: 'Exam', label: 'امتحان', icon: Award, description: 'كود لفتح امتحان محدد' },
  { type: 'Balance', label: 'شحن رصيد', icon: Wallet, description: 'يشحن محفظة الطالب بمبلغ نقدي' },
];

export function CodeTypeSelector({ value, onChange, errors = {} }: CodeTypeSelectorProps) {
  // Handlers
  const setType = (codeType: CodeType) => {
    onChange({ codeType, discountPercentage: value.discountPercentage, expiresAt: value.expiresAt });
  };

  const handleField = (field: keyof CodeTypeSelection, val: any) => {
    onChange({ ...value, [field]: val });
  };

  return (
    <div className="space-y-6">
      {/* ── Type Grid ── */}
      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3">
        {CODE_TYPES.map((ct) => {
          const isSelected = value.codeType === ct.type;
          return (
            <button
              key={ct.type}
              type="button"
              onClick={() => setType(ct.type)}
              className={`flex flex-col items-center gap-2 p-4 rounded-xl border-2 transition-all ${
                isSelected
                  ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)]/10 text-[var(--admin-primary)]'
                  : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)] hover:border-[var(--admin-primary)]/50'
              }`}
            >
              <ct.icon size={28} />
              <span className="font-bold text-sm">{ct.label}</span>
              <span className="text-xs opacity-70 text-center">{ct.description}</span>
            </button>
          );
        })}
      </div>
      {errors.codeType && <p className="text-sm text-red-500 mt-1">{errors.codeType}</p>}

      {/* ── Dynamic Target Inputs ── */}
      <div className="p-5 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)]">
        <div className="flex items-center gap-2 mb-4 text-[var(--admin-text)]">
          <Info size={16} className="opacity-50" />
          <h3 className="font-bold text-sm">حدد هدف الكود</h3>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">

          {/* Package */}
          {value.codeType === 'Package' && (
            <div className="col-span-1 md:col-span-2">
              <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 block">رقم (ID) الباكدج</label>
              <input
                type="text"
                placeholder="أدخل UUID الباكدج"
                className="w-full bg-[var(--admin-card)] border border-[var(--admin-border)] rounded-lg px-3 py-2 text-sm text-[var(--admin-text)]"
                value={value.packageId || ''}
                onChange={(e) => handleField('packageId', e.target.value)}
                dir="ltr"
              />
              {errors.packageId && <p className="text-xs text-red-500 mt-1">{errors.packageId}</p>}
            </div>
          )}

          {/* Term */}
          {value.codeType === 'Term' && (
            <div className="col-span-1 md:col-span-2">
              <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 block">رقم (ID) الترم</label>
              <input
                type="text"
                placeholder="أدخل UUID الترم"
                className="w-full bg-[var(--admin-card)] border border-[var(--admin-border)] rounded-lg px-3 py-2 text-sm text-[var(--admin-text)]"
                value={value.termId || ''}
                onChange={(e) => handleField('termId', e.target.value)}
                dir="ltr"
              />
              {errors.termId && <p className="text-xs text-red-500 mt-1">{errors.termId}</p>}
            </div>
          )}

          {/* Month */}
          {value.codeType === 'Month' && (
            <div className="col-span-1 md:col-span-2">
              <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 block">رقم (ID) الشهر / القسم</label>
              <input
                type="text"
                placeholder="أدخل UUID الشهر"
                className="w-full bg-[var(--admin-card)] border border-[var(--admin-border)] rounded-lg px-3 py-2 text-sm text-[var(--admin-text)]"
                value={value.contentSectionId || ''}
                onChange={(e) => handleField('contentSectionId', e.target.value)}
                dir="ltr"
              />
              {errors.contentSectionId && <p className="text-xs text-red-500 mt-1">{errors.contentSectionId}</p>}
            </div>
          )}

          {/* Lesson */}
          {value.codeType === 'Lesson' && (
            <div className="col-span-1 md:col-span-2">
              <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 block">رقم (ID) الحصة</label>
              <input
                type="text"
                placeholder="أدخل UUID الحصة"
                className="w-full bg-[var(--admin-card)] border border-[var(--admin-border)] rounded-lg px-3 py-2 text-sm text-[var(--admin-text)]"
                value={value.lessonId || ''}
                onChange={(e) => handleField('lessonId', e.target.value)}
                dir="ltr"
              />
              {errors.lessonId && <p className="text-xs text-red-500 mt-1">{errors.lessonId}</p>}
            </div>
          )}

          {/* Exam */}
          {value.codeType === 'Exam' && (
            <div className="col-span-1 md:col-span-2">
              <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 block">رقم (ID) الامتحان</label>
              <input
                type="text"
                placeholder="أدخل UUID الامتحان"
                className="w-full bg-[var(--admin-card)] border border-[var(--admin-border)] rounded-lg px-3 py-2 text-sm text-[var(--admin-text)]"
                value={value.examId || ''}
                onChange={(e) => handleField('examId', e.target.value)}
                dir="ltr"
              />
              {errors.examId && <p className="text-xs text-red-500 mt-1">{errors.examId}</p>}
            </div>
          )}

          {/* Video */}
          {value.codeType === 'Video' && (
            <div className="col-span-1 md:col-span-2">
              <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 block">معرفات (UUIDs) الفيديوهات <span className="text-xs opacity-50">(افصل بينهم بفاصلة)</span></label>
              <input
                type="text"
                placeholder="UUID1, UUID2..."
                className="w-full bg-[var(--admin-card)] border border-[var(--admin-border)] rounded-lg px-3 py-2 text-sm text-[var(--admin-text)]"
                value={value.videoTargetIds?.join(', ') || ''}
                onChange={(e) => {
                  const ids = e.target.value.split(',').map(s => s.trim()).filter(Boolean);
                  handleField('videoTargetIds', ids);
                }}
                dir="ltr"
              />
              {errors.videoTargetIds && <p className="text-xs text-red-500 mt-1">{errors.videoTargetIds}</p>}
            </div>
          )}

          {/* Balance */}
          {value.codeType === 'Balance' && (
            <div className="col-span-1 md:col-span-2 lg:col-span-1">
              <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 block">قيمة الشحن (جنيه)</label>
              <input
                type="number"
                min="1"
                placeholder="50"
                className="w-full bg-[var(--admin-card)] border border-[var(--admin-border)] rounded-lg px-3 py-2 text-sm text-[var(--admin-text)]"
                value={value.balanceAmount || ''}
                onChange={(e) => handleField('balanceAmount', e.target.value ? Number(e.target.value) : undefined)}
                dir="ltr"
              />
              {errors.balanceAmount && <p className="text-xs text-red-500 mt-1">{errors.balanceAmount}</p>}
            </div>
          )}

        </div>

        {/* ── Optional fields (Discount + Expiration) ── */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4 pt-4 border-t border-[var(--admin-border)]/50">
          <div>
            <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 flex justify-between">
              <span>نسبة الخصم % </span>
              <span className="opacity-50 font-normal">اختياري</span>
            </label>
            <input
              type="number"
              min="1"
              max="100"
              placeholder="مثلا: 20"
              className="w-full bg-[var(--admin-card)] border border-[var(--admin-border)] rounded-lg px-3 py-2 text-sm text-[var(--admin-text)]"
              value={value.discountPercentage || ''}
              onChange={(e) => handleField('discountPercentage', e.target.value ? Number(e.target.value) : undefined)}
              dir="ltr"
            />
          </div>

          <div>
            <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 flex justify-between">
              <span>تاريخ الانتهاء</span>
              <span className="opacity-50 font-normal">اختياري</span>
            </label>
            <input
              type="datetime-local"
              className="w-full bg-[var(--admin-card)] border border-[var(--admin-border)] rounded-lg px-3 py-2 text-sm text-[var(--admin-text)] [color-scheme:dark]"
              value={value.expiresAt ? new Date(value.expiresAt).toISOString().slice(0, 16) : ''}
              onChange={(e) => handleField('expiresAt', e.target.value ? new Date(e.target.value).toISOString() : undefined)}
              dir="ltr"
            />
          </div>
        </div>
      </div>
    </div>
  );
}
