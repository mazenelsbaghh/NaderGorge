'use client';

import React, { useState } from 'react';
import { MousePointerClick, RefreshCw } from 'lucide-react';
import { motion } from 'framer-motion';

interface FindTheMistakeBuilderProps {
  baseText: string;
  startIndex: number | null;
  endIndex: number | null;
  onChange: (baseText: string, startIndex: number | null, endIndex: number | null) => void;
}

export function FindTheMistakeBuilder({ baseText, startIndex, endIndex, onChange }: FindTheMistakeBuilderProps) {
  const [textMode, setTextMode] = useState(!baseText);
  const [localText, setLocalText] = useState(baseText);

  // Parse text into individual words while remembering whitespace and indices
  const getRenderableTokens = () => {
    const tokens = [];
    let currentWord = '';
    let startIdx = 0;

    for (let i = 0; i <= localText.length; i++) {
      const char = localText[i];
      if (char === ' ' || char === '\n' || i === localText.length) {
        if (currentWord.length > 0) {
          tokens.push({ text: currentWord, start: startIdx, end: i, isSpace: false });
          currentWord = '';
        }
        if (char === ' ' || char === '\n') {
          tokens.push({ text: char, start: i, end: i + 1, isSpace: true });
        }
        startIdx = i + 1;
      } else {
        currentWord += char;
      }
    }
    return tokens;
  };

  const tokens = getRenderableTokens();

  const handleTokenClick = (start: number, end: number) => {
    let newStart = startIndex;
    let newEnd = endIndex;

    if (startIndex === null || endIndex === null) {
        newStart = start;
        newEnd = end;
    } else if (startIndex === start && endIndex === end) {
        // exactly the same token requested to toggle -> deselect
        newStart = null;
        newEnd = null;
    } else {
        // check adjacency for expanding right
        if (start >= endIndex) {
            const gap = localText.substring(endIndex, start);
            if (gap.trim() === '') {
                newEnd = end;
            } else {
                newStart = start;
                newEnd = end;
            }
        // check adjacency for expanding left
        } else if (end <= startIndex) {
            const gap = localText.substring(end, startIndex);
            if (gap.trim() === '') {
                newStart = start;
            } else {
                newStart = start;
                newEnd = end;
            }
        // check if clicking inside the selection (to shrink)
        } else if (start >= startIndex && end <= endIndex) {
            if (start === startIndex) {
                // shrink from left
                let nextStart = end;
                while (nextStart < endIndex && localText[nextStart] === ' ') nextStart++;
                if (nextStart < endIndex) {
                    newStart = nextStart;
                } else {
                    newStart = null; newEnd = null;
                }
            } else if (end === endIndex) {
                // shrink from right
                let prevEnd = start;
                while (prevEnd > startIndex && localText[prevEnd - 1] === ' ') prevEnd--;
                if (prevEnd > startIndex) {
                    newEnd = prevEnd;
                } else {
                    newStart = null; newEnd = null;
                }
            } else {
                // clicked in the middle, reset to just this token
                newStart = start;
                newEnd = end;
            }
        } else {
            // disjoint
            newStart = start;
            newEnd = end;
        }
    }

    onChange(localText, newStart, newEnd);
  };

  const commitText = () => {
    if (localText.trim()) {
      onChange(localText, null, null);
      setTextMode(false);
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <label className="text-sm font-bold text-[var(--admin-text)]">إعداد سؤال &quot;اكتشف الغلطة&quot;</label>
        {!textMode && (
          <button 
            type="button" 
            onClick={() => setTextMode(true)}
            className="text-xs text-[var(--admin-main)] flex items-center gap-1 hover:underline"
          >
            <RefreshCw className="w-3 h-3" /> تعديل النص
          </button>
        )}
      </div>

      {textMode ? (
        <div className="space-y-2">
          <textarea
            className="admin-input w-full min-h-[100px]"
            placeholder="اكتب الجملة هنا..."
            value={localText}
            onChange={e => setLocalText(e.target.value)}
          />
          <button 
            type="button" 
            onClick={commitText}
            className="admin-btn admin-btn-primary w-full"
            disabled={!localText.trim()}
          >
            متابعة لتحديد الغلطة
          </button>
        </div>
      ) : (
        <div className="space-y-4">
          <div className="bg-indigo-500/5 border border-indigo-500/20 rounded-xl p-4">
            <div className="flex items-center gap-2 mb-4 text-indigo-500 text-sm font-bold">
              <MousePointerClick className="w-4 h-4" />
              <span>انقر على الكلمة الخطأ في الجملة التالـــية (يمكنك تحديد عدة كلمات):</span>
            </div>
            
            <div className="flex flex-wrap text-lg leading-loose direction-rtl">
              {tokens.map((token, idx) => {
                if (token.isSpace) {
                  return token.text === '\n' ? <br key={idx} /> : <span key={idx}> </span>;
                }
                
                const isSelected = startIndex !== null && endIndex !== null && token.start >= startIndex && token.end <= endIndex;
                
                return (
                  <motion.button
                    key={idx}
                    type="button"
                    onClick={() => handleTokenClick(token.start, token.end)}
                    whileHover={{ scale: 1.05 }}
                    whileTap={{ scale: 0.95 }}
                    className={`px-2 py-1 mx-1 rounded-md transition-colors ${
                      isSelected 
                        ? 'bg-red-500 text-white font-black shadow-md border border-red-600' 
                        : 'bg-[var(--admin-card)] hover:bg-slate-200 text-[var(--admin-text)] border border-[var(--admin-border)]'
                    }`}
                  >
                    {token.text}
                  </motion.button>
                );
              })}
            </div>
          </div>
          
          {startIndex !== null && (
             <div className="bg-green-500/10 text-green-700 text-sm p-3 rounded-xl border border-green-500/20">
                ✅ تم تحديد الخطأ بنجاح: &quot;{localText.substring(startIndex, endIndex!)}&quot;
             </div>
          )}
        </div>
      )}
    </div>
  );
}
