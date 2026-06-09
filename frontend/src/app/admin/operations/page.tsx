'use client';

import React, { useEffect, useState, useCallback } from 'react';
import {
  RefreshCw,
  Search,
  Check,
  X as CloseIcon,
  Plus,
  Eye,
} from 'lucide-react';
import {
  AdminShellChrome,
  AdminDataTable,
  AdminColumn
} from '@/components/admin';
import { assistantService, TaskItemDto } from '@/services/assistant-service';
import { hrService, EmployeeDto } from '@/services/hr-service';
import { useAuthStore } from '@/stores/auth-store';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';
import TaskCreateModal from '@/components/assistant/TaskCreateModal';
import TaskDetailsModal from '@/components/assistant/TaskDetailsModal';

export default function AdminOperationsPage() {
  const { user } = useAuthStore();
  const [tasks, setTasks] = useState<TaskItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [employees, setEmployees] = useState<EmployeeDto[]>([]);

  // Modals state
  const [isCreateOpen, setCreateOpen] = useState(false);
  const [selectedTaskId, setSelectedTaskId] = useState<string | null>(null);

  // Filters state
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<number | ''>('');
  const [priorityFilter, setPriorityFilter] = useState<number | ''>('');
  const [assigneeFilter, setAssigneeFilter] = useState<string>('');

  const fetchEmployees = async () => {
    try {
      const data = await hrService.listEmployees();
      setEmployees(data);
    } catch {
      toast.error('تعذر تحميل قائمة الموظفين');
    }
  };

  const fetchTasks = useCallback(async () => {
    setLoading(true);
    try {
      const res = await assistantService.getAdminOperationsTasks({
        search: searchQuery || undefined,
        assigneeId: assigneeFilter || undefined,
        status: statusFilter !== '' ? statusFilter : undefined,
        priority: priorityFilter !== '' ? priorityFilter : undefined,
      });

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
        toast.error(res.data?.message || 'تعذر تحميل المهام');
      }
    } catch {
      toast.error('حدث خطأ أثناء تحميل المهام');
    } finally {
      setLoading(false);
    }
  }, [searchQuery, assigneeFilter, statusFilter, priorityFilter]);

  useEffect(() => {
    fetchEmployees();
  }, []);

  useEffect(() => {
    fetchTasks();
  }, [fetchTasks]);

  const handleResolveDirectly = async (taskId: string, approve: boolean) => {
    try {
      const res = await assistantService.resolveAdminOperationsTaskApproval(taskId, approve);
      if (res.data?.success) {
        toast.success(approve ? 'تم قبول وإغلاق المهمة ✅' : 'تم رفض وإرجاع المهمة للموظف ❌');
        fetchTasks();
      } else {
        toast.error(res.data?.message || 'تعذر معالجة القرار');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'حدث خطأ أثناء معالجة القرار');
    }
  };

  const getPriorityBadge = (priority: number | string) => {
    const p = typeof priority === 'string' ? {
      "Low": 1,
      "Medium": 2,
      "High": 3,
      "Critical": 4
    }[priority] || 2 : priority;
    switch (p) {
      case 1:
        return <span className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-bold bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-400">منخفضة</span>;
      case 2:
        return <span className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-bold bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400">متوسطة</span>;
      case 3:
        return <span className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-bold bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400">عالية</span>;
      case 4:
        return <span className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-bold bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400 animate-pulse">حرجة 🚨</span>;
      default:
        return <span className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-bold bg-gray-100 text-gray-700">غير محددة</span>;
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
        return <span className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-bold bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-400">جديدة</span>;
      case 2:
        return <span className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-bold bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400">قيد التنفيذ</span>;
      case 3:
        return <span className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-bold bg-purple-100 text-purple-700 dark:bg-purple-950/40 dark:text-purple-400">تحت المراجعة</span>;
      case 4:
        return <span className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-bold bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400">مكتملة</span>;
      case 5:
        return <span className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-bold bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-400">متوقفة مؤقتاً</span>;
      case 6:
        return <span className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-bold bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400">متأخرة ⚠️</span>;
      default:
        return <span className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-bold bg-gray-100 text-gray-700">غير معروفة</span>;
    }
  };

  const isUserApprovedManager = user?.roles.includes('Admin') || user?.roles.includes('Supervisor');

  const columns: AdminColumn<TaskItemDto>[] = [
    {
      key: 'title',
      label: 'المهمة',
      render: (t) => (
        <div className="cursor-pointer" onClick={() => setSelectedTaskId(t.id)}>
          <div className="font-bold text-[var(--admin-text)] hover:text-[var(--admin-primary)] transition-colors">
            {t.title}
          </div>
          {t.description && (
            <div className="text-xs text-[var(--admin-muted)] truncate max-w-[240px] mt-0.5">
              {t.description}
            </div>
          )}
        </div>
      ),
    },
    {
      key: 'assignee',
      label: 'المسؤول عنها',
      render: (t) => <span className="text-sm font-semibold">{t.assigneeName}</span>,
    },
    {
      key: 'priority',
      label: 'الأولوية',
      render: (t) => getPriorityBadge(t.priority),
    },
    {
      key: 'status',
      label: 'الحالة',
      render: (t) => getStatusBadge(t.status),
    },
    {
      key: 'dueDate',
      label: 'تاريخ الاستحقاق',
      render: (t) => (
        <span className="font-mono text-xs text-[var(--admin-muted)]">
          {t.dueDate
            ? new Date(t.dueDate).toLocaleDateString('ar-EG', {
                month: 'short',
                day: 'numeric',
                hour: '2-digit',
                minute: '2-digit',
              })
            : '—'}
        </span>
      ),
    },
    {
      key: 'actions',
      label: 'الإجراءات',
      align: 'left',
      render: (t) => (
        <div className="flex items-center justify-end gap-2">
          <NeumorphButton
            type="button"
            onClick={() => setSelectedTaskId(t.id)}
            intent="ghost"
            size="sm"
            className="px-2.5 py-1.5 rounded-xl flex items-center gap-1 font-bold text-xs"
          >
            <Eye className="h-3.5 w-3.5" />
            التفاصيل
          </NeumorphButton>

          {isUserApprovedManager && (t.status === 3 || t.status === 'Review') && (
            <>
              <NeumorphButton
                type="button"
                onClick={() => handleResolveDirectly(t.id, true)}
                intent="primary"
                size="sm"
                className="!bg-emerald-500 !text-white hover:!bg-emerald-600 px-2.5 py-1.5 rounded-xl flex items-center gap-1 font-bold text-xs"
              >
                <Check className="h-3.5 w-3.5" />
                موافقة
              </NeumorphButton>
              <NeumorphButton
                type="button"
                onClick={() => setSelectedTaskId(t.id)} // opens details to fill rejection reason
                intent="danger"
                size="sm"
                className="px-2.5 py-1.5 rounded-xl flex items-center gap-1 font-bold text-xs"
              >
                <CloseIcon className="h-3.5 w-3.5" />
                رفض
              </NeumorphButton>
            </>
          )}
        </div>
      ),
    },
  ];

  return (
    <AdminShellChrome
      activePath="/admin/operations"
      sectionLabel="إدارة العمليات"
      pageTitle="متابعة المهام التشغيلية اليومية"
      subtitle="جدولة وتعيين المهام اليومية للمساعدين والموظفين، ومتابعة معدل التنفيذ وطلبات المراجعة والإغلاق المعلقة."
      action={
        <NeumorphButton intent="primary" size="lg" pill onClick={() => setCreateOpen(true)}>
          <Plus className="h-4 w-4 ml-1" />
          إضافة مهمة جديدة
        </NeumorphButton>
      }
    >
      {/* Search and Filters panel */}
      <div className="mb-6 rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 flex flex-wrap gap-4 items-center justify-between" dir="rtl">
        <div className="flex flex-1 min-w-[240px] items-center gap-2 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2">
          <Search className="h-4 w-4 text-[var(--admin-muted)]" />
          <input
            type="text"
            placeholder="ابحث عن مهمة أو موظف..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full bg-transparent text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none text-right"
          />
        </div>

        <div className="flex flex-wrap items-center gap-3 w-full sm:w-auto">
          {/* Assignee Filter */}
          <select
            value={assigneeFilter}
            onChange={(e) => setAssigneeFilter(e.target.value)}
            className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2 text-xs text-[var(--admin-text)] outline-none"
          >
            <option value="">كل المسؤولين</option>
            {employees.map((emp) => (
              <option key={emp.userId} value={emp.userId}>
                {emp.fullName}
              </option>
            ))}
          </select>

          {/* Status Filter */}
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value !== '' ? Number(e.target.value) : '')}
            className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2 text-xs text-[var(--admin-text)] outline-none"
          >
            <option value="">كل الحالات</option>
            <option value={1}>جديدة</option>
            <option value={2}>قيد التنفيذ</option>
            <option value={3}>تحت المراجعة</option>
            <option value={4}>مكتملة</option>
            <option value={5}>متوقفة مؤقتاً</option>
            <option value={6}>متأخرة</option>
          </select>

          {/* Priority Filter */}
          <select
            value={priorityFilter}
            onChange={(e) => setPriorityFilter(e.target.value !== '' ? Number(e.target.value) : '')}
            className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2 text-xs text-[var(--admin-text)] outline-none"
          >
            <option value="">كل الأولويات</option>
            <option value={1}>منخفضة</option>
            <option value={2}>متوسطة</option>
            <option value={3}>عالية</option>
            <option value={4}>حرجة</option>
          </select>

          <NeumorphButton
            intent="primary"
            size="md"
            onClick={fetchTasks}
            disabled={loading}
            className="flex items-center gap-1.5"
          >
            <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
            تحديث
          </NeumorphButton>
        </div>
      </div>

      {/* Main Datatable */}
      <AdminDataTable
        data={tasks}
        columns={columns}
        loading={loading}
        rowKey={(t) => t.id}
        emptyMessage="لا توجد مهام تشغيلية مطابقة لخيارات البحث."
      />

      {/* Task Creation Modal */}
      <TaskCreateModal
        open={isCreateOpen}
        onClose={() => setCreateOpen(false)}
        onSuccess={fetchTasks}
      />

      {/* Task Details Modal */}
      <TaskDetailsModal
        taskId={selectedTaskId}
        open={selectedTaskId !== null}
        onClose={() => setSelectedTaskId(null)}
        onStatusUpdated={fetchTasks}
        isManager={isUserApprovedManager}
        currentUserId={user?.id}
      />
    </AdminShellChrome>
  );
}
