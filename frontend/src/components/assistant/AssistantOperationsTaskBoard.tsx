'use client';

import React, { useEffect, useState, useCallback } from 'react';
import { assistantService, TaskItemDto } from '@/services/assistant-service';
import { useAuthStore } from '@/stores/auth-store';
import { RefreshCw, Search, Clock, AlertTriangle } from 'lucide-react';
import NeumorphButton from '@/components/ui/neumorph-button';
import TaskDetailsModal from '@/components/assistant/TaskDetailsModal';
import toast from 'react-hot-toast';

export function AssistantOperationsTaskBoard() {
  const { user } = useAuthStore();
  const [tasks, setTasks] = useState<TaskItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedTaskId, setSelectedTaskId] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');

  const fetchTasks = useCallback(async () => {
    setLoading(true);
    try {
      const res = await assistantService.getMyOperationsTasks();
      if (res.data?.success) {
        const statusMap: Record<string, number> = {
          "New": 1,
          "InProgress": 2,
          "Review": 3,
          "Completed": 4,
          "Paused": 5,
          "Overdue": 6
        };
        const priorityMap: Record<string, number> = {
          "Low": 1,
          "Medium": 2,
          "High": 3,
          "Critical": 4
        };
        const normalized = res.data.data.map(t => ({
          ...t,
          status: typeof t.status === 'string' ? statusMap[t.status] || 1 : t.status,
          priority: typeof t.priority === 'string' ? priorityMap[t.priority] || 2 : t.priority
        }));
        setTasks(normalized);
      } else {
        toast.error(res.data?.message || 'تعذر تحميل المهام التشغيلية');
      }
    } catch {
      toast.error('حدث خطأ أثناء تحميل المهام التشغيلية');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchTasks();
  }, [fetchTasks]);

  const getPriorityBadge = (priority: number | string) => {
    const p = typeof priority === 'string' ? {
      "Low": 1,
      "Medium": 2,
      "High": 3,
      "Critical": 4
    }[priority] || 2 : priority;
    switch (p) {
      case 1:
        return <span className="inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-bold bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-400">منخفضة</span>;
      case 2:
        return <span className="inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-bold bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400">متوسطة</span>;
      case 3:
        return <span className="inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-bold bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400">عالية</span>;
      case 4:
        return <span className="inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-bold bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400 animate-pulse">حرجة 🚨</span>;
      default:
        return <span className="inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-bold bg-gray-100 text-gray-700">غير محددة</span>;
    }
  };

  const getStatusBadge = (status: number | string) => {
    const s = typeof status === 'string' ? {
      "New": 1,
      "InProgress": 2,
      "Review": 3,
      "Completed": 4,
      "Paused": 5,
      "Overdue": 6
    }[status] || 1 : status;
    switch (s) {
      case 1:
        return <span className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-bold bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-400">جديدة</span>;
      case 2:
        return <span className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-bold bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400">قيد التنفيذ</span>;
      case 3:
        return <span className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-bold bg-purple-100 text-purple-700 dark:bg-purple-950/40 dark:text-purple-400">تحت المراجعة</span>;
      case 4:
        return <span className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-bold bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400">مكتملة</span>;
      case 5:
        return <span className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-bold bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-400">متوقفة مؤقتاً</span>;
      case 6:
        return <span className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-bold bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400">متأخرة ⚠️</span>;
      default:
        return <span className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-bold bg-gray-100 text-gray-700">غير معروفة</span>;
    }
  };

  const filteredTasks = tasks.filter(t =>
    t.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
    (t.description && t.description.toLowerCase().includes(searchQuery.toLowerCase()))
  );

  return (
    <div className="space-y-6 text-right" dir="rtl">
      {/* Top Filter and Search Bar */}
      <div className="flex flex-col sm:flex-row gap-4 justify-between items-center bg-[var(--admin-card-soft)] p-4 rounded-3xl border border-[var(--admin-border)]">
        <div className="flex flex-1 w-full min-w-[280px] items-center gap-2 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2">
          <Search className="h-4 w-4 text-[var(--admin-muted)]" />
          <input
            type="text"
            placeholder="ابحث في مهامك التشغيلية..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full bg-transparent text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none text-right"
          />
        </div>
        <NeumorphButton
          intent="primary"
          size="md"
          onClick={fetchTasks}
          disabled={loading}
          className="flex items-center gap-1.5 w-full sm:w-auto"
        >
          <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
          تحديث المهام
        </NeumorphButton>
      </div>

      {/* Grid of task cards */}
      {loading && filteredTasks.length === 0 ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {[1, 2, 3].map((i) => (
            <div key={i} className="animate-pulse bg-[var(--admin-card)] rounded-[2rem] h-[180px] border border-[var(--admin-border)]" />
          ))}
        </div>
      ) : filteredTasks.length === 0 ? (
        <div className="py-16 text-center border border-[var(--admin-border)] rounded-3xl bg-[var(--admin-card-soft)]">
          <AlertTriangle className="mx-auto h-12 w-12 text-[var(--admin-muted)] mb-3 opacity-40" />
          <h3 className="text-lg font-bold text-[var(--admin-text)]">لا توجد مهام تشغيلية مسندة إليك!</h3>
          <p className="text-sm text-[var(--admin-muted)] mt-1">عند تكليفك بمهمة جديدة من الإدارة، ستظهر هنا فوراً.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {filteredTasks.map((task) => (
            <div
              key={task.id}
              onClick={() => setSelectedTaskId(task.id)}
              className="group cursor-pointer flex flex-col rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm hover:shadow-[0_12px_28px_var(--admin-shadow)] transition-all duration-200"
            >
              <div className="flex justify-between items-center mb-3">
                {getPriorityBadge(task.priority)}
                <span className="text-xs text-[var(--admin-muted)] font-mono flex items-center gap-1">
                  <Clock className="h-3 w-3" />
                  {task.dueDate ? new Date(task.dueDate).toLocaleDateString('ar-EG') : 'بدون تاريخ'}
                </span>
              </div>

              <h3 className="text-lg font-bold text-[var(--admin-text)] group-hover:text-[var(--admin-primary)] transition-colors line-clamp-1">
                {task.title}
              </h3>

              {task.description && (
                <p className="text-xs text-[var(--admin-muted)] mt-1.5 line-clamp-2 leading-relaxed">
                  {task.description}
                </p>
              )}

              <div className="mt-6 pt-4 border-t border-[var(--admin-border)] flex items-center justify-between">
                {getStatusBadge(task.status)}
                <span className="text-xs text-[var(--admin-muted)]">تعيين بواسطة: {task.createdByName}</span>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Task Details Modal */}
      <TaskDetailsModal
        taskId={selectedTaskId}
        open={selectedTaskId !== null}
        onClose={() => setSelectedTaskId(null)}
        onStatusUpdated={fetchTasks}
        isManager={false} // Assistants cannot approve tasks
        currentUserId={user?.id}
      />
    </div>
  );
}
