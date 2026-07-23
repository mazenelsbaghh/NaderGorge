'use client';

import { BookOpenCheck, Brain, Lightbulb, ThumbsUp, Trophy } from 'lucide-react';
import { useRef, useState } from 'react';
import toast from 'react-hot-toast';

import { communityService } from '@/services/community-service';

type CommunityPostLikeButtonProps = {
  postId: string;
  initialLiked: boolean;
  initialCount: number;
};

const reactionOptions = [
  {
    id: 'useful',
    label: 'شرح مفيد',
    shortLabel: 'مفيد',
    Icon: BookOpenCheck,
    colorClass: 'text-[var(--admin-primary)]',
    activeClass: 'bg-[var(--admin-primary-10)] text-[var(--admin-primary)]',
  },
  {
    id: 'clear',
    label: 'فكرة وصلت',
    shortLabel: 'فهمت',
    Icon: Lightbulb,
    colorClass: 'text-[var(--admin-warning)]',
    activeClass: 'bg-[var(--admin-warning-10)] text-[var(--admin-warning)]',
  },
  {
    id: 'smart',
    label: 'تفكير ممتاز',
    shortLabel: 'ممتاز',
    Icon: Brain,
    colorClass: 'text-[var(--admin-primary-strong)]',
    activeClass: 'bg-[var(--admin-primary-10)] text-[var(--admin-primary-strong)]',
  },
  {
    id: 'achievement',
    label: 'حفزني',
    shortLabel: 'حفزني',
    Icon: Trophy,
    colorClass: 'text-[var(--admin-warning)]',
    activeClass: 'bg-[var(--admin-warning-10)] text-[var(--admin-warning)]',
  },
] as const;

type ReactionId = (typeof reactionOptions)[number]['id'];

export function CommunityPostLikeButton({
  postId,
  initialLiked,
  initialCount,
}: CommunityPostLikeButtonProps) {
  const [liked, setLiked] = useState(initialLiked);
  const [count, setCount] = useState(initialCount);
  const [loading, setLoading] = useState(false);
  const [isPickerOpen, setIsPickerOpen] = useState(false);
  const [selectedReaction, setSelectedReaction] = useState<ReactionId>('useful');
  const closeTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const selectedReactionOption = reactionOptions.find((reaction) => reaction.id === selectedReaction) ?? reactionOptions[0];
  const SelectedIcon = selectedReactionOption.Icon;

  const openPicker = () => {
    if (closeTimer.current) {
      clearTimeout(closeTimer.current);
      closeTimer.current = null;
    }
    setIsPickerOpen(true);
  };

  const closePickerSoon = () => {
    closeTimer.current = setTimeout(() => setIsPickerOpen(false), 140);
  };

  const handleToggle = async (reactionId: ReactionId = selectedReaction) => {
    if (loading) return;
    const previousLiked = liked;
    const previousCount = count;
    const nextLiked = !previousLiked;

    setSelectedReaction(reactionId);
    setLiked(nextLiked);
    setCount(Math.max(0, previousCount + (nextLiked ? 1 : -1)));
    setLoading(true);
    try {
      const response = await communityService.toggleCommunityPostLike(postId);
      const data = response.data?.data;
      if (data) {
        setLiked(data.isLikedByCurrentUser);
        setCount(data.likeCount);
      } else if (response.data?.success === false) {
        setLiked(previousLiked);
        setCount(previousCount);
        toast.error(response.data.message || 'تعذر تحديث التفاعل');
      }
    } catch (error: any) {
      setLiked(previousLiked);
      setCount(previousCount);
      toast.error(error?.response?.data?.message || 'تعذر تحديث التفاعل');
    } finally {
      setLoading(false);
    }
  };

  const handleReactionSelect = async (reactionId: ReactionId) => {
    setSelectedReaction(reactionId);
    setIsPickerOpen(false);
    if (!liked) {
      await handleToggle(reactionId);
    }
  };

  return (
    <div
      className="relative flex flex-1"
      onMouseEnter={openPicker}
      onMouseLeave={closePickerSoon}
      onFocusCapture={openPicker}
      onBlurCapture={closePickerSoon}
    >
      <div
        className={`absolute bottom-full right-0 z-20 mb-2 flex items-center gap-1 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-1.5 shadow-lg transition duration-200 ease-out ${
          isPickerOpen ? 'translate-y-0 opacity-100' : 'pointer-events-none translate-y-1 opacity-0'
        }`}
        role="listbox"
        aria-label="اختيارات التفاعل"
      >
        {reactionOptions.map(({ id, label, Icon, colorClass, activeClass }) => {
          const isSelected = liked && selectedReaction === id;
          return (
            <button
              key={id}
              type="button"
              onClick={() => handleReactionSelect(id)}
              disabled={loading}
              className={`group flex min-w-[72px] flex-col items-center gap-1 rounded-lg px-2 py-2 text-[11px] font-bold transition duration-200 hover:-translate-y-0.5 hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] disabled:cursor-not-allowed disabled:opacity-60 ${
                isSelected ? activeClass : 'text-[var(--admin-muted)]'
              }`}
              title={label}
              aria-label={label}
              aria-selected={isSelected}
              role="option"
            >
              <Icon className={`h-5 w-5 transition group-hover:scale-110 ${isSelected ? '' : colorClass}`} />
              <span className="whitespace-nowrap">{label}</span>
            </button>
          );
        })}
      </div>

      <button
        type="button"
        onClick={() => handleToggle()}
        disabled={loading}
        className={`flex min-h-11 flex-1 items-center justify-center gap-2 rounded-md py-2 text-sm font-semibold transition hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] disabled:cursor-not-allowed disabled:opacity-70 ${
          liked ? selectedReactionOption.activeClass : 'text-[var(--admin-muted)]'
        }`}
        aria-haspopup="listbox"
        aria-expanded={isPickerOpen}
      >
        {liked ? (
          <SelectedIcon className="h-5 w-5" />
        ) : (
          <ThumbsUp className="h-5 w-5" />
        )}
        <span>{count > 0 ? count : ''} {liked ? selectedReactionOption.shortLabel : 'أعجبني'}</span>
      </button>
    </div>
  );
}
