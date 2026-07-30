"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import {
  CheckCircle2,
  MessageSquareText,
  RefreshCcw,
  Send,
  ShieldX,
} from "lucide-react";
import toast from "react-hot-toast";

import {
  AdminColumn,
  AdminDataTable,
  AdminStatCard,
  AdminTab,
  AdminTabBar,
} from "@/components/admin";
import { TeacherPage } from "@/components/teacher/TeacherShellChrome";
import { communityService } from "@/services/community-service";
import { teacherService } from "@/services/teacher-service";
import type {
  ModerationCommunityCommentDto,
  ModerationCommunityPostDto,
} from "@/services/admin-service";

type CommunityTab = "posts" | "comments";

const TABS: AdminTab<CommunityTab>[] = [
  { key: "posts", label: "المنشورات", icon: MessageSquareText },
  { key: "comments", label: "التعليقات", icon: CheckCircle2 },
];

const formatDate = (value?: string | null) =>
  value
    ? new Intl.DateTimeFormat("ar-EG", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value))
    : "—";

const statusLabel = (status: string) =>
  status === "Approved" ? "مقبول" : status === "Rejected" ? "مرفوض" : "قيد المراجعة";

const statusClass = (status: string) => {
  if (status === "Approved") return "bg-emerald-500/10 text-emerald-600";
  if (status === "Rejected") return "bg-rose-500/10 text-rose-600";
  return "bg-[var(--admin-primary-15)] text-[var(--admin-primary)]";
};

export default function TeacherCommunityPageClient() {
  const [activeTab, setActiveTab] = useState<CommunityTab>("posts");
  const [posts, setPosts] = useState<ModerationCommunityPostDto[]>([]);
  const [comments, setComments] = useState<ModerationCommunityCommentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [actingId, setActingId] = useState<string | null>(null);
  const [replyByPost, setReplyByPost] = useState<Record<string, string>>({});

  const load = useCallback(async () => {
    try {
      setLoading(true);
      const [nextPosts, nextComments] = await Promise.all([
        teacherService.getCommunityPostsForModeration("All"),
        teacherService.getPendingCommunityComments(),
      ]);
      setPosts(nextPosts);
      setComments(nextComments);
    } catch {
      toast.error("تعذر تحميل مجتمع المدرس.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const approvePost = async (postId: string) => {
    try {
      setActingId(postId);
      await teacherService.approveCommunityPost(postId);
      toast.success("تم قبول المنشور.");
      await load();
    } catch {
      toast.error("تعذر قبول المنشور.");
    } finally {
      setActingId(null);
    }
  };

  const rejectPost = async (postId: string) => {
    try {
      setActingId(postId);
      await teacherService.rejectCommunityPost(postId);
      toast.success("تم رفض المنشور.");
      await load();
    } catch {
      toast.error("تعذر رفض المنشور.");
    } finally {
      setActingId(null);
    }
  };

  const moderateComment = async (commentId: string, action: "approve" | "reject") => {
    try {
      setActingId(commentId);
      if (action === "approve") await teacherService.approveCommunityComment(commentId);
      else await teacherService.rejectCommunityComment(commentId, "رفض بواسطة المدرس");
      toast.success(action === "approve" ? "تم قبول التعليق." : "تم رفض التعليق.");
      await load();
    } catch {
      toast.error("تعذر تنفيذ الإجراء.");
    } finally {
      setActingId(null);
    }
  };

  const sendReply = async (postId: string) => {
    const body = replyByPost[postId]?.trim();
    if (!body) return;

    try {
      setActingId(`reply-${postId}`);
      await communityService.createCommunityPostComment(postId, body);
      setReplyByPost((current) => ({ ...current, [postId]: "" }));
      toast.success("تم نشر رد المدرس.");
      await load();
    } catch {
      toast.error("تعذر نشر الرد. تأكد أن المنشور مقبول أولاً.");
    } finally {
      setActingId(null);
    }
  };

  const pendingPosts = useMemo(() => posts.filter((post) => post.status === "Pending").length, [posts]);

  const postColumns: AdminColumn<ModerationCommunityPostDto>[] = [
    {
      key: "student",
      label: "الطالب",
      render: (post) => (
        <div className="space-y-1">
          <p className="font-black text-[var(--admin-text)]">{post.studentName}</p>
          <p className="text-xs font-bold text-[var(--admin-muted)]">{formatDate(post.createdAt)}</p>
        </div>
      ),
    },
    {
      key: "body",
      label: "المنشور",
      render: (post) => (
        <p className="line-clamp-3 max-w-xl whitespace-pre-wrap text-sm font-bold leading-7 text-[var(--admin-text)]">
          {post.body}
        </p>
      ),
    },
    {
      key: "status",
      label: "الحالة",
      align: "center",
      render: (post) => (
        <span className={`inline-flex rounded-full px-3 py-1 text-xs font-black ${statusClass(post.status)}`}>
          {statusLabel(post.status)}
        </span>
      ),
    },
    {
      key: "actions",
      label: "الإجراء",
      align: "left",
      render: (post) =>
        post.status === "Pending" ? (
          <div className="flex flex-wrap justify-end gap-2">
            <ActionButton
              icon={CheckCircle2}
              label="قبول"
              disabled={actingId === post.id}
              onClick={() => approvePost(post.id)}
              tone="success"
            />
            <ActionButton
              icon={ShieldX}
              label="رفض"
              disabled={actingId === post.id}
              onClick={() => rejectPost(post.id)}
              tone="danger"
            />
          </div>
        ) : (
          <span className="text-xs font-bold text-[var(--admin-muted)]">تمت المراجعة</span>
        ),
    },
  ];

  const commentColumns: AdminColumn<ModerationCommunityCommentDto>[] = [
    {
      key: "student",
      label: "الطالب",
      render: (comment) => (
        <div className="space-y-1">
          <p className="font-black text-[var(--admin-text)]">{comment.studentName}</p>
          <p className="text-xs font-bold text-[var(--admin-muted)]">{formatDate(comment.createdAt)}</p>
        </div>
      ),
    },
    {
      key: "comment",
      label: "التعليق",
      render: (comment) => (
        <p className="line-clamp-4 max-w-xl whitespace-pre-wrap text-sm font-bold leading-7 text-[var(--admin-text)]">
          {comment.body}
        </p>
      ),
    },
    {
      key: "actions",
      label: "الإجراء",
      align: "left",
      render: (comment) => (
        <div className="flex flex-wrap justify-end gap-2">
          <ActionButton
            icon={CheckCircle2}
            label="قبول"
            disabled={actingId === comment.id}
            onClick={() => moderateComment(comment.id, "approve")}
            tone="success"
          />
          <ActionButton
            icon={ShieldX}
            label="رفض"
            disabled={actingId === comment.id}
            onClick={() => moderateComment(comment.id, "reject")}
            tone="danger"
          />
        </div>
      ),
    },
  ];

  return (
    <TeacherPage
      activePath="/teacher/community"
      sectionLabel="مجتمع المدرس"
      pageTitle="منشورات وتعليقات الطلاب"
      subtitle="راجع ما يخص مجتمع صفحتك، واقبل أو ارفض، ورد باسمك على المنشورات المقبولة."
      action={
        <button type="button" onClick={load} disabled={loading} className="admin-btn-ghost inline-flex items-center gap-2">
          <RefreshCcw className="h-4 w-4" />
          تحديث
        </button>
      }
    >
      <div className="space-y-8" dir="rtl">
        <section className="grid grid-cols-1 gap-6 md:grid-cols-3">
          <AdminStatCard variant="light" icon={MessageSquareText} label="منشورات مرتبطة بك" value={posts.length} />
          <AdminStatCard variant="accent" icon={CheckCircle2} label="تنتظر مراجعة" value={pendingPosts} />
          <AdminStatCard variant="muted" icon={MessageSquareText} label="تعليقات معلقة" value={comments.length} />
        </section>

        <AdminTabBar tabs={TABS} activeTab={activeTab} onSelect={setActiveTab} />

        {activeTab === "posts" ? (
          <AdminDataTable
            data={posts}
            columns={postColumns}
            loading={loading}
            rowKey={(post) => post.id}
            emptyMessage="لا توجد منشورات مرتبطة بك بعد."
            expandedRowRender={(post) =>
              post.status === "Approved" ? (
                <div className="flex flex-col gap-3 md:flex-row">
                  <input
                    value={replyByPost[post.id] ?? ""}
                    onChange={(event) =>
                      setReplyByPost((current) => ({ ...current, [post.id]: event.target.value }))
                    }
                    placeholder="اكتب رد المدرس..."
                    className="admin-input"
                  />
                  <button
                    type="button"
                    onClick={() => sendReply(post.id)}
                    disabled={actingId === `reply-${post.id}`}
                    className="admin-btn-primary inline-flex min-w-32 items-center justify-center gap-2"
                  >
                    <Send className="h-4 w-4" />
                    رد
                  </button>
                </div>
              ) : (
                <p className="text-sm font-bold text-[var(--admin-muted)]">
                  الرد متاح بعد قبول المنشور.
                </p>
              )
            }
            rowActionLabel={(post) => `فتح رد المدرس على منشور ${post.studentName}`}
          />
        ) : (
          <AdminDataTable
            data={comments}
            columns={commentColumns}
            loading={loading}
            rowKey={(comment) => comment.id}
            emptyMessage="لا توجد تعليقات معلقة."
          />
        )}
      </div>
    </TeacherPage>
  );
}

function ActionButton({
  icon: Icon,
  label,
  disabled,
  onClick,
  tone,
}: {
  icon: typeof CheckCircle2;
  label: string;
  disabled: boolean;
  onClick: () => void;
  tone: "success" | "danger";
}) {
  const color =
    tone === "success"
      ? "border-emerald-500/20 bg-emerald-500/10 text-emerald-600"
      : "border-rose-500/20 bg-rose-500/10 text-rose-600";

  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className={`inline-flex items-center gap-2 rounded-full border px-4 py-2 text-xs font-black transition disabled:opacity-50 ${color}`}
    >
      <Icon className="h-4 w-4" />
      {label}
    </button>
  );
}
