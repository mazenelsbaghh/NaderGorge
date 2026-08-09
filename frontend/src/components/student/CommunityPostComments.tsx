'use client';

import { Send, Pin } from 'lucide-react';
import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';

import {
  communityService,
  type CommunityPostCommentDto,
  type CreateCommunityPostCommentResponse,
} from '@/services/community-service';
import { useAuthStore } from '@/stores/auth-store';
import { UserAvatar } from '@/components/ui/UserAvatar';

type CommunityPostCommentsProps = {
  postId: string;
  commentCount: number;
};

const formatDate = (value: string) =>
  new Intl.DateTimeFormat('ar-EG', { timeZone: 'Africa/Cairo', dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));

export function CommunityPostComments({ postId, commentCount }: CommunityPostCommentsProps) {
  const [comments, setComments] = useState<CommunityPostCommentDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [body, setBody] = useState('');
  const [replyingTo, setReplyingTo] = useState<CommunityPostCommentDto | null>(null);
  const [isOpen] = useState(true);
  const user = useAuthStore((state) => state.user);

  useEffect(() => {
    if (!isOpen && commentCount > 0) return;
    setLoading(true);
    communityService
      .getCommunityPostComments(postId)
      .then((response) => setComments(response.data?.data ?? []))
      .catch(() => setComments([]))
      .finally(() => setLoading(false));
  }, [isOpen, postId, commentCount]);

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    const trimmed = body.trim();
    if (!trimmed) {
      toast.error('اكتب تعليقًا صالحًا قبل الإرسال.');
      return;
    }

    setSubmitting(true);
    try {
      const response = await communityService.createCommunityPostComment(postId, trimmed, replyingTo?.id);
      const created = response.data?.data as CreateCommunityPostCommentResponse | undefined;
      if (created?.message) {
        toast.success(created.message);
      }
      setBody('');
      setReplyingTo(null);
      // Optimistic upate or refetch; we can just refetch
      communityService
        .getCommunityPostComments(postId)
        .then((res) => setComments(res.data?.data ?? []))
        .catch(() => {});
    } catch {
      // handled globally
    } finally {
      setSubmitting(false);
    }
  };

  const commentsByParent = new Map<string | null, CommunityPostCommentDto[]>();
  comments.forEach((comment) => {
    const key = comment.parentCommentId ?? null;
    commentsByParent.set(key, [...(commentsByParent.get(key) ?? []), comment]);
  });

  const renderComment = (comment: CommunityPostCommentDto, depth = 0): React.ReactNode => (
    <div key={comment.id} className={depth > 0 ? 'ms-7 border-s border-[var(--admin-border)] ps-3' : ''}>
      <article className="flex gap-2 group">
        <UserAvatar
          avatarSlug={comment.authorAvatarSlug}
          fullName={comment.authorName}
          size="sm"
          className="mt-0.5"
        />
        <div className="flex flex-col items-start w-full">
          <div
            className={
              comment.isPinned
                ? 'bg-amber-50/70 dark:bg-amber-950/20 border border-amber-200 dark:border-amber-900/40 rounded-2xl px-3 py-2 max-w-[85%] relative shadow-sm transition-[color,background-color,border-color,opacity,transform,box-shadow] duration-300 hover:shadow-md'
                : 'bg-[#f0f2f5] dark:bg-[var(--admin-card-soft)] rounded-2xl px-3 py-2 max-w-[85%]'
            }
          >
            {comment.isPinned && (
              <div className="flex items-center gap-1 text-xs font-bold text-amber-600 dark:text-amber-400 mb-1 select-none">
                <Pin className="h-3 w-3 fill-current rotate-45" />
                <span>تعليق مثبت</span>
              </div>
            )}
            <span className="font-bold text-sm text-gray-900 dark:text-[var(--admin-text)] hover:underline cursor-pointer block leading-tight mb-0.5">
              {comment.authorName}
            </span>
            <p className="text-[14px] text-gray-900 dark:text-gray-200 leading-snug whitespace-pre-wrap">
              {comment.body}
            </p>
          </div>
          <div className="flex items-center gap-3 px-3 mt-1 text-sm font-bold text-gray-500 hover:text-gray-700 dark:text-[var(--admin-muted)] transition-colors">
            <span className="cursor-pointer hover:underline">أعجبني</span>
            <button
              type="button"
              className="cursor-pointer hover:underline"
              onClick={() => setReplyingTo(comment)}
            >
              رد
            </button>
            <span className="font-normal text-gray-400 dark:text-gray-500 text-xs">{formatDate(comment.createdAt)}</span>
          </div>
        </div>
      </article>
      <div className="mt-3 space-y-3">
        {(commentsByParent.get(comment.id) ?? []).map((reply) => renderComment(reply, depth + 1))}
      </div>
    </div>
  );

  return (
    <div className="w-full">
      {/* Comments List */}
      {!loading && comments.length > 0 && (
        <div className="mb-4 space-y-3">
          {(commentsByParent.get(null) ?? []).map((comment) => renderComment(comment))}
        </div>
      )}

      {loading && (
        <div className="mb-4 space-y-3">
          <div className="flex gap-2">
            <div className="h-8 w-8 rounded-full bg-gray-200 dark:bg-[var(--admin-card-soft)] animate-pulse" />
            <div className="h-12 w-3/4 rounded-2xl bg-gray-200 dark:bg-[var(--admin-card-soft)] animate-pulse" />
          </div>
        </div>
      )}

      {/* Input Form */}
      <form onSubmit={handleSubmit} className="flex gap-2 items-start mt-2">
        <UserAvatar
          avatarSlug={user?.avatarSlug}
          fullName={user?.fullName}
          size="sm"
        />
        <div className="flex-1 flex items-center bg-[#f0f2f5] dark:bg-[var(--admin-card-soft)] rounded-2xl px-3 py-1 relative">
          {replyingTo && (
            <button
              type="button"
              onClick={() => setReplyingTo(null)}
              className="absolute -top-6 end-1 text-xs font-semibold text-[#0866ff] hover:underline"
            >
              الرد على {replyingTo.authorName} ×
            </button>
          )}
          <textarea
            id={`comment-input-${postId}`}
            className="w-full bg-transparent border-none focus:ring-0 text-[14px] text-gray-900 dark:text-[var(--admin-text)] placeholder-gray-500 dark:placeholder-gray-400 py-1.5 resize-none outline-none min-h-[36px]"
            value={body}
            onChange={(e) => {
              setBody(e.target.value);
              e.target.style.height = 'auto';
              e.target.style.height = e.target.scrollHeight + 'px';
            }}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                handleSubmit(e as unknown as React.FormEvent);
              }
            }}
            placeholder={replyingTo ? `اكتب ردًا إلى ${replyingTo.authorName}...` : 'اكتب تعليقًا...'}
            maxLength={2000}
            rows={1}
          />
          <button
            type="submit"
            disabled={submitting || !body.trim()}
            className="flex-shrink-0 text-[#0866ff] disabled:opacity-50 p-1 ms-1 hover:bg-gray-200 dark:hover:bg-[var(--admin-hover)] rounded-full transition-colors"
          >
            <Send className="h-4 w-4" />
          </button>
        </div>
      </form>
    </div>
  );
}
