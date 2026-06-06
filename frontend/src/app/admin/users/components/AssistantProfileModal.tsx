'use client';

import { useEffect, useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { X, Shield, Activity, Calendar, User, Loader2, Database } from 'lucide-react';
import { adminService, UserAuditLogDto, AdminUserListDto } from '@/services/admin-service';
import { getInitials, formatRelativeDate } from '@/components/admin/admin-utils';
import toast from 'react-hot-toast';

interface AssistantProfileModalProps {
  open: boolean;
  onClose: () => void;
  assistant: AdminUserListDto | null;
}

function translateAction(action: string): string {
  const map: Record<string, string> = {
    AdjustBalance: 'تعديل رصيد الطالب',
    OverrideVideoLimit: 'تجاوز حد مشاهدة الفيديو',
    ToggleStudentSystemAccess: 'تعديل صلاحية وصول الطالب',
    ResetWatchLimit: 'إعادة تعيين حد مشاهدة الفيديو',
    AdjustGamificationPoints: 'تعديل نقاط الطالب',
    ApproveWatchRequest: 'الموافقة على طلب مشاهدة إضافية',
    AddStudentNote: 'إضافة ملاحظة للطالب',
    DeleteStudentNote: 'حذف ملاحظة الطالب',
    UpdateStudentProfile: 'تحديث الملف الشخصي لطالب',
    DisconnectStudentDevice: 'فصل جهاز الطالب',
    RemoveDevice: 'حذف جهاز مسجل',
    CreateUser: 'إنشاء مستخدم جديد',
    UpdateUserStatus: 'تحديث حالة المستخدم',
    UpdateUserRoles: 'تحديث أدوار المستخدم',
  };
  return map[action] || action;
}

function renderChangedValues(oldVal?: string, newVal?: string) {
  try {
    if (!oldVal && !newVal) return null;
    const oldObj = oldVal ? JSON.parse(oldVal) : null;
    const newObj = newVal ? JSON.parse(newVal) : null;

    const keys = Array.from(new Set([...Object.keys(oldObj || {}), ...Object.keys(newObj || {})]));
    const changes = keys.filter(key => {
      const o = oldObj ? oldObj[key] : undefined;
      const n = newObj ? newObj[key] : undefined;
      return o !== n;
    });

    if (changes.length === 0) return null;

    return (
      <div className="mt-2 space-y-1 text-xs text-[var(--admin-muted)] bg-[var(--admin-hover)]/30 px-3 py-2 rounded-xl border border-[var(--admin-border)]/5 outline-none">
        {changes.map(key => {
          const o = oldObj ? oldObj[key] : undefined;
          const n = newObj ? newObj[key] : undefined;
          return (
            <div key={key} className="flex flex-wrap items-center gap-1.5">
              <span className="font-bold text-[var(--admin-text)]">{key}:</span>
              {o !== undefined && <span className="line-through text-red-500/80">{String(o)}</span>}
              {o !== undefined && <span className="opacity-40">←</span>}
              {n !== undefined && <span className="text-emerald-500 font-bold">{String(n)}</span>}
            </div>
          );
        })}
      </div>
    );
  } catch {
    return null;
  }
}

export function AssistantProfileModal({ open, onClose, assistant }: AssistantProfileModalProps) {
  const [logs, setLogs] = useState<UserAuditLogDto[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (open && assistant) {
      setLoading(true);
      adminService.getUserAuditLogs(assistant.id)
        .then(data => {
          setLogs(data || []);
        })
        .catch(err => {
          console.error("Failed to load audit logs", err);
          toast.error("فشل في تحميل سجل النشاطات");
        })
        .finally(() => {
          setLoading(false);
        });
    } else {
      setLogs([]);
    }
  }, [open, assistant]);

  return (
    <AnimatePresence>
      {open && assistant && (
        <>
          {/* Backdrop */}
          <motion.div
            key="backdrop"
            initial={{ opacity: 0 }}
            animate={{ opacity: 0.4 }}
            exit={{ opacity: 0 }}
            onClick={onClose}
            className="fixed inset-0 z-[90] bg-black/60 backdrop-blur-sm"
          />

          {/* Modal */}
          <motion.div
            key="modal"
            initial={{ opacity: 0, scale: 0.96, y: 18 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.98, y: 12 }}
            transition={{ duration: 0.2, ease: [0.22, 1, 0.36, 1] }}
            className="fixed inset-0 z-[100] flex items-center justify-center p-4 sm:p-6"
            dir="rtl"
            role="dialog"
            aria-modal="true"
            aria-labelledby="assistant-profile-title"
          >
            <div className="flex max-h-[min(880px,calc(100dvh-2rem))] w-full max-w-4xl flex-col overflow-hidden rounded-[32px] border border-[var(--admin-border)] bg-[var(--admin-bg)] shadow-2xl">
              
              {/* Header */}
              <div className="flex shrink-0 items-center justify-between border-b border-[var(--admin-border)] bg-[var(--admin-card)] px-6 py-5">
                <div className="flex items-center gap-4">
                  <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)] font-bold text-lg shadow-sm">
                    {getInitials(assistant.fullName)}
                  </div>
                  <div>
                    <h2
                      id="assistant-profile-title"
                      className="text-lg font-black text-[var(--admin-text)] tracking-tight"
                    >
                      {assistant.fullName}
                    </h2>
                    <p className="text-xs text-[var(--admin-muted)] mt-0.5">الملف التعريفي للتدقيق وسجل العمليات</p>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={onClose}
                  aria-label="إغلاق الملف التعريفي"
                  className="flex h-9 w-9 items-center justify-center rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] hover:text-[var(--admin-text)]"
                >
                  <X className="h-4 w-4" />
                </button>
              </div>

              {/* Scrollable Content */}
              <div className="min-h-0 flex-1 overflow-y-auto p-6 space-y-6">
                
                {/* Info Block */}
                <div className="grid grid-cols-1 md:grid-cols-3 gap-5 bg-[var(--admin-card)] p-5 rounded-3xl">
                  <div className="flex items-center gap-3">
                    <User className="h-5 w-5 text-[var(--admin-primary)]" />
                    <div>
                      <p className="text-xs text-[var(--admin-muted)]">اسم المستخدم / الهاتف</p>
                      <p className="text-sm font-bold text-[var(--admin-text)] font-mono">{assistant.phoneNumber}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    <Shield className="h-5 w-5 text-[var(--admin-primary)]" />
                    <div>
                      <p className="text-xs text-[var(--admin-muted)]">الدور والصلاحيات</p>
                      <p className="text-sm font-bold text-[var(--admin-text)]">
                        {assistant.roles.join('، ') || 'مساعد تعليمي'}
                      </p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    <Calendar className="h-5 w-5 text-[var(--admin-primary)]" />
                    <div>
                      <p className="text-xs text-[var(--admin-muted)]">تاريخ الإنشاء</p>
                      <p className="text-sm font-bold text-[var(--admin-text)]">
                        {new Date(assistant.createdAt).toLocaleDateString('ar-EG', { dateStyle: 'medium' })}
                      </p>
                    </div>
                  </div>
                </div>

                {/* Audit logs Timeline Section */}
                <div>
                  <h3 className="text-sm font-black text-[var(--admin-text)] mb-4 flex items-center gap-2">
                    <Activity className="h-4 w-4 text-[var(--admin-primary)]" />
                    سجل النشاطات والأحداث
                  </h3>

                  {loading ? (
                    <div className="flex flex-col items-center justify-center py-20 text-[var(--admin-muted)] gap-3">
                      <Loader2 className="h-8 w-8 animate-spin text-[var(--admin-primary)]" />
                      <p className="text-sm font-bold">جاري تحميل سجل النشاطات...</p>
                    </div>
                  ) : logs.length === 0 ? (
                    <div className="flex flex-col items-center justify-center py-20 text-[var(--admin-muted)] bg-[var(--admin-card)]/50 rounded-3xl">
                      <Database className="h-10 w-10 mb-3 opacity-40 text-[var(--admin-primary)]" />
                      <p className="text-sm font-bold">لا يوجد أي نشاطات مسجلة لهذا الحساب حالياً.</p>
                    </div>
                  ) : (
                    <div className="relative border-r border-[var(--admin-border)]/60 mr-3 pr-6 space-y-6">
                      {logs.map((log) => (
                        <div key={log.id} className="relative group">
                          {/* Timeline node */}
                          <div className="absolute right-[-31px] top-1.5 flex h-4.5 w-4.5 items-center justify-center rounded-full border-2 border-[var(--admin-bg)] bg-[var(--admin-primary)] ring-4 ring-[var(--admin-primary-15)]" />
                          
                          <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-1.5">
                            <div>
                              <p className="text-sm font-bold text-[var(--admin-text)]">
                                {translateAction(log.action)}
                              </p>
                              <p className="text-xs text-[var(--admin-muted)] mt-0.5">
                                الكيان المتأثر: <span className="font-mono text-[var(--admin-text)]">{log.entityType}</span>
                                {log.ipAddress && <> | عنوان الـ IP: <span className="font-mono">{log.ipAddress}</span></>}
                              </p>
                            </div>
                            <span className="text-xs text-[var(--admin-muted)] font-medium shrink-0">
                              {formatRelativeDate(log.createdAt)}
                            </span>
                          </div>

                          {/* Changed values visualization */}
                          {renderChangedValues(log.oldValues, log.newValues)}
                        </div>
                      ))}
                    </div>
                  )}
                </div>

              </div>

              {/* Footer */}
              <div className="shrink-0 border-t border-[var(--admin-border)] bg-[var(--admin-card)] px-6 py-4">
                <button
                  type="button"
                  onClick={onClose}
                  className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] py-3 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)]"
                >
                  إغلاق
                </button>
              </div>

            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
