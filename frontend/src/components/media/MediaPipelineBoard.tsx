'use client';

import React, { useEffect, useState, useCallback } from 'react';
import { 
  Plus, 
  FolderOpen, 
  AlertTriangle, 
  User, 
  ArrowLeft, 
  Edit, 
  ExternalLink,
  ChevronRight,
  UserCheck
} from 'lucide-react';
import toast from 'react-hot-toast';
import { 
  mediaService, 
  MediaPipelineDto, 
  MediaStage 
} from '@/services/media-service';
import { hrService, EmployeeDto } from '@/services/hr-service';
import NeumorphButton from '@/components/ui/neumorph-button';

const STAGES: { key: MediaStage; label: string; color: string; bg: string; border: string }[] = [
  { key: 'Preparation', label: 'تحضير', color: 'text-slate-600 dark:text-slate-300', bg: 'bg-slate-100/50 dark:bg-slate-900/40', border: 'border-slate-300/45 dark:border-slate-700/45' },
  { key: 'Filming', label: 'تصوير', color: 'text-blue-600 dark:text-blue-400', bg: 'bg-blue-100/50 dark:bg-blue-900/40', border: 'border-blue-300/45 dark:border-blue-700/45' },
  { key: 'Editing', label: 'مونتاج', color: 'text-amber-600 dark:text-amber-400', bg: 'bg-amber-100/50 dark:bg-amber-900/40', border: 'border-amber-300/45 dark:border-amber-700/45' },
  { key: 'Uploading', label: 'رفع', color: 'text-purple-600 dark:text-purple-400', bg: 'bg-purple-100/50 dark:bg-purple-900/40', border: 'border-purple-300/45 dark:border-purple-700/45' },
  { key: 'Review', label: 'مراجعة', color: 'text-rose-600 dark:text-rose-400', bg: 'bg-rose-100/50 dark:bg-rose-900/40', border: 'border-rose-300/45 dark:border-rose-700/45' },
  { key: 'Approved', label: 'مقبول', color: 'text-emerald-600 dark:text-emerald-400', bg: 'bg-emerald-100/50 dark:bg-emerald-900/40', border: 'border-emerald-300/45 dark:border-emerald-700/45' },
  { key: 'Published', label: 'منشور', color: 'text-indigo-600 dark:text-indigo-400', bg: 'bg-indigo-100/50 dark:bg-indigo-900/40', border: 'border-indigo-300/45 dark:border-indigo-700/45' }
];

export default function MediaPipelineBoard() {
  const [pipelines, setPipelines] = useState<MediaPipelineDto[]>([]);
  const [employees, setEmployees] = useState<EmployeeDto[]>([]);

  // Modal states
  const [isCreateOpen, setCreateOpen] = useState(false);
  const [editingPipeline, setEditingPipeline] = useState<MediaPipelineDto | null>(null);
  
  // Transition to Review state
  const [reviewPipeline, setReviewPipeline] = useState<MediaPipelineDto | null>(null);
  const [selectedSupervisorId, setSelectedSupervisorId] = useState<string>('');

  // Form states
  const [formData, setFormData] = useState({
    title: '',
    description: '',
    assignedAgentId: '',
    assetFolderUrl: ''
  });

  const [editFormData, setEditFormData] = useState({
    title: '',
    description: '',
    assignedAgentId: '',
    assetFolderUrl: '',
    editingErrorCount: 0,
    stage: 'Preparation' as MediaStage
  });

  const fetchData = useCallback(async () => {
    try {
      const pipelinesData = await mediaService.getPipelines({ pageSize: 100 });
      setPipelines(pipelinesData.items || []);
      
      const employeesData = await hrService.listEmployees();
      setEmployees(employeesData || []);
    } catch {
      // API client global interceptor handles error notifications
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleCreateSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.title) {
      toast.error('عنوان المادة مطلوب');
      return;
    }
    try {
      const res = await mediaService.createPipeline({
        title: formData.title,
        description: formData.description || undefined,
        assignedAgentId: formData.assignedAgentId || undefined,
        assetFolderUrl: formData.assetFolderUrl || undefined
      });
      if (res.success) {
        toast.success('تمت إضافة المادة بنجاح');
        setCreateOpen(false);
        setFormData({ title: '', description: '', assignedAgentId: '', assetFolderUrl: '' });
        fetchData();
      } else {
        toast.error(res.message || 'تعذر إضافة المادة');
      }
    } catch {
      // API client global interceptor handles error notifications
    }
  };

  const handleEditSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingPipeline) return;
    try {
      const res = await mediaService.updatePipeline(editingPipeline.id, {
        title: editFormData.title,
        description: editFormData.description || undefined,
        assignedAgentId: editFormData.assignedAgentId || undefined,
        assetFolderUrl: editFormData.assetFolderUrl || undefined,
        editingErrorCount: Number(editFormData.editingErrorCount),
        stage: editFormData.stage
      });
      if (res.success) {
        toast.success('تم تعديل المادة بنجاح');
        setEditingPipeline(null);
        fetchData();
      } else {
        toast.error(res.message || 'تعذر تعديل المادة');
      }
    } catch {
      // API client global interceptor handles error notifications
    }
  };

  const handleOpenEdit = (p: MediaPipelineDto) => {
    setEditingPipeline(p);
    setEditFormData({
      title: p.title,
      description: p.description || '',
      assignedAgentId: p.assignedAgentId || '',
      assetFolderUrl: p.assetFolderUrl || '',
      editingErrorCount: p.editingErrorCount,
      stage: p.stage
    });
  };

  const handleStageTransition = async (p: MediaPipelineDto, targetStage: MediaStage) => {
    // Check approval gate
    if (targetStage === 'Published' && p.stage !== 'Approved') {
      toast.error('لا يمكن نشر مادة لم يتم الموافقة عليها.');
      return;
    }

    // Entering Review requires Supervisor assignee selection
    if (targetStage === 'Review') {
      setReviewPipeline(p);
      setSelectedSupervisorId('');
      return;
    }

    try {
      const res = await mediaService.updatePipeline(p.id, {
        title: p.title,
        description: p.description,
        assignedAgentId: p.assignedAgentId,
        assetFolderUrl: p.assetFolderUrl,
        editingErrorCount: p.editingErrorCount,
        stage: targetStage
      });
      if (res.success) {
        toast.success(`تم نقل المادة إلى مرحلة ${STAGES.find(s => s.key === targetStage)?.label}`);
        fetchData();
      } else {
        toast.error(res.message || 'فشل نقل المادة');
      }
    } catch {
      // API client global interceptor handles error notifications
    }
  };

  const handleReviewSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reviewPipeline) return;
    if (!selectedSupervisorId) {
      toast.error('يجب اختيار مشرف للمراجعة');
      return;
    }

    try {
      const res = await mediaService.updatePipeline(reviewPipeline.id, {
        title: reviewPipeline.title,
        description: reviewPipeline.description,
        assignedAgentId: reviewPipeline.assignedAgentId,
        assetFolderUrl: reviewPipeline.assetFolderUrl,
        editingErrorCount: reviewPipeline.editingErrorCount,
        stage: 'Review',
        supervisorId: selectedSupervisorId
      });
      if (res.success) {
        toast.success('تم إرسال المادة للمراجعة وإنشاء مهمة للمشرف');
        setReviewPipeline(null);
        fetchData();
      } else {
        toast.error(res.message || 'فشل إرسال المادة للمراجعة');
      }
    } catch {
      // API client global interceptor handles error notifications
    }
  };

  // Helper to get next stage
  const getNextStage = (current: MediaStage): MediaStage | null => {
    const idx = STAGES.findIndex(s => s.key === current);
    if (idx !== -1 && idx < STAGES.length - 1) {
      return STAGES[idx + 1].key;
    }
    return null;
  };

  // Helper to get prev stage
  const getPrevStage = (current: MediaStage): MediaStage | null => {
    const idx = STAGES.findIndex(s => s.key === current);
    if (idx !== -1 && idx > 0 && current !== 'Review' && current !== 'Approved') {
      // Review/Approved are resolved through task system or supervisor editing
      return STAGES[idx - 1].key;
    }
    return null;
  };

  // Get supervisors/managers for assignment
  const supervisors = employees.filter(emp => 
    emp.roles.includes('Admin') || emp.roles.includes('Supervisor')
  );

  return (
    <div dir="rtl" className="w-full">
      {/* Header and Quick Add */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 mb-6">
        <div>
          <h2 className="text-xl font-bold text-[var(--admin-text)]">لوحة تتبع الإنتاج المرئي</h2>
          <p className="text-sm text-[var(--admin-muted)] mt-1">تتبع مسار إنتاج الدروس والمقاطع المرئية عبر 7 مراحل إنتاجية متكاملة.</p>
        </div>
        <NeumorphButton intent="primary" size="md" onClick={() => setCreateOpen(true)}>
          <Plus className="h-4 w-4 ml-1" />
          إضافة مادة للإنتاج
        </NeumorphButton>
      </div>

      {/* Kanban Board Container */}
      <div className="grid grid-cols-1 md:grid-cols-4 lg:grid-cols-7 gap-4 items-start overflow-x-auto pb-4">
        {STAGES.map((stageInfo) => {
          const stageItems = pipelines.filter(p => p.stage === stageInfo.key);
          
          return (
            <div 
              key={stageInfo.key} 
              className={`rounded-3xl border ${stageInfo.border} ${stageInfo.bg} backdrop-blur-md p-3 flex flex-col min-h-[500px] w-full min-w-[220px]`}
            >
              {/* Column Header */}
              <div className="flex items-center justify-between mb-4 border-b border-dashed border-[var(--admin-border)] pb-2">
                <span className={`font-bold text-sm ${stageInfo.color}`}>{stageInfo.label}</span>
                <span className="text-xs bg-black/10 dark:bg-white/10 rounded-full px-2 py-0.5 font-bold text-[var(--admin-text)]">
                  {stageItems.length}
                </span>
              </div>

              {/* Column Content */}
              <div className="flex flex-col gap-3 flex-1 overflow-y-auto max-h-[600px] no-scrollbar">
                {stageItems.length === 0 ? (
                  <div className="flex-1 flex flex-col items-center justify-center p-4 border-2 border-dashed border-[var(--admin-border)] rounded-2xl opacity-40">
                    <span className="text-xs text-[var(--admin-muted)]">فارغ</span>
                  </div>
                ) : (
                  stageItems.map((item) => {
                    const next = getNextStage(item.stage);
                    const prev = getPrevStage(item.stage);

                    return (
                      <div 
                        key={item.id}
                        className="p-3.5 rounded-2xl bg-[var(--admin-card)] border border-[var(--admin-border)] shadow-md hover:shadow-lg hover:border-[var(--admin-primary-30)] transition-all flex flex-col gap-2.5 relative group"
                      >
                        {/* Title and Edit Button */}
                        <div className="flex items-start justify-between gap-1">
                          <h4 className="font-bold text-xs text-[var(--admin-text)] leading-relaxed break-all pr-1">{item.title}</h4>
                          <button 
                            onClick={() => handleOpenEdit(item)} 
                            className="p-1 rounded-lg text-[var(--admin-muted)] hover:text-[var(--admin-primary)] hover:bg-[var(--admin-hover)] transition-all opacity-0 group-hover:opacity-100"
                          >
                            <Edit className="h-3.5 w-3.5" />
                          </button>
                        </div>

                        {/* Description */}
                        {item.description && (
                          <p className="text-xs text-[var(--admin-muted)] line-clamp-2 leading-relaxed">
                            {item.description}
                          </p>
                        )}

                        {/* Badges/Details */}
                        <div className="flex flex-col gap-1.5 pt-2 border-t border-[var(--admin-border)]">
                          {/* Assignee */}
                          <div className="flex items-center gap-1.5 text-xs text-[var(--admin-text)] font-medium">
                            <User className="h-3 w-3 text-[var(--admin-muted)]" />
                            <span>{item.assignedAgentName || 'غير معين'}</span>
                          </div>

                          {/* Asset Folder Link */}
                          {item.assetFolderUrl && (
                            <a 
                              href={item.assetFolderUrl} 
                              target="_blank" 
                              rel="noopener noreferrer"
                              className="inline-flex items-center gap-1 text-xs text-[var(--admin-primary)] font-bold hover:underline"
                            >
                              <FolderOpen className="h-3 w-3" />
                              <span>المجلد المشترك</span>
                              <ExternalLink className="h-2 w-2" />
                            </a>
                          )}

                          {/* Errors */}
                          {item.editingErrorCount > 0 && (
                            <div className="inline-flex items-center gap-1 text-xs text-rose-500 font-bold bg-rose-500/10 rounded px-1.5 py-0.5 w-fit">
                              <AlertTriangle className="h-3 w-3" />
                              <span>{item.editingErrorCount} أخطاء مونتاج</span>
                            </div>
                          )}
                        </div>

                        {/* Transition Buttons */}
                        <div className="flex items-center justify-between gap-2 mt-2 pt-2 border-t border-dashed border-[var(--admin-border)]">
                          {prev && (
                            <button
                              onClick={() => handleStageTransition(item, prev)}
                              className="text-xs text-[var(--admin-muted)] hover:text-[var(--admin-text)] flex items-center gap-0.5 hover:underline"
                            >
                              <ChevronRight className="h-3 w-3" />
                              <span>السابق</span>
                            </button>
                          )}
                          {!prev && <div />}

                          {next && (
                            <button
                              onClick={() => handleStageTransition(item, next)}
                              className={`text-xs font-bold flex items-center gap-0.5 px-2 py-1 rounded-xl transition-all ${
                                next === 'Published' 
                                  ? 'bg-indigo-500 text-white hover:bg-indigo-600'
                                  : next === 'Review'
                                  ? 'bg-rose-500 text-white hover:bg-rose-600'
                                  : 'bg-[var(--admin-primary-15)] text-[var(--admin-primary)] hover:bg-[var(--admin-primary-20)]'
                              }`}
                            >
                              <span>{next === 'Review' ? 'طلب مراجعة' : next === 'Published' ? 'نشر المادة' : 'التالي'}</span>
                              <ArrowLeft className="h-3 w-3 mr-0.5" />
                            </button>
                          )}
                        </div>
                      </div>
                    );
                  })
                )}
              </div>
            </div>
          );
        })}
      </div>

      {/* CREATE MODAL */}
      {isCreateOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="w-full max-w-lg rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-2xl animate-in fade-in zoom-in-95" dir="rtl">
            <h3 className="text-lg font-bold text-[var(--admin-text)] mb-4">إضافة مادة مرئية جديدة للإنتاج</h3>
            
            <form onSubmit={handleCreateSubmit} className="flex flex-col gap-4">
              <div className="flex flex-col gap-1">
                <label className="text-xs font-bold text-[var(--admin-text)]">عنوان المادة *</label>
                <input 
                  type="text" 
                  required
                  placeholder="مثال: شرح درس الجبر - الصف الثالث الثانوي"
                  value={formData.title}
                  onChange={e => setFormData({ ...formData, title: e.target.value })}
                  className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2.5 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none"
                />
              </div>

              <div className="flex flex-col gap-1">
                <label className="text-xs font-bold text-[var(--admin-text)]">تفاصيل أو ملاحظات</label>
                <textarea 
                  placeholder="ملاحظات الإنتاج أو السيناريو للمحرر..."
                  value={formData.description}
                  onChange={e => setFormData({ ...formData, description: e.target.value })}
                  className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2.5 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none h-24 resize-none"
                />
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="flex flex-col gap-1">
                  <label className="text-xs font-bold text-[var(--admin-text)]">محرر المونتاج المسؤول</label>
                  <select 
                    value={formData.assignedAgentId}
                    onChange={e => setFormData({ ...formData, assignedAgentId: e.target.value })}
                    className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2.5 text-sm text-[var(--admin-text)] outline-none"
                  >
                    <option value="">اختر الموظف...</option>
                    {employees.map(emp => (
                      <option key={emp.userId} value={emp.userId}>{emp.fullName}</option>
                    ))}
                  </select>
                </div>

                <div className="flex flex-col gap-1">
                  <label className="text-xs font-bold text-[var(--admin-text)]">رابط مجلد التخزين</label>
                  <input 
                    type="url" 
                    placeholder="رابط Google Drive أو Dropbox..."
                    value={formData.assetFolderUrl}
                    onChange={e => setFormData({ ...formData, assetFolderUrl: e.target.value })}
                    className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2.5 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none font-mono"
                  />
                </div>
              </div>

              <div className="flex justify-end gap-3 mt-4 border-t border-[var(--admin-border)] pt-4">
                <NeumorphButton type="button" intent="ghost" onClick={() => setCreateOpen(false)}>
                  إلغاء
                </NeumorphButton>
                <NeumorphButton type="submit" intent="primary">
                  إضافة ومتابعة
                </NeumorphButton>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* EDIT MODAL */}
      {editingPipeline && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="w-full max-w-lg rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-2xl" dir="rtl">
            <h3 className="text-lg font-bold text-[var(--admin-text)] mb-4">تعديل مادة الإنتاج المرئي</h3>
            
            <form onSubmit={handleEditSubmit} className="flex flex-col gap-4">
              <div className="flex flex-col gap-1">
                <label className="text-xs font-bold text-[var(--admin-text)]">عنوان المادة *</label>
                <input 
                  type="text" 
                  required
                  value={editFormData.title}
                  onChange={e => setEditFormData({ ...editFormData, title: e.target.value })}
                  className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2.5 text-sm text-[var(--admin-text)] outline-none"
                />
              </div>

              <div className="flex flex-col gap-1">
                <label className="text-xs font-bold text-[var(--admin-text)]">تفاصيل أو ملاحظات</label>
                <textarea 
                  value={editFormData.description}
                  onChange={e => setEditFormData({ ...editFormData, description: e.target.value })}
                  className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2.5 text-sm text-[var(--admin-text)] outline-none h-20 resize-none"
                />
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="flex flex-col gap-1">
                  <label className="text-xs font-bold text-[var(--admin-text)]">محرر المونتاج المسؤول</label>
                  <select 
                    value={editFormData.assignedAgentId}
                    onChange={e => setEditFormData({ ...editFormData, assignedAgentId: e.target.value })}
                    className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2.5 text-sm text-[var(--admin-text)] outline-none"
                  >
                    <option value="">اختر الموظف...</option>
                    {employees.map(emp => (
                      <option key={emp.userId} value={emp.userId}>{emp.fullName}</option>
                    ))}
                  </select>
                </div>

                <div className="flex flex-col gap-1">
                  <label className="text-xs font-bold text-[var(--admin-text)]">رابط مجلد التخزين</label>
                  <input 
                    type="url" 
                    value={editFormData.assetFolderUrl}
                    onChange={e => setEditFormData({ ...editFormData, assetFolderUrl: e.target.value })}
                    className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2.5 text-sm text-[var(--admin-text)] outline-none font-mono"
                  />
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="flex flex-col gap-1">
                  <label className="text-xs font-bold text-[var(--admin-text)]">عدد أخطاء المونتاج المرصودة</label>
                  <input 
                    type="number" 
                    min={0}
                    value={editFormData.editingErrorCount}
                    onChange={e => setEditFormData({ ...editFormData, editingErrorCount: Number(e.target.value) })}
                    className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2.5 text-sm text-[var(--admin-text)] outline-none"
                  />
                </div>

                <div className="flex flex-col gap-1">
                  <label className="text-xs font-bold text-[var(--admin-text)]">مرحلة الإنتاج</label>
                  <select 
                    value={editFormData.stage}
                    onChange={e => setEditFormData({ ...editFormData, stage: e.target.value as MediaStage })}
                    className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2.5 text-sm text-[var(--admin-text)] outline-none"
                  >
                    {STAGES.map(s => (
                      <option key={s.key} value={s.key}>{s.label}</option>
                    ))}
                  </select>
                </div>
              </div>

              <div className="flex justify-end gap-3 mt-4 border-t border-[var(--admin-border)] pt-4">
                <NeumorphButton type="button" intent="ghost" onClick={() => setEditingPipeline(null)}>
                  إلغاء
                </NeumorphButton>
                <NeumorphButton type="submit" intent="primary">
                  حفظ التعديلات
                </NeumorphButton>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* SELECT SUPERVISOR MODAL FOR REVIEW STAGE */}
      {reviewPipeline && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="w-full max-w-md rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-2xl animate-in fade-in" dir="rtl">
            <div className="flex items-center gap-3 text-rose-500 mb-3">
              <UserCheck className="h-6 w-6" />
              <h3 className="text-lg font-bold text-[var(--admin-text)]">طلب تدقيق ومراجعة المادة</h3>
            </div>
            
            <p className="text-xs text-[var(--admin-muted)] mb-4 leading-relaxed">
              عند طلب المراجعة، سيتم إنشاء مهمة مراجعة تلقائية ومزامنتها مع نظام الموافقات. الرجاء اختيار المشرف المسؤول لمراجعة المادة المرئية: <strong className="text-[var(--admin-text)]">{reviewPipeline.title}</strong>
            </p>

            <form onSubmit={handleReviewSubmit} className="flex flex-col gap-4">
              <div className="flex flex-col gap-1">
                <label className="text-xs font-bold text-[var(--admin-text)]">المشرف المسؤول عن المراجعة *</label>
                <select 
                  required
                  value={selectedSupervisorId}
                  onChange={e => setSelectedSupervisorId(e.target.value)}
                  className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2.5 text-sm text-[var(--admin-text)] outline-none"
                >
                  <option value="">اختر المشرف...</option>
                  {supervisors.map(sup => (
                    <option key={sup.userId} value={sup.userId}>{sup.fullName}</option>
                  ))}
                </select>
              </div>

              <div className="flex justify-end gap-3 mt-4 border-t border-[var(--admin-border)] pt-4">
                <NeumorphButton type="button" intent="ghost" onClick={() => setReviewPipeline(null)}>
                  إلغاء
                </NeumorphButton>
                <NeumorphButton type="submit" intent="primary" className="!bg-rose-500 !text-white hover:!bg-rose-600">
                  إرسال للمراجعة
                </NeumorphButton>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
