import apiClient from './api-client';

export interface ChatMessageDto {
  id: string;
  roomId: string;
  senderUserId: string;
  senderName: string;
  content: string;
  type: string;
  mediaUrl?: string;
  mediaMetadata?: string;
  isPinned: boolean;
  createdAt: string;
  readBy: string[];
}

export interface ChatRoomDto {
  id: string;
  name: string;
  type: string;
  taskItemId?: string;
  isArchived: boolean;
  createdAt: string;
  unreadCount: number;
  lastMessage?: {
    id: string;
    content: string;
    senderName: string;
    createdAt: string;
  };
}

export interface CreateRoomPayload {
  name?: string;
  type: 'Individual' | 'Group' | 'Workroom';
  participantIds: string[];
  taskItemId?: string;
}

interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data: T;
}

export const chatService = {
  getRooms: () => 
    apiClient.get<ApiResponse<ChatRoomDto[]>>('/chat/rooms').then(r => r.data.data),

  getRoomMessages: (roomId: string, page = 1, pageSize = 50) => 
    apiClient.get<ApiResponse<ChatMessageDto[]>>(`/chat/rooms/${roomId}/messages?page=${page}&pageSize=${pageSize}`).then(r => r.data.data),

  createRoom: (payload: CreateRoomPayload) => 
    apiClient.post<ApiResponse<string>>('/chat/rooms', payload).then(r => r.data.data),

  archiveRoom: (roomId: string, isArchived: boolean) => 
    apiClient.post<ApiResponse<void>>(`/chat/rooms/${roomId}/archive`, { isArchived }).then(r => r.data),

  togglePinMessage: (messageId: string) => 
    apiClient.post<ApiResponse<void>>(`/chat/messages/${messageId}/pin`).then(r => r.data),

  markRoomRead: (roomId: string) => 
    apiClient.post<ApiResponse<void>>(`/chat/rooms/${roomId}/read`).then(r => r.data),
};
