'use client';

import { ThumbsUp } from 'lucide-react';
import { useState } from 'react';

import { communityService } from '@/services/community-service';

type CommunityPostLikeButtonProps = {
  postId: string;
  initialLiked: boolean;
  initialCount: number;
};

export function CommunityPostLikeButton({
  postId,
  initialLiked,
  initialCount,
}: CommunityPostLikeButtonProps) {
  const [liked, setLiked] = useState(initialLiked);
  const [count, setCount] = useState(initialCount);
  const [loading, setLoading] = useState(false);

  const handleToggle = async () => {
    if (loading) return;
    setLoading(true);
    try {
      const response = await communityService.toggleCommunityPostLike(postId);
      const data = response.data?.data;
      if (data) {
        setLiked(data.isLikedByCurrentUser);
        setCount(data.likeCount);
      }
    } catch {
      // handled globally
    } finally {
      setLoading(false);
    }
  };

  return (
    <button
      type="button"
      onClick={handleToggle}
      disabled={loading}
      className={`flex flex-1 items-center justify-center gap-2 rounded-md py-2 text-sm font-semibold transition hover:bg-[var(--admin-hover)] ${
        liked ? 'text-[#0866ff]' : 'text-gray-500 dark:text-[var(--admin-muted)]'
      }`}
    >
      <ThumbsUp className={`h-5 w-5 ${liked ? 'fill-current' : ''}`} />
      <span>{count > 0 ? count : ''} أعجبني</span>
    </button>
  );
}
