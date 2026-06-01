'use client';

import { useState } from 'react';
import toast from 'react-hot-toast';
import { BarChart3, Plus, X } from 'lucide-react';

import { communityService, type CreateCommunityPostResponse } from '@/services/community-service';

type CommunityPostComposerProps = {
  onCreated: (post: { id: string; body: string; status: string; createdAt: string; isPoll: boolean }) => void;
};

export function CommunityPostComposer({ onCreated }: CommunityPostComposerProps) {
  const [body, setBody] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [isPollEnabled, setIsPollEnabled] = useState(false);
  const [pollOptions, setPollOptions] = useState<string[]>(['', '']);

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    const trimmed = body.trim();
    if (!trimmed) {
      toast.error('اكتب بوست صالحًا قبل الإرسال.');
      return;
    }

    const validOptions = pollOptions.map((o) => o.trim()).filter((o) => o.length > 0);
    if (isPollEnabled) {
      if (validOptions.length < 2) {
        toast.error('يجب أن يحتوي الاستطلاع على خيارين على الأقل.');
        return;
      }
      if (validOptions.length > 10) {
        toast.error('لا يمكن أن يحتوي الاستطلاع على أكثر من ١٠ خيارات.');
        return;
      }
    }

    setSubmitting(true);
    try {
      const response = await communityService.createCommunityPost(trimmed, isPollEnabled ? validOptions : undefined);
      const created = response.data?.data as CreateCommunityPostResponse | undefined;

      if (created) {
        onCreated({
          id: created.id,
          body: trimmed,
          status: created.status,
          createdAt: created.createdAt,
          isPoll: isPollEnabled
        });
      }

      setBody('');
      setIsPollEnabled(false);
      setPollOptions(['', '']);
      toast.success(created?.message || 'تم إرسال البوست بنجاح.');
    } catch {
      // axios interceptor handles toast
    } finally {
      setSubmitting(false);
    }
  };

  const updatePollOption = (index: number, value: string) => {
    const newOptions = [...pollOptions];
    newOptions[index] = value;
    setPollOptions(newOptions);
  };

  const removePollOption = (index: number) => {
    if (pollOptions.length <= 2) return;
    const newOptions = [...pollOptions];
    newOptions.splice(index, 1);
    setPollOptions(newOptions);
  };

  return (
    <form 
      onSubmit={handleSubmit} 
      className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-sm dark:shadow-none"
    >
      <div className="flex gap-3 px-4 pt-4 pb-2">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-[var(--admin-primary-10)] text-sm font-bold text-[var(--admin-primary)]">
          أ
        </div>
        <div className="flex-1">
          <textarea
            className="w-full min-h-[60px] resize-none border-none bg-transparent pt-2 text-[18px] focus:ring-0 text-gray-900 dark:text-[var(--admin-text)] placeholder-gray-500 dark:placeholder-gray-400 outline-none"
            placeholder={isPollEnabled ? "توضيح الاستطلاع..." : "بم تفكر؟"}
            value={body}
            onChange={(e) => {
              setBody(e.target.value);
              e.target.style.height = 'auto';
              e.target.style.height = e.target.scrollHeight + 'px';
            }}
            maxLength={4000}
          />

          {isPollEnabled && (
            <div className="mt-3 space-y-3 rounded-xl border border-[var(--admin-border)] p-4 bg-[var(--admin-card-soft)] bg-opacity-50">
              <h4 className="text-[15px] font-bold text-[var(--admin-text)]">خيارات الاستطلاع</h4>
              <div className="space-y-2">
                {pollOptions.map((option, idx) => (
                    <div key={idx} className="flex items-center gap-2">
                      <input
                        type="text"
                        placeholder={`الخيار ${idx + 1}`}
                        value={option}
                        onChange={(e) => updatePollOption(idx, e.target.value)}
                        className="flex-1 rounded-lg border border-[var(--admin-border)] bg-transparent px-3 py-2 text-[14px] text-gray-900 focus:border-[#0866ff] focus:outline-none focus:ring-1 focus:ring-[#0866ff] dark:text-gray-100"
                        maxLength={200}
                      />
                      {pollOptions.length > 2 && (
                        <button
                          type="button"
                          onClick={() => removePollOption(idx)}
                          className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-red-500 hover:bg-red-50 dark:hover:bg-red-500/10"
                        >
                          <X className="h-5 w-5" />
                        </button>
                      )}
                    </div>
                  ))}
              </div>
              {pollOptions.length < 10 && (
                <button
                  type="button"
                  onClick={() => setPollOptions([...pollOptions, ''])}
                  className="flex items-center gap-2 rounded-lg py-2 pl-3 text-[14px] font-bold text-[var(--admin-primary)] transition-colors hover:bg-[var(--admin-primary-10)]"
                >
                  <Plus className="h-4 w-4" />
                  <span>إضافة خيار</span>
                </button>
              )}
            </div>
          )}

        </div>
      </div>

      <div className="px-4 pb-3">
        <div className="flex items-center justify-between border-t border-gray-200 dark:border-[var(--admin-border)] pt-3 mt-1">
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => setIsPollEnabled(!isPollEnabled)}
              className={`flex items-center justify-center rounded-full p-2 transition-colors ${
                isPollEnabled ? 'bg-[var(--admin-primary-10)] text-[var(--admin-primary)]' : 'text-gray-500 hover:bg-[var(--admin-hover)] dark:text-gray-400'
              }`}
              title="إضافة استطلاع رأي"
            >
              <BarChart3 className="h-5 w-5" />
            </button>
          </div>
          <button
            type="submit"
            disabled={submitting || !body.trim()}
            className="rounded-md bg-[#0866ff] px-6 py-1.5 text-[14px] font-bold text-white transition-opacity hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {submitting ? 'جارٍ النشر...' : 'نشر'}
          </button>
        </div>
      </div>
    </form>
  );
}
