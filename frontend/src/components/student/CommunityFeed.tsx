'use client';

import { ThumbsUp, Globe2, MessageSquare, Share2 } from 'lucide-react';

import type { CommunityPostFeedDto } from '@/services/community-service';
import { UserAvatar } from '@/components/ui/UserAvatar';

import { CommunityPostComments } from './CommunityPostComments';
import { CommunityPostLikeButton } from './CommunityPostLikeButton';
import { CommunityPostPoll } from './CommunityPostPoll';

type CommunityFeedProps = {
  posts: CommunityPostFeedDto[];
  loading?: boolean;
};

const formatDate = (value: string) =>
  new Intl.DateTimeFormat('ar-EG', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));

export function CommunityFeed({ posts, loading = false }: CommunityFeedProps) {
  return (
    <section className="space-y-6">
      {loading ? (
        <div className="space-y-4">
          <div className="h-40 animate-pulse rounded-xl bg-[var(--admin-card-soft)]" />
          <div className="h-40 animate-pulse rounded-xl bg-[var(--admin-card-soft)]" />
        </div>
      ) : posts.length === 0 ? (
        <div className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-5 py-10 text-center shadow-sm">
          <p className="text-base font-bold text-[var(--admin-text)]">لا توجد بوستات معتمدة حتى الآن.</p>
          <p className="mt-2 text-sm font-medium text-[var(--admin-muted)]">
            أول بوست مقبول سيبدأ نبض المجتمع هنا.
          </p>
        </div>
      ) : (
        <div className="space-y-4">
          {posts.map((post) => (
            <article
              key={post.id}
              className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-sm dark:shadow-none"
            >
              {/* Header */}
              <div className="flex items-center justify-between px-4 pt-4">
                <div className="flex items-center gap-3">
                  <UserAvatar
                    avatarSlug={post.authorAvatarSlug}
                    fullName={post.authorName}
                    size="sm"
                  />
                  <div>
                    <h4 className="text-[15px] font-bold text-[var(--admin-text)] hover:underline cursor-pointer">{post.authorName}</h4>
                    <div className="flex items-center gap-1 text-[13px] text-[var(--admin-muted)]">
                      <span>{formatDate(post.createdAt)}</span>
                      <span>·</span>
                      <Globe2 className="h-3 w-3" />
                    </div>
                  </div>
                </div>
              </div>

              {/* Body */}
              <div className="px-4 py-3">
                <p className="whitespace-pre-wrap text-[15px] leading-relaxed text-[var(--admin-text)]">
                  {post.body}
                </p>
                {post.isPoll && post.pollOptions?.length > 0 && (
                  <CommunityPostPoll
                    postId={post.id}
                    options={post.pollOptions}
                    initialUserVoteOptionId={post.userVoteOptionId}
                  />
                )}
              </div>

              {/* Stats Row */}
              {(post.likeCount > 0 || post.commentCount > 0) && (
                <div className="mx-4 flex items-center justify-between border-b border-[var(--admin-border)] pb-2 text-[13px] text-[var(--admin-muted)]">
                  <div className="flex items-center gap-1">
                    {post.likeCount > 0 && (
                      <>
                        <div className="flex h-[18px] w-[18px] items-center justify-center rounded-full bg-[#0866ff]">
                          <ThumbsUp className="h-[10px] w-[10px] fill-white text-white" />
                        </div>
                        <span className="ms-1">{post.likeCount}</span>
                      </>
                    )}
                  </div>
                  <div>
                    {post.commentCount > 0 && <span>{post.commentCount} تعليقاً</span>}
                  </div>
                </div>
              )}

              {/* Action Buttons */}
              <div className="mx-4 flex items-center justify-between gap-1 py-1">
                <CommunityPostLikeButton
                  postId={post.id}
                  initialLiked={post.isLikedByCurrentUser}
                  initialCount={post.likeCount}
                />
                <button
                  type="button"
                  className="flex flex-1 items-center justify-center gap-2 rounded-md py-2 text-sm font-semibold text-gray-500 transition hover:bg-[var(--admin-hover)] dark:text-[var(--admin-muted)]"
                  onClick={() => document.getElementById(`comment-input-${post.id}`)?.focus()}
                >
                  <MessageSquare className="h-5 w-5" />
                  <span>تعليق</span>
                </button>
                <button
                  type="button"
                  className="flex flex-1 items-center justify-center gap-2 rounded-md py-2 text-sm font-semibold text-gray-500 transition hover:bg-[var(--admin-hover)] dark:text-[var(--admin-muted)]"
                >
                  <Share2 className="h-5 w-5" />
                  <span>مشاركة</span>
                </button>
              </div>

              {/* Comments Section */}
              <div className="border-t border-[var(--admin-border)] px-4 py-3">
                <CommunityPostComments postId={post.id} commentCount={post.commentCount} />
              </div>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}
