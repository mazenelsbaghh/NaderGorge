'use client';

import { useCallback, useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { ArrowRight, BookOpenText, PlaySquare, FileText, ClipboardList, BookCheck, MessageSquareText, Video, Sparkles, Users } from 'lucide-react';
import { AdminPage, AdminStatCard, AdminTabBar, AdminTab, AddVideoForm, LessonVideoList, AddResourceForm, LessonResourceList, UnifiedAssessmentBuilder, AdminPageSkeleton, LessonCommentsModerationTab, EntityOverviewDashboard, AttachedExamViewer, AttachedHomeworkViewer, LessonAIAnalysisTab, ContentArchiveControl, ContentInternalCode, ContentBasicDetailsForm, ContentSubscribersTab } from '@/components/admin';
import type { OverviewStat } from '@/components/admin';
import { adminService, type LessonCockpitDto } from '@/services/admin-service';
import toast from 'react-hot-toast';

type ActiveTab = 'overview' | 'videos' | 'ai-analysis' | 'resources' | 'homework' | 'exam' | 'comments' | 'subscribers';

const TAB_OPTIONS: AdminTab<ActiveTab>[] = [
  { key: 'overview', label: 'نظرة عامة', icon: BookOpenText },
  { key: 'videos', label: 'الفيديوهات', icon: PlaySquare },
  { key: 'ai-analysis', label: 'تحليل AI', icon: Sparkles },
  { key: 'comments', label: 'التعليقات', icon: MessageSquareText },
  { key: 'subscribers', label: 'الطلاب المشتركون', icon: Users },
  { key: 'resources', label: 'المذكرات والملفات', icon: FileText },
  { key: 'homework', label: 'الواجبات', icon: ClipboardList },
  { key: 'exam', label: 'الامتحان المرفق', icon: BookCheck },
];

export default function LessonProfilePageClient(props: { params: { id: string } }) {
  const params = props.params;
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<ActiveTab>('overview');
  const [lesson, setLesson] = useState<LessonCockpitDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [videoView, setVideoView] = useState<'current' | 'archived'>('current');
  const [resourceView, setResourceView] = useState<'current' | 'archived'>('current');

  const loadData = useCallback(async () => {
    try {
      const response = await adminService.getLessonCockpit(params.id);
      setLesson(response.data?.data);
    } catch {
      toast.error('تعذر تحميل تفاصيل الحصة');
    } finally {
      setLoading(false);
    }
  }, [params.id]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  if (loading) {
    return (
      <AdminPage
        activePath="/admin/content"
        sectionLabel="إدارة المحتوى"
        pageTitle="جاري التحميل..."
        subtitle="الرجاء الانتظار"
      >
        <AdminPageSkeleton />
      </AdminPage>
    );
  }

  if (!lesson) {
     return (
        <AdminPage
            activePath="/admin/content"
            sectionLabel="إدارة المحتوى"
            pageTitle="خطأ"
            subtitle="الحصة غير موجودة"
        >
            <div className="p-8 text-center text-[var(--admin-muted)]">
                لا يمكن العثور على الحصة المطلوبة
            </div>
        </AdminPage>
     )
  }

  // Build lesson overview stats from cockpit data
  const videoCount = lesson.videos?.length || 0;
  const resourceCount = lesson.resources?.length || 0;
  const homeworkCount = lesson.homework?.length || 0;
  const pendingComments = lesson.commentsSummary?.pending || 0;
  const totalComments = lesson.commentsSummary?.total || 0;

  const overviewStats: OverviewStat[] = [
    { label: 'الفيديوهات', value: videoCount, icon: Video, tone: videoCount > 0 ? 'success' : 'muted' },
    { label: 'المذكرات والملفات', value: resourceCount, icon: FileText, tone: resourceCount > 0 ? 'muted' : 'muted' },
    { label: 'الواجبات', value: homeworkCount, icon: ClipboardList, tone: homeworkCount > 0 ? 'primary' : 'muted' },
    { label: 'التعليقات', value: `${totalComments}${pendingComments > 0 ? ` (${pendingComments} بانتظار)` : ''}`, icon: MessageSquareText, tone: pendingComments > 0 ? 'warning' : 'muted' },
  ];
  const visibleVideos = (lesson.videos ?? []).filter((item) => videoView === 'archived' ? item.archiveMode !== 'None' : item.archiveMode === 'None');
  const visibleResources = (lesson.resources ?? []).filter((item) => resourceView === 'archived' ? item.archiveMode !== 'None' : item.archiveMode === 'None');

  // Add exam status
  if (lesson.examId) {
    overviewStats.push({ label: 'الامتحان', value: 'مرفق', icon: BookCheck, tone: 'success' });
  }

  return (
    <AdminPage
      activePath="/admin/content"
      sectionLabel="إدارة المحتوى ▸ الحصص"
      pageTitle={lesson.title}
      subtitle={lesson.summary || 'إدارة محتويات وإعدادات الحصة'}
      action={
        <div className="flex flex-wrap items-center gap-2">
          <ContentArchiveControl targetType="Lesson" targetId={lesson.lessonId} title={lesson.title} archiveMode={lesson.archiveMode} onChanged={loadData} />
          <button
            onClick={() => router.back()}
            className="inline-flex min-h-11 w-fit items-center gap-2 rounded-full bg-[var(--admin-card-strong)] px-6 py-3 text-sm font-bold text-[var(--admin-text)] shadow-sm border border-[var(--admin-border)] transition hover:bg-[var(--admin-hover)]"
          >
            <ArrowRight className="h-4 w-4" /> عودة للقائمة
          </button>
        </div>
      }
    >
      <div className="mb-6 flex justify-start">
        <ContentInternalCode code={lesson.internalCode} label="كود الحصة الداخلي" />
      </div>
      <section className="mb-12 grid grid-cols-1 gap-6 md:grid-cols-4">
        <AdminStatCard variant="accent" icon={BookOpenText} label="معرف الحصة" value={lesson.lessonId.split('-')[0]} />
        <AdminStatCard variant="light" icon={PlaySquare} label="الفيديوهات" value={`${lesson.videos?.length || 0}`} />
        <AdminStatCard variant="muted" icon={MessageSquareText} label="تعليقات قيد المراجعة" value={`${lesson.commentsSummary?.pending || 0}`} />
        <AdminStatCard variant="accent" icon={ClipboardList} label="الواجبات" value={`${lesson.homework?.length || 0}`} />
      </section>

      <div className="mb-8">
        <AdminTabBar tabs={TAB_OPTIONS} activeTab={activeTab} onSelect={setActiveTab} />
      </div>

      {activeTab === 'overview' && (
        <EntityOverviewDashboard
          entityType="حصة"
          details={{ title: lesson.title, description: lesson.summary, price: lesson.price }}
          stats={overviewStats}
          loading={false}
          onPriceUpdate={async (newPrice) => {
            await adminService.updateLesson(lesson.lessonId, { title: lesson.title, summary: lesson.summary, order: lesson.order, price: newPrice });
            toast.success('تم تحديث السعر');
            await loadData();
          }}
        >
          <ContentBasicDetailsForm
            title={lesson.title}
            order={lesson.order}
            price={lesson.price}
            summary={lesson.summary}
            summaryLabel="ملخص الحصة"
            onSave={async ({ title, summary, order, price }) => {
              await adminService.updateLesson(lesson.lessonId, {
                title,
                summary: summary ?? '',
                order,
                price,
              });
              await loadData();
            }}
          />

          {/* Quick navigation cards */}
          <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
            <h3 className="mb-4 text-lg font-black text-[var(--admin-text)]">انتقال سريع</h3>
            <div className="flex flex-wrap gap-3">
              <button
                onClick={() => setActiveTab('videos')}
                className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-primary-15)] px-4 py-2 text-sm font-bold text-[var(--admin-primary)] transition-colors hover:bg-[var(--admin-primary)] hover:text-white"
              >
                <PlaySquare className="h-4 w-4" />
                الفيديوهات ({videoCount})
              </button>
              <button
                onClick={() => setActiveTab('comments')}
                className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-bold transition-colors ${pendingComments > 0 ? 'bg-amber-500/10 text-amber-600 hover:bg-amber-500 hover:text-white' : 'bg-[var(--admin-card-strong)] text-[var(--admin-text)] hover:bg-[var(--admin-hover)]'}`}
              >
                <MessageSquareText className="h-4 w-4" />
                التعليقات {pendingComments > 0 ? `(${pendingComments} بانتظار)` : ''}
              </button>
              <button
                onClick={() => setActiveTab('resources')}
                className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-card-strong)] px-4 py-2 text-sm font-bold text-[var(--admin-text)] transition-colors hover:bg-[var(--admin-hover)]"
              >
                <FileText className="h-4 w-4" />
                المذكرات ({resourceCount})
              </button>
              <button
                onClick={() => setActiveTab('homework')}
                className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-card-strong)] px-4 py-2 text-sm font-bold text-[var(--admin-text)] transition-colors hover:bg-[var(--admin-hover)]"
              >
                <ClipboardList className="h-4 w-4" />
                الواجبات ({homeworkCount})
              </button>
              <button
                onClick={() => setActiveTab('exam')}
                className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-card-strong)] px-4 py-2 text-sm font-bold text-[var(--admin-text)] transition-colors hover:bg-[var(--admin-hover)]"
              >
                <BookCheck className="h-4 w-4" />
                {lesson.examId ? 'عرض الامتحان' : 'إنشاء امتحان'}
              </button>
            </div>
          </div>
        </EntityOverviewDashboard>
      )}

      {activeTab === 'videos' && (
        <div className="space-y-6">
          <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
            <h3 className="mb-4 text-xl font-bold text-[var(--admin-text)]">إضافة فيديو جديد</h3>
            <AddVideoForm lessonId={lesson.lessonId} onSuccess={loadData} />
          </div>

          <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
            <h3 className="mb-6 text-xl font-bold text-[var(--admin-text)]">الفيديوهات المرفقة ({lesson.videos?.length || 0})</h3>
            <ArchiveViewTabs current={lesson.videos.filter((item) => item.archiveMode === 'None').length} archived={lesson.videos.filter((item) => item.archiveMode !== 'None').length} value={videoView} onChange={setVideoView} />
            <LessonVideoList videos={visibleVideos} lessonId={lesson.lessonId} onRefresh={loadData} />
          </div>
        </div>
      )}

      {activeTab === 'ai-analysis' && (
        <LessonAIAnalysisTab lessonId={lesson.lessonId} videos={lesson.videos || []} onRefresh={loadData} />
      )}

      {activeTab === 'comments' && (
        <LessonCommentsModerationTab
          lessonId={lesson.lessonId}
          pendingCount={lesson.commentsSummary?.pending || 0}
          onRefresh={loadData}
        />
      )}

      {activeTab === 'subscribers' && (
        <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
          <ContentSubscribersTab contentType="lesson" contentId={lesson.lessonId} contentName={lesson.title} />
        </div>
      )}

      {activeTab === 'resources' && (
        <div className="space-y-6">
          <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
            <h3 className="mb-4 text-xl font-bold text-[var(--admin-text)]">إضافة ملف أو مذكرة</h3>
            <AddResourceForm lessonId={lesson.lessonId} onSuccess={loadData} />
          </div>

          <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
            <h3 className="mb-6 text-xl font-bold text-[var(--admin-text)]">الملفات المرفقة ({lesson.resources?.length || 0})</h3>
            <ArchiveViewTabs current={lesson.resources.filter((item) => item.archiveMode === 'None').length} archived={lesson.resources.filter((item) => item.archiveMode !== 'None').length} value={resourceView} onChange={setResourceView} />
            <LessonResourceList resources={visibleResources} onRefresh={loadData} />
          </div>
        </div>
      )}

      {activeTab === 'homework' && (
        <div className="space-y-6">
          {lesson.homework && lesson.homework.length > 0 ? (
            <AttachedHomeworkViewer homeworkId={lesson.homework[0].id} />
          ) : (
            <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
              <h3 className="mb-4 text-xl font-bold text-[var(--admin-text)]">إضافة واجب جديد</h3>
              <UnifiedAssessmentBuilder type="homework" lessonId={lesson.lessonId} onSuccess={loadData} />
            </div>
          )}
        </div>
      )}

      {activeTab === 'exam' && (
        <div className="space-y-6 animate-in slide-in-from-bottom-2 fade-in">
          {lesson.examId ? (
            <AttachedExamViewer examId={lesson.examId} />
          ) : (
            <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm">
              <h3 className="mb-6 text-xl font-bold text-[var(--admin-text)] flex items-center gap-3">
                <BookCheck className="h-6 w-6 text-[var(--admin-primary)]" />
                إنشاء امتحان الحصة
              </h3>
              <UnifiedAssessmentBuilder type="exam" lessonId={lesson.lessonId} videos={lesson.videos || []} onSuccess={loadData} forceTargetType="Lesson" />
            </div>
          )}

          {/* Show existing video exams */}
          {(lesson.videos || []).some((v: any) => v.exams && v.exams.length > 0) && (
            <div className="space-y-4">
              <h3 className="text-lg font-bold text-[var(--admin-text)] flex items-center gap-2">
                <Video className="h-5 w-5 text-[var(--admin-primary)]" />
                امتحانات الفيديوهات ({(lesson.videos || []).reduce((acc: number, v: any) => acc + (v.exams?.length || 0), 0)})
              </h3>
              {(lesson.videos || []).filter((v: any) => v.exams && v.exams.length > 0).map((v: any) => (
                <div key={v.id} className="space-y-4 p-5 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)]">
                  <p className="text-sm font-bold text-[var(--admin-text)]">فيديو: {v.title}</p>
                  <div className="space-y-4">
                    {v.exams.map((exam: any) => (
                      <div key={exam.examId} className="space-y-2">
                        <p className="text-xs font-bold text-[var(--admin-muted)]">امتحان: {exam.title}</p>
                        <AttachedExamViewer examId={exam.examId} />
                      </div>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Always show video exam builder if videos exist */}
          {(lesson.videos?.length ?? 0) > 0 && (
            <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm">
              <h3 className="mb-2 text-xl font-bold text-[var(--admin-text)] flex items-center gap-3">
                <Video className="h-6 w-6 text-[var(--admin-primary)]" />
                إضافة امتحان فيديو (Pop Quiz)
              </h3>
              <p className="mb-6 text-sm text-[var(--admin-muted)]">
                أضف امتحان يظهر بعد مشاهدة فيديو معين — مستقل عن امتحان الحصة.
              </p>
              <UnifiedAssessmentBuilder type="exam" lessonId={lesson.lessonId} videos={lesson.videos || []} onSuccess={loadData} forceTargetType="Video" />
            </div>
          )}
        </div>
      )}
    </AdminPage>
  );
}

function ArchiveViewTabs({ current, archived, value, onChange }: { current: number; archived: number; value: 'current' | 'archived'; onChange: (value: 'current' | 'archived') => void }) {
  return (
    <div className="mb-5 grid grid-cols-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] p-1" role="tablist" aria-label="حالة المحتوى">
      <button type="button" role="tab" aria-selected={value === 'current'} onClick={() => onChange('current')} className={`min-h-11 rounded-lg text-sm font-black ${value === 'current' ? 'bg-[var(--admin-primary)] text-white' : 'text-[var(--admin-muted)]'}`}>المحتوى الحالي ({current})</button>
      <button type="button" role="tab" aria-selected={value === 'archived'} onClick={() => onChange('archived')} className={`min-h-11 rounded-lg text-sm font-black ${value === 'archived' ? 'bg-amber-700 text-white' : 'text-[var(--admin-muted)]'}`}>المؤرشف ({archived})</button>
    </div>
  );
}
