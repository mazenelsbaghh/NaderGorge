import React, { useEffect, useState, useCallback } from "react";
import { useAuthStore } from "@/stores/auth-store";
import { chatService, ChatRoomDto, ChatMessageDto } from "@/services/chat-service";
import { ChatSidebar } from "./ChatSidebar";
import { ChatWindow } from "./ChatWindow";
import { useSignalR, SignalRMessage } from "@/hooks/useSignalR";
import { devConsole } from "@/utils/dev-console";
import toast from "react-hot-toast";

export const ChatContainer: React.FC = () => {
  const { user } = useAuthStore();
  const [rooms, setRooms] = useState<ChatRoomDto[]>([]);
  const [selectedRoomId, setSelectedRoomId] = useState<string | undefined>(undefined);
  const [messages, setMessages] = useState<ChatMessageDto[]>([]);
  const [loadingRooms, setLoadingRooms] = useState(true);
  const [typingUsers, setTypingUsers] = useState<Record<string, { userName: string; timestamp: number; roomId: string }>>({});

  // Fetch rooms list
  const loadRooms = useCallback(async () => {
    try {
      setLoadingRooms(true);
      const data = await chatService.getRooms();
      setRooms(data);
      if (data.length > 0 && !selectedRoomId) {
        setSelectedRoomId(data[0].id);
      }
    } catch (err) {
      devConsole.error(err);
      toast.error("تعذر تحميل المحادثات");
    } finally {
      setLoadingRooms(false);
    }
  }, [selectedRoomId]);

  useEffect(() => {
    void loadRooms();
  }, [loadRooms]);

  // Fetch messages when room changes
  useEffect(() => {
    if (!selectedRoomId) return;

    const loadMessages = async () => {
      try {
        const data = await chatService.getRoomMessages(selectedRoomId);
        setMessages(data);
        
        // Mark room as read
        await chatService.markRoomRead(selectedRoomId);
        
        // Reset unread count locally
        setRooms((prev) =>
          prev.map((r) => (r.id === selectedRoomId ? { ...r, unreadCount: 0 } : r))
        );
      } catch (err) {
        devConsole.error(err);
      }
    };

    void loadMessages();
  }, [selectedRoomId]);

  // Handle incoming real-time message
  const handleMessageReceived = useCallback((msg: SignalRMessage) => {
    // 1. If message belongs to selected room, append it to messages feed
    if (msg.roomId === selectedRoomId) {
      setMessages((prev) => {
        if (prev.some((m) => m.id === msg.id)) return prev;
        return [...prev, {
          id: msg.id,
          roomId: msg.roomId,
          senderUserId: msg.senderUserId,
          senderName: msg.senderName,
          content: msg.content,
          type: msg.type,
          mediaUrl: msg.mediaUrl,
          mediaMetadata: msg.mediaMetadata,
          isPinned: msg.isPinned,
          createdAt: msg.createdAt,
          readBy: msg.readBy,
        }];
      });
      // Acknowledge read status immediately
      void chatService.markRoomRead(msg.roomId);
    }

    // 2. Update last message preview in rooms list
    setRooms((prev) =>
      prev.map((r) => {
        if (r.id === msg.roomId) {
          const isCurrentRoom = r.id === selectedRoomId;
          return {
            ...r,
            unreadCount: isCurrentRoom ? 0 : r.unreadCount + 1,
            lastMessage: {
              id: msg.id,
              content: msg.content,
              senderName: msg.senderName,
              createdAt: msg.createdAt,
            },
          };
        }
        return r;
      })
    );
  }, [selectedRoomId]);

  // Handle incoming typing indicator
  const handleUserTyping = useCallback((roomId: string, userId: string, userName: string) => {
    setTypingUsers((prev) => ({
      ...prev,
      [userId]: { userName, timestamp: Date.now(), roomId },
    }));
  }, []);

  // Initialize SignalR
  const { sendMessage, sendTyping } = useSignalR(handleMessageReceived, handleUserTyping);

  // Send Message
  const handleSendMessage = (content: string, mediaUrl?: string, mediaMetadata?: string) => {
    if (!selectedRoomId) return;
    void sendMessage(selectedRoomId, content, mediaUrl, mediaMetadata);
  };

  // Send Typing
  const handleSendTyping = () => {
    if (!selectedRoomId) return;
    void sendTyping(selectedRoomId);
  };

  // Toggle Pinned message
  const handlePinMessage = async (messageId: string) => {
    try {
      await chatService.togglePinMessage(messageId);
      // Update local pin state
      setMessages((prev) =>
        prev.map((m) => (m.id === messageId ? { ...m, isPinned: !m.isPinned } : m))
      );
      toast.success("تم تعديل تثبيت الرسالة");
    } catch (err) {
      devConsole.error(err);
      toast.error("فشل تعديل تثبيت الرسالة");
    }
  };

  // Toggle Archive Room
  const handleArchiveToggle = async () => {
    if (!selectedRoomId) return;
    const targetRoom = rooms.find((r) => r.id === selectedRoomId);
    if (!targetRoom) return;

    const nextArchived = !targetRoom.isArchived;

    try {
      await chatService.archiveRoom(selectedRoomId, nextArchived);
      setRooms((prev) =>
        prev.map((r) => (r.id === selectedRoomId ? { ...r, isArchived: nextArchived } : r))
      );
      toast.success(nextArchived ? "تم أرشفة الغرفة" : "تم إلغاء أرشفة الغرفة");
    } catch (err) {
      devConsole.error(err);
      toast.error("فشل تعديل حالة الأرشفة");
    }
  };

  const selectedRoom = rooms.find((r) => r.id === selectedRoomId);
  const isUserPrivileged = user?.roles.some((r) => r === "Admin" || r === "Supervisor") ?? false;

  if (loadingRooms) {
    return (
      <div className="flex h-[75vh] items-center justify-center text-[var(--admin-muted)] text-sm">
        جاري تحميل قنوات المحادثة...
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 md:grid-cols-[280px_1fr] h-[75vh] max-h-[800px] border border-[var(--admin-border)] rounded-3xl shadow-[0_8px_30px_rgb(0,0,0,0.04)] overflow-hidden">
      {/* Sidebar (Rooms List) */}
      <ChatSidebar
        rooms={rooms}
        selectedRoomId={selectedRoomId}
        onSelectRoom={setSelectedRoomId}
        typingUsers={typingUsers}
      />

      {/* Main Chat Panel */}
      {selectedRoom ? (
        <ChatWindow
          roomId={selectedRoom.id}
          messages={messages}
          currentUserId={user?.id || ""}
          onSendMessage={handleSendMessage}
          onSendTyping={handleSendTyping}
          isRoomArchived={selectedRoom.isArchived}
          isUserPrivileged={isUserPrivileged}
          onArchiveToggle={handleArchiveToggle}
          onPinMessage={handlePinMessage}
          roomName={selectedRoom.name}
        />
      ) : (
        <div className="flex h-full items-center justify-center text-[var(--admin-muted)] text-sm border-r border-[var(--admin-border)]">
          اختر محادثة للبدء في التواصل
        </div>
      )}
    </div>
  );
};
