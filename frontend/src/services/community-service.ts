import apiClient from './api-client';

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
  getCommunityPosts: () => apiClient.get<CommunityApiResponse<CommunityPostFeedDto[]>>('/community/posts'),
  getMyCommunityPosts: () => apiClient.get<CommunityApiResponse<MyCommunityPostDto[]>>('/community/posts/mine'),
  createCommunityPost: (body: string, pollOptions?: string[]) =>
    apiClient.post<CommunityApiResponse<CreateCommunityPostResponse>>('/community/posts', { body, pollOptions }),
  getCommunityPostComments: (postId: string) =>
    apiClient.get<CommunityApiResponse<CommunityPostCommentDto[]>>(`/community/posts/${postId}/comments`),
  createCommunityPostComment: (postId: string, body: string) =>
    apiClient.post<CommunityApiResponse<CreateCommunityPostCommentResponse>>(`/community/posts/${postId}/comments`, { body }),
  toggleCommunityPostLike: (postId: string) =>
    apiClient.post<CommunityApiResponse<ToggleCommunityPostLikeResponse>>(`/community/posts/${postId}/likes/toggle`, {}),
  toggleCommunityPostVote: (postId: string, optionId: string) =>
    apiClient.post<CommunityApiResponse<ToggleCommunityPostVoteResponse>>(`/community/posts/${postId}/polls/${optionId}/vote`, {}),
};
