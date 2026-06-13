'use client';

import React, { useEffect, useId, useState } from 'react';
import { AdminModal } from '@/components/admin/AdminModal';
import NeumorphButton from '@/components/ui/neumorph-button';
import { hrService, EmployeeDto } from '@/services/hr-service';
import { assistantService } from '@/services/assistant-service';
import toast from 'react-hot-toast';
import { Dropdown } from '@/components/ui/dropdown';

interface TaskCreateModalProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export default function TaskCreateModal({ open, onClose, onSuccess }: TaskCreateModalProps) {
  const [employees, setEmployees] = useState<EmployeeDto[]>([]);
  const [loadingEmployees, setLoadingEmployees] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  // Form states
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [assigneeId, setAssigneeId] = useState('');
  const [priority, setPriority] = useState<number>(2); // Default to Medium (2)
  const [dueDate, setDueDate] = useState('');
  const titleId = useId();
  const descriptionId = useId();
  const dueDateId = useId();

  useEffect(() => {
    if (open) {
      setLoadingEmployees(true);
      hrService.listEmployees()
        .then((data) => {
          setEmployees(data);
          if (data.length > 0) {
            setAssigneeId(data[0].userId);
          }
        })
        .catch(() => {
          toast.error('تعذر تحميل قائمة الموظفين');
        })
        .finally(() => {
          setLoadingEmployees(false);
        });

      // Reset form
      setTitle('');
      setDescription('');
      setPriority(2);
      setDueDate('');
    }
  }, [open]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim()) {
      toast.error('يرجى إدخال عنوان المهمة');
      return;
    }
    if (!assigneeId) {
      toast.error('يرجى اختيار المسؤول عن المهمة');
      return;
    }

    setSubmitting(true);
    try {
      const formattedDueDate = dueDate ? new Date(dueDate).toISOString() : undefined;
      const res = await assistantService.createAdminOperationsTask({
        title,
        description,
        assigneeId,
        priority,
        dueDate: formattedDueDate
      });

      if (res.data?.success) {
        toast.success('تم إنشاء المهمة بنجاح ✅');
        onSuccess();
        onClose();
      } else {
        toast.error(res.data?.message || 'تعذر إنشاء المهمة');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'حدث خطأ أثناء إنشاء المهمة');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AdminModal open={open} onClose={onClose} title="إنشاء مهمة جديدة" subtitle="قم بتعبئة تفاصيل المهمة وتعيينها لأحد الموظفين.">
      <form onSubmit={handleSubmit} className="space-y-4 text-right" dir="rtl">
        <div>
          <label htmlFor={titleId} className="block text-sm font-bold text-[var(--admin-text)] mb-1.5">عنوان المهمة *</label>
          <input
            id={titleId}
            type="text"
            required
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="مثال: مراجعة الدفعة المالية الثالثة"
            className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2.5 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)]"
          />
        </div>

        <div>
          <label htmlFor={descriptionId} className="block text-sm font-bold text-[var(--admin-text)] mb-1.5">وصف المهمة</label>
          <textarea
            id={descriptionId}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={3}
            placeholder="أدخل تفاصيل ومطلوبات المهمة هنا..."
            className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2.5 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)]"
          />
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <Dropdown
              label="تعيين إلى *"
              value={assigneeId}
              onChange={(v) => setAssigneeId(v as string)}
              disabled={loadingEmployees}
              searchable
              placeholder={loadingEmployees ? 'جاري تحميل الموظفين...' : 'اختر الموظف...'}
              options={employees.map((emp) => ({
                value: emp.userId,
                label: `${emp.fullName} (${emp.roles.join(', ')})`,
              }))}
            />
          </div>

          <div>
            <Dropdown
              label="الأولوية"
              value={String(priority)}
              onChange={(v) => setPriority(Number(v))}
              options={[
                { value: '1', label: 'منخفضة (Low)' },
                { value: '2', label: 'متوسطة (Medium)' },
                { value: '3', label: 'عالية (High)' },
                { value: '4', label: 'حرجة (Critical)' },
              ]}
            />
          </div>
        </div>

        <div>
          <label htmlFor={dueDateId} className="block text-sm font-bold text-[var(--admin-text)] mb-1.5">تاريخ الاستحقاق (اختياري)</label>
          <input
            id={dueDateId}
            type="datetime-local"
            value={dueDate}
            onChange={(e) => setDueDate(e.target.value)}
            className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2.5 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)]"
          />
        </div>

        <div className="flex justify-end gap-3 pt-4 border-t border-[var(--admin-border)]">
          <NeumorphButton
            type="button"
            onClick={onClose}
            intent="danger"
            size="md"
            className="px-6 py-2 rounded-xl text-sm font-bold"
          >
            إلغاء
          </NeumorphButton>
          <NeumorphButton
            type="submit"
            intent="primary"
            size="md"
            disabled={submitting}
            className="px-6 py-2 rounded-xl text-sm font-bold"
          >
            {submitting ? 'جاري الحفظ...' : 'حفظ المهمة'}
          </NeumorphButton>
        </div>
      </form>
    </AdminModal>
  );
}
