'use client';

import { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';

import {
  communityService,
  type CommunityPostFeedDto,
  type CreateCommunityPostResponse,
  type MyCommunityPostDto,
} from '@/services/community-service';
import { CommunityFeed } from '@/components/student/CommunityFeed';
import { CommunityPostComposer } from '@/components/student/CommunityPostComposer';
import { MyCommunityPostsPanel } from '@/components/student/MyCommunityPostsPanel';
import { registerCacheStore } from '@/lib/cache-invalidation';

type TeacherCommunityPanelProps = {
  teacherId: string;
};

export function TeacherCommunityPanel({ teacherId }: TeacherCommunityPanelProps) {
  const [posts, setPosts] = useState<CommunityPostFeedDto[]>([]);
  const [myPosts, setMyPosts] = useState<MyCommunityPostDto[]>([]);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [postsResponse, myPostsResponse] = await Promise.all([
        communityService.getTeacherCommunityPosts(teacherId),
        communityService.getMyTeacherCommunityPosts(teacherId).catch(() => ({ data: { data: [] as MyCommunityPostDto[] } })),
      ]);

      setPosts(postsResponse.data?.data ?? []);
      setMyPosts(myPostsResponse.data?.data ?? []);
    } catch {
      setPosts([]);
      setMyPosts([]);
      toast.error('تعذر تحميل مجتمع المدرس');
    } finally {
      setLoading(false);
    }
  }, [teacherId]);

  useEffect(() => {
    void load();
    const cleanupCacheStore = registerCacheStore('community:posts', () => {}, () => void load());
    return cleanupCacheStore;
  }, [load]);

  const submitTeacherPost = async (body: string, pollOptions?: string[]): Promise<CreateCommunityPostResponse | undefined> => {
    const response = await communityService.createTeacherCommunityPost(teacherId, body, pollOptions);
    return response.data?.data;
  };

  return (
    <div className="space-y-6">
      <CommunityPostComposer
        placeholder="اكتب سؤالاً أو مشاركة للمدرس..."
        submitPost={submitTeacherPost}
        onCreated={(post) => setMyPosts((current) => [post, ...current])}
      />

      <div className="grid gap-6 xl:grid-cols-[0.9fr_1.1fr]">
        <MyCommunityPostsPanel posts={myPosts} loading={loading} />
        <CommunityFeed posts={posts} loading={loading} />
      </div>
    </div>
  );
}
