import React, { useState } from "react";
import { Search, MessageSquare, Briefcase, User as UserIcon } from "lucide-react";
import { ChatRoomDto } from "@/services/chat-service";
import { formatDate } from "@/components/admin/admin-utils";

interface ChatSidebarProps {
  rooms: ChatRoomDto[];
  selectedRoomId?: string;
  onSelectRoom: (roomId: string) => void;
  typingUsers: Record<string, { userName: string; timestamp: number; roomId: string }>;
}

export const ChatSidebar: React.FC<ChatSidebarProps> = ({
  rooms,
  selectedRoomId,
  onSelectRoom,
  typingUsers,
}) => {
  const [searchQuery, setSearchQuery] = useState("");

  const filteredRooms = rooms.filter((r) =>
    r.name.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const getRoomIcon = (type: string) => {
    switch (type) {
      case "Individual":
        return <UserIcon className="h-5 w-5 text-indigo-400" />;
      case "Workroom":
        return <Briefcase className="h-5 w-5 text-emerald-400" />;
      default:
        return <MessageSquare className="h-5 w-5 text-amber-400" />;
    }
  };

  const getRoomSubtitle = (room: ChatRoomDto) => {
    const typing = Object.values(typingUsers).find(
      (t) => t.roomId === room.id && Date.now() - t.timestamp < 3000
    );

    if (typing) {
      return (
        <span className="text-xs text-[var(--admin-primary)] font-bold animate-pulse">
          {typing.userName} يكتب الآن...
        </span>
      );
    }

    if (room.lastMessage) {
      return (
        <span className="text-xs text-[var(--admin-muted)] line-clamp-1">
          <span className="font-bold text-[var(--admin-text)]">
            {room.lastMessage.senderName}:{" "}
          </span>
          {room.lastMessage.content}
        </span>
      );
    }

    return <span className="text-xs text-[var(--admin-muted)] italic">لا توجد رسائل بعد</span>;
  };

  return (
    <div className="flex flex-col h-full border-l border-[var(--admin-border)] bg-[var(--admin-card)]/50 backdrop-blur-md rounded-r-3xl overflow-hidden">
      <div className="p-4 border-b border-[var(--admin-border)]">
        <h2 className="text-lg font-black text-[var(--admin-text-strong)] mb-4">المحادثات الداخلية</h2>
        <div className="flex items-center bg-[var(--admin-surface)] rounded-2xl border border-[var(--admin-border)] px-3 py-2 shadow-sm">
          <Search className="text-[var(--admin-muted)] w-4 h-4 ml-2" />
          <input
            type="text"
            placeholder="البحث عن محادثة..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="bg-transparent border-none outline-none text-xs text-[var(--admin-text)] placeholder:text-[var(--admin-muted)] w-full text-right"
            dir="rtl"
          />
        </div>
      </div>

      <div className="flex-1 overflow-y-auto p-2 space-y-1">
        {filteredRooms.map((room) => {
          const isSelected = room.id === selectedRoomId;
          return (
            <button
              key={room.id}
              onClick={() => onSelectRoom(room.id)}
              className={`w-full flex items-center gap-3 p-3 rounded-2xl text-right transition-all duration-300 ${
                isSelected
                  ? "bg-[var(--admin-primary-15)] border-r-4 border-[var(--admin-primary)] shadow-sm"
                  : "hover:bg-[var(--admin-card-soft)]"
              }`}
            >
              <div className="p-2.5 rounded-xl bg-[var(--admin-card-strong)] border border-[var(--admin-border)] shadow-sm">
                {getRoomIcon(room.type)}
              </div>

              <div className="flex-1 min-w-0">
                <div className="flex justify-between items-baseline mb-0.5">
                  <h3 className="text-sm font-extrabold text-[var(--admin-text)] truncate">
                    {room.name}
                  </h3>
                  {room.lastMessage && (
                    <span className="text-[10px] text-[var(--admin-muted)] whitespace-nowrap">
                      {formatDate(room.lastMessage.createdAt)}
                    </span>
                  )}
                </div>
                <div className="flex justify-between items-center gap-2">
                  {getRoomSubtitle(room)}
                  {room.unreadCount > 0 && (
                    <span className="bg-[var(--admin-primary)] text-white text-[10px] font-black h-5 min-w-5 px-1.5 rounded-full flex items-center justify-center animate-bounce shadow">
                      {room.unreadCount}
                    </span>
                  )}
                </div>
              </div>
            </button>
          );
        })}

        {filteredRooms.length === 0 && (
          <div className="text-center py-8 text-[var(--admin-muted)] text-sm">
            لا توجد محادثات متطابقة.
          </div>
        )}
      </div>
    </div>
  );
};
