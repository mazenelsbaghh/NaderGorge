'use client';

import { useId } from 'react';
import {
  AI_OUTPUT_LANGUAGE_OPTIONS,
  type AiOutputLanguage,
} from '@/lib/ai-output-language';

interface AiOutputLanguageFieldProps {
  language: AiOutputLanguage;
  onLanguageChange: (language: AiOutputLanguage) => void;
  disabled?: boolean;
}

export function AiOutputLanguageField({
  language,
  onLanguageChange,
  disabled = false,
}: AiOutputLanguageFieldProps) {
  const id = useId();
  const descriptionId = `${id}-description`;

  return (
    <fieldset
      className="space-y-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-4"
      aria-describedby={descriptionId}
      disabled={disabled}
      dir="rtl"
    >
      <legend className="text-sm font-bold text-[var(--admin-text)]">
        لغة ملخصات وصور AI
      </legend>
      <p id={descriptionId} className="text-xs leading-5 text-[var(--admin-muted)]">
        يطبق الاختيار على الملخصات والصور الجديدة؛ المحتوى الذي تم توليده بالفعل لا يتغير تلقائيًا.
      </p>

      <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
        {AI_OUTPUT_LANGUAGE_OPTIONS.map((option) => {
          const optionId = `${id}-${option.value.toLowerCase()}`;
          const selected = language === option.value;

          return (
            <label
              key={option.value}
              htmlFor={optionId}
              className={`flex min-h-20 cursor-pointer flex-col justify-center rounded-xl border px-3 py-2.5 text-start transition-[color,background-color,border-color,box-shadow] focus-within:ring-2 focus-within:ring-[var(--admin-primary)] focus-within:ring-offset-2 focus-within:ring-offset-[var(--admin-bg)] ${
                selected
                  ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)] text-[var(--admin-primary)]'
                  : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-text)] hover:border-[var(--admin-primary)]/45'
              } ${disabled ? 'cursor-not-allowed opacity-60' : ''}`}
            >
              <input
                id={optionId}
                type="radio"
                name={`${id}-ai-output-language`}
                value={option.value}
                checked={selected}
                onChange={() => onLanguageChange(option.value)}
                className="sr-only"
              />
              <span className="flex items-center justify-between gap-3">
                <span className="text-sm font-black" dir={option.value === 'English' ? 'ltr' : undefined}>
                  {option.label}
                </span>
                <span
                  aria-hidden="true"
                  className={`flex size-5 shrink-0 items-center justify-center rounded-full border-2 ${
                    selected
                      ? 'border-[var(--admin-primary)]'
                      : 'border-[var(--admin-muted)]'
                  }`}
                >
                  {selected && <span className="size-2.5 rounded-full bg-[var(--admin-primary)]" />}
                </span>
              </span>
              <span className={`mt-1 text-xs leading-5 ${selected ? 'text-[var(--admin-primary)]' : 'text-[var(--admin-muted)]'}`}>
                {option.description}
              </span>
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}
