'use client';

import React, { useState, useEffect } from 'react';
import { adminService, type EssaySubmissionDto } from '@/services/admin-service';
import toast from 'react-hot-toast';
import { Loader2, CheckCircle2 } from 'lucide-react';
import NeumorphButton from '@/components/ui/neumorph-button';
import { sanitizeRichHtml } from '@/lib/sanitize-html';

export function EssayGradingView() {
  const [essays, setEssays] = useState<EssaySubmissionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [gradingId, setGradingId] = useState<string | null>(null);

  useEffect(() => {
    fetchPending();
  }, []);

  const fetchPending = async () => {
    try {
      setLoading(true);
      const data = await adminService.getPendingEssays();
      setEssays(data || []);
    } catch {
      toast.error('Failed to fetch pending essays');
    } finally {
      setLoading(false);
    }
  };

  if (loading) return <div className="flex justify-center p-8"><Loader2 className="animate-spin text-[var(--admin-primary)] w-8 h-8" /></div>;
  if (!essays.length) return <div className="text-center p-8 text-[var(--admin-muted)]">No essays pending review.</div>;

  return (
    <div className="space-y-6">
      <h2 className="text-2xl font-black text-[var(--admin-text)]">مراجعة المقالات</h2>
      {essays.map(essay => (
        <EssayCard key={essay.id} essay={essay} onGraded={fetchPending} gradingId={gradingId} setGradingId={setGradingId} />
      ))}
    </div>
  );
}

function EssayCard({ essay, onGraded, gradingId, setGradingId }: { 
  essay: EssaySubmissionDto; 
  onGraded: () => void;
  gradingId: string | null;
  setGradingId: (id: string | null) => void;
}) {
  const [teacherScore, setTeacherScore] = useState(essay.aiInitialScore ?? 0);
  const [teacherFeedback, setTeacherFeedback] = useState(essay.aiFeedback ?? '');
  const isSubmitting = gradingId === essay.id;

  const handleGrade = async () => {
    if (teacherScore < 0) return toast.error('Score must be positive');
    try {
      setGradingId(essay.id);
      await adminService.gradeEssay(essay.id, teacherScore, teacherFeedback);
      toast.success('Essay graded successfully');
      onGraded();
    } catch {
      toast.error('Failed to submit grade');
    } finally {
      if (gradingId === essay.id) setGradingId(null);
    }
  };

  return (
    <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <div className="space-y-4">
          <div>
             <span className="text-xs font-bold uppercase tracking-wider text-[var(--admin-muted)]">إجابة الطالب</span>
             <div className="mt-2 text-sm leading-7 text-[var(--admin-text)] bg-[var(--admin-card-soft)] p-4 rounded-xl border border-[var(--admin-border)] min-h-[100px] whitespace-pre-wrap" dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(essay.answerText) }} />
          </div>
          {essay.status === 'WaitAI' && (
            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-500/10 text-blue-500 text-xs font-bold">
               <Loader2 className="w-4 h-4 animate-spin" /> جاري التقييم بواسطة الذكاء الاصطناعي...
            </div>
          )}
          {essay.status === 'AIScored' && (
            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-indigo-500/10 text-indigo-500 text-xs font-bold">
               تم استلام تقييم الذكاء الاصطناعي
            </div>
          )}
          {essay.status === 'WaitTeacher' && (
            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-emerald-500/10 text-emerald-600 text-xs font-bold">
               جاهز لمراجعة المعلم
            </div>
          )}
          {essay.aiFeedback && (
            <div>
              <span className="text-xs font-bold uppercase tracking-wider text-[var(--admin-muted)]">تقييم الذكاء الاصطناعي (مبدئي: {essay.aiInitialScore} نقطة)</span>
              <p className="mt-2 text-sm leading-7 text-[var(--admin-text)] bg-indigo-500/5 p-4 rounded-xl border border-indigo-500/20 min-h-[100px] whitespace-pre-wrap">
                {essay.aiFeedback}
              </p>
            </div>
          )}
        </div>

        <div className="space-y-4 pt-4 md:pt-0 md:pl-6 md:border-l border-[var(--admin-border)]">
          <h3 className="text-sm font-black text-[var(--admin-text)] mb-4">التقييم النهائي للمعلم</h3>
          
          <div className="space-y-1">
             <label className="text-xs font-bold text-[var(--admin-muted)]">الدرجة</label>
             <input type="number" step="0.5" min={0} value={teacherScore} onChange={e => setTeacherScore(Number(e.target.value))} className="admin-input w-full" />
          </div>

          <div className="space-y-1">
             <label className="text-xs font-bold text-[var(--admin-muted)]">ملاحظات توجيهية (تظهر للطالب)</label>
             <textarea rows={4} value={teacherFeedback} onChange={e => setTeacherFeedback(e.target.value)} className="admin-input w-full" placeholder="ملاحظات المعلم..." />
          </div>

          <NeumorphButton onClick={handleGrade} loading={isSubmitting} intent="primary" size="lg" className="w-full mt-4" pill>
             <CheckCircle2 className="w-4 h-4 mr-2" /> اعتماد التقييم 
          </NeumorphButton>
        </div>
      </div>
    </div>
  );
}
