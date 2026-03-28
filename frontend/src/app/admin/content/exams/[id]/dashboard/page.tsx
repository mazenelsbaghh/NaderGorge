"use client"

import { useEffect, useState, use } from 'react';
import { adminService, ExamDashboardDto, StudentExamResultSummaryDto } from '@/services/admin-service';
import { useRouter } from 'next/navigation';
import { FileText, Clock, BookCheck, Users, AlertCircle } from 'lucide-react';
import { AdminShellChrome, AdminStatCard, AdminDataTable, AdminBackButton } from '@/components/admin';

export default function ExamDashboardPage(props: { params: Promise<{ id: string }> }) {
  const params = use(props.params);
  const examId = params.id;

  const [dashboard, setDashboard] = useState<ExamDashboardDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    if (!examId) return;
    
    adminService.getExamDashboard(examId)
      .then(data => {
        setDashboard(data);
      })
      .catch(() => setError(true))
      .finally(() => setIsLoading(false));
  }, [examId]);

  if (isLoading) {
    return (
      <div className="flex justify-center items-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
      </div>
    );
  }

  if (error || !dashboard) {
    return (
      <div className="p-8 text-center text-red-500">
        <p>حدث خطأ أثناء تحميل بيانات الامتحان</p>
      </div>
    );
  }

  return (
    <AdminShellChrome 
      activePath="/admin/content"
      sectionLabel="إدارة الامتحانات"
      pageTitle="بروفايل الامتحان" 
      subtitle={dashboard.title}
      action={<AdminBackButton />}
    >
      <div className="space-y-8 animate-in fade-in slide-in-from-bottom-4 duration-700">
        
        {/* Description */}
        {dashboard.description && (
          <div className="rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card-soft)]/90 p-8 shadow-sm backdrop-blur-xl">
            <h3 className="text-sm font-bold uppercase tracking-[0.25em] text-[var(--admin-muted)] mb-2">وصف الامتحان</h3>
            <p className="text-[var(--admin-text)] text-lg leading-relaxed">{dashboard.description}</p>
          </div>
        )}

        {/* Stats Grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
          <AdminStatCard 
            icon={BookCheck} 
            label="الدرجة النهائية" 
            value={dashboard.totalScore} 
            variant="accent" 
          />
          <AdminStatCard 
            icon={BookCheck} 
            label="درجة النجاح" 
            value={dashboard.passingScore} 
            variant="light" 
          />
          <AdminStatCard 
            icon={FileText} 
            label="عدد الأسئلة" 
            value={dashboard.questionCount} 
            variant="muted" 
          />
          <AdminStatCard 
            icon={Clock} 
            label="وقت الامتحان" 
            value={dashboard.durationMinutes || 'مفتوح'} 
            subtitle={dashboard.durationMinutes ? "دقيقة" : undefined}
            variant="light" 
          />
        </div>

        {/* Attempts Table */}
        <div className="space-y-4">
          <div className="flex items-center gap-3">
            <div className="rounded-2xl bg-[var(--admin-primary-15)] p-3 text-[var(--admin-primary)]">
              <Users className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-xl font-bold text-[var(--admin-text)]">محاولات الطلاب</h2>
              <p className="text-sm text-[var(--admin-muted)] font-mono mt-1">
                إجمالي المحاولات المسجلة: {dashboard.attempts.length}
              </p>
            </div>
          </div>
          
          <AdminDataTable<StudentExamResultSummaryDto>
            data={dashboard.attempts}
            columns={[
              {
                key: 'student',
                label: 'الطالب',
                render: (row) => (
                  <div>
                    <p className="font-bold text-base text-[var(--admin-text)]">{row.studentName}</p>
                    <p className="text-xs font-mono text-[var(--admin-primary)] mt-1">{row.studentPhone || '---'}</p>
                  </div>
                )
              },
              {
                key: 'time',
                label: 'التوقيت',
                render: (row) => (
                  <div className="space-y-1">
                    <div className="text-xs text-[var(--admin-muted)]">
                      <span className="font-bold inline-block w-12 text-[var(--admin-text)] opacity-70">البدء:</span> 
                      {row.startedAt ? new Date(row.startedAt).toLocaleString('ar-EG', { dateStyle: 'short', timeStyle: 'short' }) : '---'}
                    </div>
                    <div className="text-xs text-[var(--admin-muted)]">
                      <span className="font-bold inline-block w-12 text-[var(--admin-text)] opacity-70">التسليم:</span> 
                      {row.submittedAt ? new Date(row.submittedAt).toLocaleString('ar-EG', { dateStyle: 'short', timeStyle: 'short' }) : '---'}
                    </div>
                  </div>
                )
              },
              {
                key: 'score',
                label: 'النتيجة',
                align: 'center',
                render: (row) => (
                  <div className="font-black text-xl text-[var(--admin-text)]">
                    {row.scoreAchieved.toFixed(1)} <span className="text-sm font-semibold opacity-50">/ {dashboard.totalScore}</span>
                  </div>
                )
              },
              {
                key: 'evaluation',
                label: 'التقييم',
                align: 'center',
                render: (row) => (
                  <span className="font-bold tracking-wide text-[var(--admin-primary)] bg-[var(--admin-primary-15)] px-4 py-1.5 rounded-full inline-block">
                    {row.evaluation}
                  </span>
                )
              },
              {
                key: 'status',
                label: 'الحالة',
                render: (row) => (
                  <div className="flex flex-col gap-2 items-start">
                    <span className={`inline-flex items-center justify-center px-4 py-1.5 rounded-full text-xs font-black uppercase tracking-wider ${
                      row.isPassed ? 'bg-[var(--admin-success-10)] text-[var(--admin-success)]' : 'bg-[var(--admin-danger-10)] text-[var(--admin-danger)]'
                    }`}>
                      {row.isPassed ? 'ناجح' : 'راسب'}
                    </span>
                    {row.isTimeExpired && (
                      <span className="inline-flex items-center justify-center px-3 py-1.5 rounded-full text-[10px] font-black uppercase tracking-wider bg-orange-500/10 text-orange-600" title="نفذ الوقت وتم التسليم تلقائياً">
                        <AlertCircle className="w-3.5 h-3.5 mr-1" /> تأخير
                      </span>
                    )}
                  </div>
                )
              }
            ]}
            rowKey={(r) => r.studentId + (r.startedAt || '')}
            pageSize={15}
            emptyMessage="لا يوجد محاولات لهذا الامتحان حتى الآن."
          />
        </div>
      </div>
    </AdminShellChrome>
  );
}
