'use client';

import { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { easeQuart, exitScale, feedbackTransition } from '@/lib/motion';
import { AssistantTaskDto, assistantService } from '@/services/assistant-service';
import toast from 'react-hot-toast';

export function AssistantTaskBoard() {
  const [tasks, setTasks] = useState<AssistantTaskDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  
  const [typeFilter, setTypeFilter] = useState<number | undefined>(undefined);
  const [resolvingTaskId, setResolvingTaskId] = useState<string | null>(null);
  const [resolutionNotes, setResolutionNotes] = useState('');

  const fetchTasks = async () => {
    setLoading(true);
    setError('');
    try {
      const res = await assistantService.getPendingTasks(typeFilter);
      setTasks(res.data.data);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to fetch tasks.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchTasks();
  }, [typeFilter]);

  const handleResolve = async (taskId: string) => {
    if (!resolutionNotes.trim()) {
      toast.error('اكتب ملاحظات الحل قبل التأكيد');
      return;
    }

    try {
      await assistantService.resolveTask(taskId, resolutionNotes);
      setResolvingTaskId(null);
      setResolutionNotes('');
      // Refresh list
      fetchTasks();
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'فشل في حل المهمة');
    }
  };

  const getTaskTypeLabel = (typeNum: number) => {
      switch (typeNum) {
          case 0: return { label: 'Grade Essay', style: 'bg-[var(--admin-primary-15)] text-[var(--admin-primary)]' };
          case 1: return { label: 'Follow-Up At Risk', style: 'bg-[color:rgba(239,68,68,0.1)] text-[#ef4444]' };
          case 2: return { label: 'Payment Issue', style: 'bg-[color:rgba(234,179,8,0.1)] text-[#ca8a04]' };
          default: return { label: 'Unknown', style: 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)]' };
      }
  };

  if (loading && tasks.length === 0) {
      return (
          <div className="flex justify-center items-center h-64">
              <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-[var(--admin-primary)]"></div>
          </div>
      );
  }

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      
      {/* Header and Filters */}
      <div className="flex flex-col sm:flex-row justify-between items-center admin-panel gap-4">
        <div>
           <h1 className="text-2xl font-bold text-[var(--admin-text)]">لوحة مهام المساعد</h1>
           <p className="text-[var(--admin-muted)] mt-1">إدارة وحل مشاكل الطلاب والمهام الأكاديمية.</p>
        </div>
        
        <div className="flex gap-2">
            <button 
                onClick={() => setTypeFilter(undefined)}
                className={`px-4 py-2 rounded-xl text-sm font-medium transition-colors ${typeFilter === undefined ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'}`}
            >
                الكل
            </button>
            <button 
                onClick={() => setTypeFilter(0)}
                className={`px-4 py-2 rounded-xl text-sm font-medium transition-colors ${typeFilter === 0 ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'}`}
            >
                تصحيح
            </button>
            <button 
                onClick={() => setTypeFilter(1)}
                className={`px-4 py-2 rounded-xl text-sm font-medium transition-colors ${typeFilter === 1 ? 'bg-[color:rgba(239,68,68,0.15)] text-[#ef4444]' : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'}`}
            >
                متابعة الطلاب
            </button>
        </div>
      </div>

      {error && (
        <div role="alert" className="rounded-2xl border border-[color:rgba(239,68,68,0.2)] bg-[color:rgba(239,68,68,0.06)] p-4 text-sm font-bold text-[#ef4444]">
            {error}
        </div>
      )}

      {/* Task List */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        <AnimatePresence mode="popLayout">
        {tasks.length === 0 ? (
           <motion.div
             key="empty"
             initial={{ opacity: 0 }}
             animate={{ opacity: 1 }}
             className="col-span-full py-20 text-center"
           >
              <svg className="mx-auto h-12 w-12 text-[var(--admin-muted)] mb-4 opacity-40" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                 <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              <h3 className="text-lg font-medium text-[var(--admin-text)]">لا توجد مهام معلقة!</h3>
              <p className="text-[var(--admin-muted)] mt-1">لا توجد مهام بحاجة للمراجعة حاليًا.</p>
           </motion.div>
        ) : (
          tasks.map((task, i) => {
              const typeInfo = getTaskTypeLabel(task.taskType);
              
              return (
                  <motion.div
                    key={task.id}
                    layout
                    initial={{ opacity: 0, y: 16, scale: 0.97 }}
                    animate={{ opacity: 1, y: 0, scale: 1, transition: { delay: i * 0.06, duration: 0.4, ease: easeQuart } }}
                    exit={exitScale}
                    className="flex flex-col rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-sm hover:shadow-[0_12px_32px_var(--admin-shadow)] transition-shadow"
                  >
                      <div className="p-6 flex-1">
                          <div className="flex justify-between items-start mb-4">
                              <span className={`inline-flex items-center px-3 py-1 rounded-full text-xs font-semibold uppercase tracking-wider ${typeInfo.style}`}>
                                  {typeInfo.label}
                              </span>
                              <span className="text-xs text-[var(--admin-muted)] font-medium">
                                  {new Date(task.createdAt).toLocaleDateString()}
                              </span>
                          </div>
                          
                          <h3 className="text-xl font-bold text-[var(--admin-text)] mb-1">
                              {task.studentName}
                          </h3>
                          <p className="text-sm text-[var(--admin-muted)] font-medium font-mono truncate">
                              Student ID: {task.studentId?.substring(0,8) || 'N/A'}
                          </p>
                          
                          {resolvingTaskId === task.id && (
                              <motion.div
                                initial={{ opacity: 0, height: 0 }}
                                animate={{ opacity: 1, height: 'auto' }}
                                exit={{ opacity: 0, height: 0 }}
                                transition={feedbackTransition}
                                className="mt-6 pt-4 border-t border-[var(--admin-border)] overflow-hidden"
                              >
                                  <label className="block text-sm font-medium text-[var(--admin-text)] mb-2">
                                      ملاحظات الحل
                                  </label>
                                  <textarea
                                      rows={3}
                                      value={resolutionNotes}
                                      onChange={(e) => setResolutionNotes(e.target.value)}
                                      placeholder="ما الإجراء الذي تم اتخاذه؟"
                                      className="admin-input sm:text-sm block"
                                  />
                              </motion.div>
                          )}
                      </div>
                      
                      <div className="px-6 py-4 bg-[var(--admin-card-soft)] rounded-b-[24px] border-t border-[var(--admin-border)] flex justify-end gap-3">
                          {resolvingTaskId === task.id ? (
                              <>
                                  <button
                                      onClick={() => {
                                          setResolvingTaskId(null);
                                          setResolutionNotes('');
                                      }}
                                      className="rounded-xl px-4 py-2 text-sm font-medium text-[var(--admin-muted)] hover:text-[var(--admin-text)] hover:bg-[var(--admin-hover)] transition"
                                  >
                                      إلغاء
                                  </button>
                                  <button
                                      onClick={() => handleResolve(task.id)}
                                      className="admin-btn-primary text-sm"
                                  >
                                      تأكيد الحل
                                  </button>
                              </>
                          ) : (
                              <button
                                  onClick={() => setResolvingTaskId(task.id)}
                                  className="admin-btn-ghost"
                              >
                                  حل المهمة
                              </button>
                          )}
                      </div>
                  </motion.div>
              );
          })
        )}
        </AnimatePresence>
      </div>

    </div>
  );
}
