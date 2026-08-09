'use client';

import React, { useEffect, useState, useCallback } from 'react';
import { assistantService, TaskItemDto } from '@/services/assistant-service';
import { useAuthStore } from '@/stores/auth-store';
import { RefreshCw, Search, Clock, AlertTriangle } from 'lucide-react';
import NeumorphButton from '@/components/ui/neumorph-button';
import TaskDetailsModal from '@/components/assistant/TaskDetailsModal';
import toast from 'react-hot-toast';
import { registerCacheStore } from '@/lib/cache-invalidation';
import { formatCairoDateTime } from '@/lib/cairo-time';

export function AssistantOperationsTaskBoard() {
  const { user } = useAuthStore();
  const [tasks, setTasks] = useState<TaskItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [selectedTaskId, setSelectedTaskId] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');

  const fetchTasks = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
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
        const message = res.data?.message || 'تعذر تحميل المهام التشغيلية';
        setLoadError(message);
        toast.error(message);
      }
    } catch {
      setLoadError('تعذر الاتصال وتحميل المهام التشغيلية.');
      toast.error('حدث خطأ أثناء تحميل المهام التشغيلية');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchTasks();
  }, [fetchTasks]);

  useEffect(() => {
    const cleanupTasksCache = registerCacheStore('operations:tasks', () => {}, () => void fetchTasks());
    const cleanupDashboardCache = registerCacheStore('operations:dashboard', () => {}, () => void fetchTasks());
    return () => {
      cleanupTasksCache();
      cleanupDashboardCache();
    };
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
      <div className="flex flex-col gap-4 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 sm:flex-row sm:items-center sm:justify-between">
        <label className="flex min-w-0 w-full flex-1 items-center gap-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2">
          <Search className="h-4 w-4 text-[var(--admin-muted)]" />
          <span className="sr-only">البحث في المهام التشغيلية</span>
          <input
            type="text"
            placeholder="ابحث في مهامك التشغيلية..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            aria-label="البحث في المهام التشغيلية"
            className="min-w-0 w-full bg-transparent text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none text-right"
          />
        </label>
        <NeumorphButton
          intent="primary"
          size="md"
          onClick={fetchTasks}
          disabled={loading}
          className="flex w-full shrink-0 items-center gap-1.5 sm:w-auto"
        >
          <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin motion-reduce:animate-none' : ''}`} />
          تحديث المهام
        </NeumorphButton>
      </div>

      {/* Grid of task cards */}
      {loadError && filteredTasks.length === 0 ? (
        <div role="alert" className="rounded-2xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-6 text-center text-[var(--admin-danger)]">
          <AlertTriangle className="mx-auto mb-3 h-8 w-8" aria-hidden="true" />
          <p className="font-bold">{loadError}</p>
          <button type="button" onClick={() => void fetchTasks()} className="admin-btn-secondary mt-4 min-h-11">إعادة المحاولة</button>
        </div>
      ) : loading && filteredTasks.length === 0 ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {[1, 2, 3].map((i) => (
            <div key={i} className="h-[180px] animate-pulse rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] motion-reduce:animate-none" />
          ))}
        </div>
      ) : filteredTasks.length === 0 ? (
        <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] py-16 text-center">
          <AlertTriangle className="mx-auto h-12 w-12 text-[var(--admin-muted)] mb-3 opacity-40" />
          <h3 className="text-lg font-bold text-[var(--admin-text)]">لا توجد مهام تشغيلية مسندة إليك!</h3>
          <p className="text-sm text-[var(--admin-muted)] mt-1">عند تكليفك بمهمة جديدة من الإدارة، ستظهر هنا فوراً.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {filteredTasks.map((task) => (
            <button
              type="button"
              key={task.id}
              onClick={() => setSelectedTaskId(task.id)}
              className="group flex w-full flex-col rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 text-right transition-colors duration-200 hover:border-[var(--admin-primary)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2"
              aria-label={`فتح تفاصيل المهمة: ${task.title}`}
            >
              <div className="flex justify-between items-center mb-3">
                {getPriorityBadge(task.priority)}
                <span className="text-xs text-[var(--admin-muted)] font-mono flex items-center gap-1">
                  <Clock className="h-3 w-3" />
                  {task.dueDate ? formatCairoDateTime(task.dueDate) : 'بدون تاريخ'}
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
            </button>
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
