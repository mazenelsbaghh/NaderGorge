import React, { useEffect, useRef, useState } from "react";
import { Send, Pin, Lock, LockOpen, Paperclip, FileText, Volume2 } from "lucide-react";
import { ChatMessageDto } from "@/services/chat-service";
import { formatDate } from "@/components/admin/admin-utils";
import NeumorphButton from "@/components/ui/neumorph-button";

interface ChatWindowProps {
  roomId: string;
  messages: ChatMessageDto[];
  currentUserId: string;
  onSendMessage: (content: string, mediaUrl?: string, mediaMetadata?: string) => void;
  onSendTyping: () => void;
  isRoomArchived: boolean;
  isUserPrivileged: boolean;
  onArchiveToggle: () => void;
  onPinMessage: (messageId: string) => void;
  roomName: string;
}

export const ChatWindow: React.FC<ChatWindowProps> = ({
  messages,
  currentUserId,
  onSendMessage,
  onSendTyping,
  isRoomArchived,
  isUserPrivileged,
  onArchiveToggle,
  onPinMessage,
  roomName,
}) => {
  const [inputMessage, setInputMessage] = useState("");
  const [attachmentUrl, setAttachmentUrl] = useState("");
  const [attachmentType, setAttachmentType] = useState<"Text" | "Attachment">("Text");
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  const handleSend = (e: React.FormEvent) => {
    e.preventDefault();
    if (!inputMessage.trim() && !attachmentUrl.trim()) return;

    let mediaUrl = undefined;
    let mediaMetadata = undefined;

    if (attachmentUrl.trim()) {
      mediaUrl = attachmentUrl.trim();
      mediaMetadata = JSON.stringify({
        fileName: attachmentUrl.split("/").pop() || "attachment",
        fileSize: "Unknown Size",
        type: attachmentUrl.endsWith(".mp3") ? "Audio" : "Image",
      });
    }

    onSendMessage(
      inputMessage,
      mediaUrl,
      mediaMetadata
    );

    setInputMessage("");
    setAttachmentUrl("");
    setAttachmentType("Text");
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    setInputMessage(e.target.value);
    onSendTyping();
  };

  const pinnedMessages = messages.filter((m) => m.isPinned);

  const isInputDisabled = isRoomArchived && !isUserPrivileged;

  return (
    <div className="flex flex-col h-full bg-[var(--admin-card)]/30 backdrop-blur-md rounded-l-3xl overflow-hidden border-r border-[var(--admin-border)]">
      {/* Header */}
      <div className="p-4 border-b border-[var(--admin-border)] flex items-center justify-between bg-[var(--admin-card)]/60">
        <div>
          <h2 className="text-base font-black text-[var(--admin-text-strong)]">{roomName}</h2>
          <p className="text-xs text-[var(--admin-muted)] mt-0.5">
            {isRoomArchived ? "مؤرشفة (للقراءة فقط)" : "محادثة نشطة"}
          </p>
        </div>

        {isUserPrivileged && (
          <NeumorphButton
            onClick={onArchiveToggle}
            intent={isRoomArchived ? "primary" : "ghost"}
            size="sm"
            pill
            className="flex items-center gap-1.5"
            title={isRoomArchived ? "إلغاء الأرشفة" : "أرشفة المحادثة"}
          >
            {isRoomArchived ? (
              <>
                <LockOpen className="h-4 w-4" />
                تفعيل الكتابة
              </>
            ) : (
              <>
                <Lock className="h-4 w-4" />
                أرشفة
              </>
            )}
          </NeumorphButton>
        )}
      </div>

      {/* Pinned Messages Bar */}
      {pinnedMessages.length > 0 && (
        <div className="bg-[var(--admin-primary-15)] px-4 py-2 border-b border-[var(--admin-border)] flex items-center justify-between animate-[slideDown_0.3s_ease-out]">
          <div className="flex items-center gap-2 min-w-0">
            <Pin className="h-4 w-4 text-[var(--admin-primary)] fill-[var(--admin-primary)] shrink-0 animate-pulse" />
            <div className="text-xs text-[var(--admin-text)] truncate font-semibold">
              <span className="font-extrabold text-[var(--admin-primary)]">المثبت: </span>
              {pinnedMessages[pinnedMessages.length - 1].content || "ملف مرفق"}
            </div>
          </div>
          <span className="text-[10px] font-bold text-[var(--admin-primary)] bg-white/40 px-2 py-0.5 rounded-full">
            {pinnedMessages.length} رسائل
          </span>
        </div>
      )}

      {/* Message Feed */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        {messages.map((msg) => {
          const isOwn = msg.senderUserId === currentUserId;
          return (
            <div
              key={msg.id}
              className={`flex flex-col max-w-[80%] ${
                isOwn ? "mr-auto items-end" : "ml-auto items-start"
              }`}
            >
              {/* Sender Name */}
              {!isOwn && (
                <span className="text-[10px] font-extrabold text-[var(--admin-muted)] mb-1 px-1">
                  {msg.senderName}
                </span>
              )}

              {/* Message Bubble */}
              <div
                className={`relative group p-3.5 rounded-[1.5rem] shadow-sm transition-all duration-300 border ${
                  isOwn
                    ? "bg-[var(--admin-primary)] text-white border-[var(--admin-primary-strong)] rounded-br-none"
                    : "bg-[var(--admin-card)] text-[var(--admin-text)] border-[var(--admin-border)] rounded-bl-none"
                } ${msg.isPinned ? "border-amber-400 dark:border-amber-500 ring-1 ring-amber-400/30" : ""}`}
              >
                {/* Pin Action on hover */}
                <button
                  onClick={() => onPinMessage(msg.id)}
                  className={`absolute top-2 opacity-0 group-hover:opacity-100 transition-opacity p-1 rounded-md bg-black/10 hover:bg-black/20 ${
                    isOwn ? "-left-8 text-[var(--admin-text)]" : "-right-8 text-[var(--admin-text)]"
                  }`}
                  title={msg.isPinned ? "إلغاء التثبيت" : "تثبيت الرسالة"}
                >
                  <Pin className={`h-3.5 w-3.5 ${msg.isPinned ? "fill-amber-400 text-amber-400" : ""}`} />
                </button>

                {/* Media Attachment */}
                {msg.mediaUrl && (
                  <div className="mb-2 p-2 rounded-xl bg-black/5 border border-black/10 flex items-center gap-2">
                    {msg.mediaUrl.endsWith(".mp3") ? (
                      <Volume2 className="h-5 w-5 text-emerald-400 animate-pulse" />
                    ) : (
                      <Paperclip className="h-5 w-5 text-indigo-400" />
                    )}
                    <a
                      href={msg.mediaUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="text-xs underline font-bold truncate max-w-[150px]"
                    >
                      {msg.mediaMetadata
                        ? JSON.parse(msg.mediaMetadata).fileName
                        : "مرفق"}
                    </a>
                  </div>
                )}

                {/* Content */}
                <p className="text-sm leading-relaxed whitespace-pre-wrap">{msg.content}</p>

                {/* Date & Read Receipt */}
                <div className="flex items-center gap-1.5 justify-end mt-1.5 opacity-60 text-[9px]">
                  <span>{formatDate(msg.createdAt)}</span>
                  {isOwn && (
                    <span className="font-extrabold text-[8px] uppercase">
                      {msg.readBy.length > 1 ? "✓✓ مقروءة" : "✓"}
                    </span>
                  )}
                </div>
              </div>
            </div>
          );
        })}
        <div ref={messagesEndRef} />
      </div>

      {/* Input Form */}
      <div className="p-4 border-t border-[var(--admin-border)] bg-[var(--admin-card)]/50">
        {isInputDisabled ? (
          <div className="flex items-center gap-2 text-sm font-bold text-[var(--admin-muted)] justify-center py-2 bg-[var(--admin-card-soft)] rounded-2xl border border-[var(--admin-border)]">
            <Lock size={16} />
            هذه الغرفة مؤرشفة. لا تملك صلاحية كتابة رسائل جديدة.
          </div>
        ) : (
          <form onSubmit={handleSend} className="space-y-3">
            {attachmentType === "Attachment" && (
              <div className="flex gap-2 items-center bg-[var(--admin-surface)] p-2 rounded-2xl border border-[var(--admin-border)] animate-[fadeIn_0.2s_ease-out]">
                <FileText className="h-4 w-4 text-[var(--admin-primary)]" />
                <input
                  type="text"
                  placeholder="أدخل رابط الملف المرفق (مثال: CDN/Image URL)..."
                  value={attachmentUrl}
                  onChange={(e) => setAttachmentUrl(e.target.value)}
                  className="bg-transparent border-none outline-none text-xs text-[var(--admin-text)] w-full text-right"
                  dir="ltr"
                />
                <button
                  type="button"
                  onClick={() => {
                    setAttachmentType("Text");
                    setAttachmentUrl("");
                  }}
                  className="text-[10px] text-red-500 font-bold px-2"
                >
                  إلغاء
                </button>
              </div>
            )}

            <div className="flex gap-3 items-end">
              <NeumorphButton
                type="submit"
                intent="primary"
                size="icon"
                pill
                className="!h-11 !w-11 shrink-0"
              >
                <Send className="h-4 w-4 rotate-180" />
              </NeumorphButton>

              <textarea
                rows={1}
                value={inputMessage}
                onChange={handleInputChange}
                placeholder="اكتب رسالة... (استخدم @ لمنشن الأعضاء)"
                className="admin-input flex-1 !rounded-2xl resize-none py-3 text-sm min-h-[44px] max-h-[120px]"
                dir="rtl"
              />

              {attachmentType === "Text" && (
                <button
                  type="button"
                  onClick={() => setAttachmentType("Attachment")}
                  className="p-2.5 rounded-2xl bg-[var(--admin-card-soft)] border border-[var(--admin-border)] hover:bg-[var(--admin-primary)]/10 hover:text-[var(--admin-primary)] text-[var(--admin-muted)] transition"
                  title="إرفاق ملف"
                >
                  <Paperclip className="h-5 w-5" />
                </button>
              )}
            </div>
          </form>
        )}
      </div>
    </div>
  );
};
