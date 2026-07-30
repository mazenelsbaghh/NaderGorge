import React, { useMemo, useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { MousePointerClick } from 'lucide-react';

interface FindTheMistakeInteractProps {
  baseText: string;
  selectedText: string | null;
  onSelect: (selectedText: string) => void;
  disabled?: boolean;
}

export function FindTheMistakeInteract({
  baseText,
  selectedText,
  onSelect,
  disabled = false,
}: FindTheMistakeInteractProps) {
  const [localStart, setLocalStart] = useState<number | null>(null);
  const [localEnd, setLocalEnd] = useState<number | null>(null);

  // Sync external selectedText → local indices on first load or external clear
  useEffect(() => {
    if (!selectedText) {
      setLocalStart(null);
      setLocalEnd(null);
    } else {
      const currentLocalStr =
        localStart !== null && localEnd !== null
          ? baseText.substring(localStart, localEnd)
          : null;
      if (currentLocalStr !== selectedText) {
        const idx = baseText.indexOf(selectedText);
        if (idx !== -1) {
          setLocalStart(idx);
          setLocalEnd(idx + selectedText.length);
        }
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedText, baseText]);

  // Tokenize into words
  const tokens = useMemo(() => {
    const arr: { text: string; start: number; end: number; isSpace: boolean }[] = [];
    let currentWord = '';
    let startIdx = 0;

    for (let i = 0; i <= baseText.length; i++) {
      const char = baseText[i];
      if (char === ' ' || char === '\n' || i === baseText.length) {
        if (currentWord.length > 0) {
          arr.push({ text: currentWord, start: startIdx, end: i, isSpace: false });
          currentWord = '';
        }
        if (char === ' ' || char === '\n') {
          arr.push({ text: char, start: i, end: i + 1, isSpace: true });
        }
        startIdx = i + 1;
      } else {
        currentWord += char;
      }
    }
    return arr;
  }, [baseText]);

  const handleTokenClick = (tokenStart: number, tokenEnd: number) => {
    if (disabled) return;

    let newStart = localStart;
    let newEnd = localEnd;

    if (localStart === null || localEnd === null) {
      newStart = tokenStart;
      newEnd = tokenEnd;
    } else if (localStart === tokenStart && localEnd === tokenEnd) {
      // exact match → deselect
      newStart = null;
      newEnd = null;
    } else if (tokenStart >= localEnd) {
      // expand right (adjacent spaces allowed)
      const gap = baseText.substring(localEnd, tokenStart);
      newEnd = gap.trim() === '' ? tokenEnd : tokenEnd;
      newStart = gap.trim() === '' ? localStart : tokenStart;
    } else if (tokenEnd <= localStart) {
      // expand left
      const gap = baseText.substring(tokenEnd, localStart);
      newStart = gap.trim() === '' ? tokenStart : tokenStart;
      newEnd = gap.trim() === '' ? localEnd : tokenEnd;
    } else if (tokenStart >= localStart && tokenEnd <= localEnd) {
      // click inside → shrink
      if (tokenStart === localStart) {
        let next = tokenEnd;
        while (next < localEnd && baseText[next] === ' ') next++;
        newStart = next < localEnd ? next : null;
        if (newStart === null) newEnd = null;
      } else if (tokenEnd === localEnd) {
        let prev = tokenStart;
        while (prev > localStart && baseText[prev - 1] === ' ') prev--;
        newEnd = prev > localStart ? prev : null;
        if (newEnd === null) newStart = null;
      } else {
        newStart = tokenStart;
        newEnd = tokenEnd;
      }
    } else {
      newStart = tokenStart;
      newEnd = tokenEnd;
    }

    setLocalStart(newStart);
    setLocalEnd(newEnd);
    onSelect(newStart !== null && newEnd !== null ? baseText.substring(newStart, newEnd) : '');
  };

  const hasSelection = localStart !== null && localEnd !== null;

  return (
    <div className="space-y-4" dir="rtl">
      <div className="rounded-2xl border border-[rgba(100,116,139,0.22)] bg-[#f8fafc]/80 p-5 sm:p-6">
        <div className="mb-4 flex items-center gap-2 text-sm font-bold text-[#475569]">
          <MousePointerClick className="h-4 w-4 shrink-0" />
          <span>انقر على الكلمة (أو الكلمات) الخطأ في الجملة التالية:</span>
        </div>

        <div className="flex flex-wrap gap-y-2 text-xl leading-loose sm:text-2xl">
          {tokens.map((token, idx) => {
            if (token.isSpace) {
              return token.text === '\n' ? <br key={idx} /> : <span key={idx}> </span>;
            }

            const isSelected =
              hasSelection &&
              token.start >= localStart! &&
              token.end <= localEnd!;

            return (
              <motion.button
                key={idx}
                type="button"
                disabled={disabled}
                onClick={() => handleTokenClick(token.start, token.end)}
                whileHover={disabled ? {} : { scale: 1.06 }}
                whileTap={disabled ? {} : { scale: 0.95 }}
                className={`mx-1 rounded-xl px-3 py-1.5 font-bold transition-colors ${
                  isSelected
                    ? 'bg-red-500 font-black text-white shadow-md'
                    : 'border border-[rgba(100,116,139,0.22)] bg-white/70 text-[#0f172a] hover:border-[#475569]/40 hover:bg-[#475569]/5'
                } ${disabled && !isSelected ? 'cursor-not-allowed opacity-50' : ''}`}
              >
                {token.text}
              </motion.button>
            );
          })}
        </div>
      </div>

      {/* Selection indicator */}
      {hasSelection && (
        <motion.div
          initial={{ opacity: 0, y: 4 }}
          animate={{ opacity: 1, y: 0 }}
          className="flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm font-bold text-red-700"
        >
          <span className="rounded-full bg-red-100 px-2 py-0.5 text-xs font-black text-red-600">
            تحديدك
          </span>
          «{baseText.substring(localStart!, localEnd!)}»
          <button
            type="button"
            disabled={disabled}
            onClick={() => {
              setLocalStart(null);
              setLocalEnd(null);
              onSelect('');
            }}
            className="mr-auto text-xs font-black text-red-400 hover:text-red-700"
          >
            إلغاء التحديد
          </button>
        </motion.div>
      )}
    </div>
  );
}
