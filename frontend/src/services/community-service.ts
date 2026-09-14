import apiClient from './api-client';
import { invalidateMany } from '@/lib/cache-invalidation';

interface CommunityApiResponse<T> {
  success?: boolean;
  message?: string;
  data?: T;
}

export interface CommunityPostPollOptionDto {
  id: string;
  text: string;
  voteCount: number;
}

export interface CommunityPostFeedDto {
  id: string;
  authorName: string;
  body: string;
  createdAt: string;
  likeCount: number;
  commentCount: number;
  isLikedByCurrentUser: boolean;
  isPoll: boolean;
  userVoteOptionId: string | null;
  pollOptions: CommunityPostPollOptionDto[];
  authorAvatarSlug?: string | null;
}

export interface MyCommunityPostDto {
  id: string;
  body: string;
  status: string;
  createdAt: string;
  isPoll: boolean;
}

export interface CreateCommunityPostResponse {
  id: string;
  status: string;
  createdAt: string;
  message: string;
}

export interface CommunityPostCommentDto {
  id: string;
  postId: string;
  parentCommentId: string | null;
  authorName: string;
  body: string;
  createdAt: string;
  isOwnComment: boolean;
  authorAvatarSlug?: string | null;
  isPinned: boolean;
}

export interface CreateCommunityPostCommentResponse {
  id: string;
  postId: string;
  parentCommentId: string | null;
  createdAt: string;
  status: string;
  message: string;
}

export interface ToggleCommunityPostLikeResponse {
  postId: string;
  isLikedByCurrentUser: boolean;
  likeCount: number;
}

export interface ToggleCommunityPostVoteResponse {
  postId: string;
  optionIdSelected: string | null;
  optionVoteCounts: Record<string, number>;
}

export const communityService = {
  getTeacherCommunityPosts: (teacherId: string) =>
    apiClient.get<CommunityApiResponse<CommunityPostFeedDto[]>>(`/public/teachers/${teacherId}/community-posts`),
  getMyTeacherCommunityPosts: (teacherId: string) =>
    apiClient.get<CommunityApiResponse<MyCommunityPostDto[]>>(`/public/teachers/${teacherId}/community-posts/mine`),
  createTeacherCommunityPost: async (teacherId: string, body: string, pollOptions?: string[]) => {
    const response = await apiClient.post<CommunityApiResponse<CreateCommunityPostResponse>>(`/public/teachers/${teacherId}/community-posts`, { body, pollOptions });
    invalidateMany(['community:posts']);
    return response;
  },
  getCommunityPostComments: (postId: string) =>
    apiClient.get<CommunityApiResponse<CommunityPostCommentDto[]>>(`/community/posts/${postId}/comments`),
  createCommunityPostComment: async (postId: string, body: string, parentCommentId?: string) => {
    const response = await apiClient.post<CommunityApiResponse<CreateCommunityPostCommentResponse>>(`/community/posts/${postId}/comments`, { body, parentCommentId });
    invalidateMany(['community:posts']);
    return response;
  },
  toggleCommunityPostLike: async (postId: string) => {
    const response = await apiClient.post<CommunityApiResponse<ToggleCommunityPostLikeResponse>>(`/community/posts/${postId}/likes/toggle`, {});
    invalidateMany(['community:posts']);
    return response;
  },
  toggleCommunityPostVote: async (postId: string, optionId: string) => {
    const response = await apiClient.post<CommunityApiResponse<ToggleCommunityPostVoteResponse>>(`/community/posts/${postId}/polls/${optionId}/vote`, {});
    invalidateMany(['community:posts']);
    return response;
  },
};
