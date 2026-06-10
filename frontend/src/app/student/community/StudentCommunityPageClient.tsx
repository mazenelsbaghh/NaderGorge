'use client';

import { useCallback, useEffect, useState } from 'react';

import {
  communityService,
  type CommunityPostFeedDto,
  type MyCommunityPostDto,
} from '@/services/community-service';
import { CommunityFeed } from '@/components/student/CommunityFeed';
import { CommunityPostComposer } from '@/components/student/CommunityPostComposer';
import { MyCommunityPostsPanel } from '@/components/student/MyCommunityPostsPanel';

export default function StudentCommunityPageClient() {
  const [posts, setPosts] = useState<CommunityPostFeedDto[]>([]);
  const [myPosts, setMyPosts] = useState<MyCommunityPostDto[]>([]);
  const [loading, setLoading] = useState(true);

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const [postsResponse, myPostsResponse] = await Promise.all([
        communityService.getCommunityPosts(),
        communityService.getMyCommunityPosts(),
      ]);

      setPosts(postsResponse.data?.data ?? []);
      setMyPosts(myPostsResponse.data?.data ?? []);
    } catch {
      setPosts([]);
      setMyPosts([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  return (
    <div className="space-y-8">
      <CommunityPostComposer
        onCreated={(post) => setMyPosts((current) => [post, ...current])}
      />

      <div className="grid gap-6 xl:grid-cols-[0.9fr_1.1fr]">
        <MyCommunityPostsPanel posts={myPosts} loading={loading} />
        <CommunityFeed posts={posts} loading={loading} />
      </div>
    </div>
  );
}
