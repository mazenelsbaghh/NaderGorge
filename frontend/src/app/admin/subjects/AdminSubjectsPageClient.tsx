'use client';

import { useState, useEffect, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { 
  Plus, 
  Search, 
  Edit, 
  Trash2, 
  BookOpen, 
  X, 
  Check 
} from 'lucide-react';
import { AdminShellChrome } from '@/components/admin';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { teacherService, type SubjectDto } from '@/services/teacher-service';
import toast from 'react-hot-toast';

export default function AdminSubjectsPageClient() {
  const [subjects, setSubjects] = useState<SubjectDto[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingSubject, setEditingSubject] = useState<SubjectDto | null>(null);
  
  // Form States
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [isSaving, setIsSaving] = useState(false);

  // Delete State
  const [subjectToDelete, setSubjectToDelete] = useState<SubjectDto | null>(null);

  const loadSubjects = useCallback(async () => {
    setIsLoading(true);
    try {
      const res = await teacherService.getSubjects();
      if (res.success) {
        setSubjects(res.data || []);
      } else {
        toast.error(res.message || 'فشل في تحميل المواد الدراسية');
      }
    } catch (err) {
      console.error(err);
      toast.error('حدث خطأ أثناء الاتصال بالخادم');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    loadSubjects();
  }, [loadSubjects]);

  const handleOpenModal = (subject: SubjectDto | null = null) => {
    if (subject) {
      setEditingSubject(subject);
      setName(subject.name);
      setDescription(subject.description);
    } else {
      setEditingSubject(null);
      setName('');
      setDescription('');
    }
    setIsModalOpen(true);
  };

  const handleCloseModal = () => {
    setIsModalOpen(false);
    setEditingSubject(null);
    setName('');
    setDescription('');
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      toast.error('يرجى إدخال اسم المادة');
      return;
    }

    setIsSaving(true);
    try {
      if (editingSubject) {
        const res = await teacherService.updateSubject(editingSubject.id, {
          name: name.trim(),
          description: description.trim(),
        });
        if (res.success) {
          toast.success('تم تحديث المادة بنجاح ✅');
          handleCloseModal();
          loadSubjects();
        } else {
          toast.error(res.message || 'فشل في تحديث المادة');
        }
      } else {
        const res = await teacherService.createSubject({
          name: name.trim(),
          description: description.trim(),
        });
        if (res.success) {
          toast.success('تم إضافة المادة بنجاح ✅');
          handleCloseModal();
          loadSubjects();
        } else {
          toast.error(res.message || 'فشل في إضافة المادة');
        }
      }
    } catch (err) {
      console.error(err);
      toast.error('حدث خطأ أثناء حفظ البيانات');
    } finally {
      setIsSaving(false);
    }
  };

  const handleDeleteConfirm = async () => {
    if (!subjectToDelete) return;
    try {
      const res = await teacherService.deleteSubject(subjectToDelete.id);
      if (res.success) {
        toast.success('تم حذف المادة بنجاح ✅');
        setSubjectToDelete(null);
        loadSubjects();
      } else {
        toast.error(res.message || 'لا يمكن حذف المادة لأنها مرتبطة بمحتوى آخر');
      }
    } catch (err) {
      console.error(err);
      toast.error('حدث خطأ أثناء محاولة الحذف');
    }
  };

  const filteredSubjects = subjects.filter(
    (s) =>
      s.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
      s.description.toLowerCase().includes(searchQuery.toLowerCase())
  );

  return (
    <AdminShellChrome
      activePath="/admin/subjects"
      sectionLabel="المحتوى والمواد"
      pageTitle="المواد الدراسية"
      subtitle="إدارة وتصنيف المواد الدراسية المتاحة في المنصة وربطها بالمعلمين والمناهج."
      action={
        <button
          onClick={() => handleOpenModal()}
          className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-primary)] px-5 py-2.5 text-sm font-bold text-[var(--admin-primary-contrast)] shadow-lg transition hover:scale-105 hover:opacity-90 active:scale-95"
        >
          <Plus className="h-4 w-4" />
          إضافة مادة جديدة
        </button>
      }
    >
      {/* Search Input */}
      <div className="relative mb-8 max-w-md">
        <input
          type="text"
          placeholder="ابحث عن مادة..."
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]/80 py-3.5 pl-4 pr-12 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none backdrop-blur-xl transition focus:border-[var(--admin-primary)]"
        />
        <Search className="absolute right-4 top-1/2 h-5 w-5 -translate-y-1/2 text-[var(--admin-muted)]" />
      </div>

      {isLoading ? (
        <div className="flex min-h-[400px] flex-col items-center justify-center gap-4">
          <div className="h-10 w-10 animate-spin rounded-full border-4 border-[var(--admin-primary)] border-t-transparent"></div>
          <p className="text-sm text-[var(--admin-muted)]">جاري تحميل المواد الدراسية...</p>
        </div>
      ) : filteredSubjects.length === 0 ? (
        <div className="flex min-h-[300px] flex-col items-center justify-center rounded-[2rem] border border-dashed border-[var(--admin-border)] bg-[var(--admin-card)]/50 p-8 text-center backdrop-blur-xl">
          <BookOpen className="mb-4 h-12 w-12 text-[var(--admin-muted)]" />
          <h3 className="text-lg font-bold text-[var(--admin-text)]">لا توجد مواد دراسية</h3>
          <p className="mt-2 text-sm text-[var(--admin-muted)]">
            {searchQuery ? 'لم يتم العثور على نتائج تطابق بحثك' : 'ابدأ بإضافة أول مادة دراسية للمنصة'}
          </p>
        </div>
      ) : (
        <motion.div 
          layout
          className="grid grid-cols-1 gap-6 md:grid-cols-2 lg:grid-cols-3"
        >
          <AnimatePresence mode="popLayout">
            {filteredSubjects.map((subject) => (
              <motion.div
                key={subject.id}
                layout
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, scale: 0.9 }}
                className="group flex flex-col justify-between overflow-hidden rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-6 shadow-md transition hover:shadow-xl"
              >
                <div>
                  <div className="flex items-center gap-3">
                    <div className="rounded-xl bg-[var(--admin-primary)]/10 p-3 text-[var(--admin-primary)]">
                      <BookOpen className="h-5 w-5" />
                    </div>
                    <h2 className="text-lg font-extrabold text-[var(--admin-text)] group-hover:text-[var(--admin-primary)] transition">
                      {subject.name}
                    </h2>
                  </div>
                  <p className="mt-4 text-sm leading-relaxed text-[var(--admin-muted)] line-clamp-3">
                    {subject.description || 'لا يوجد وصف للمادة.'}
                  </p>
                </div>

                <div className="mt-6 flex items-center justify-end gap-2 border-t border-[var(--admin-border)] pt-4">
                  <button
                    onClick={() => handleOpenModal(subject)}
                    className="inline-flex h-9 w-9 items-center justify-center rounded-xl bg-blue-500/10 text-blue-500 hover:bg-blue-500 hover:text-white transition active:scale-95"
                    title="تعديل المادة"
                  >
                    <Edit className="h-4 w-4" />
                  </button>
                  <button
                    onClick={() => setSubjectToDelete(subject)}
                    className="inline-flex h-9 w-9 items-center justify-center rounded-xl bg-red-500/10 text-red-500 hover:bg-red-500 hover:text-white transition active:scale-95"
                    title="حذف المادة"
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              </motion.div>
            ))}
          </AnimatePresence>
        </motion.div>
      )}

      {/* Subject Modal (Add / Edit) */}
      <AnimatePresence>
        {isModalOpen && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
            {/* Backdrop */}
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              onClick={handleCloseModal}
              className="absolute inset-0 bg-black/60 backdrop-blur-md"
            />
            {/* Modal Box */}
            <motion.div
              initial={{ opacity: 0, scale: 0.9, y: 20 }}
              animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.9, y: 20 }}
              className="relative w-full max-w-lg overflow-hidden rounded-[2.5rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-2xl backdrop-blur-3xl"
            >
              <button
                onClick={handleCloseModal}
                className="absolute left-6 top-6 rounded-xl border border-[var(--admin-border)] p-2 text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]"
              >
                <X className="h-4 w-4" />
              </button>

              <h2 className="text-xl font-black text-[var(--admin-text)]">
                {editingSubject ? 'تعديل مادة دراسية' : 'إضافة مادة دراسية جديدة'}
              </h2>
              <p className="mt-1 text-sm text-[var(--admin-muted)]">
                قم بتعبئة الحقول المطلوبة لإنشاء أو تحديث المادة الدراسية.
              </p>

              <form onSubmit={handleSave} className="mt-6 space-y-6">
                <div>
                  <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">اسم المادة *</label>
                  <input
                    type="text"
                    required
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    placeholder="مثال: اللغة العربية، الكيمياء، التاريخ..."
                    className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] transition"
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">وصف المادة</label>
                  <textarea
                    rows={4}
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                    placeholder="اكتب نبذة مختصرة عن منهج المادة الدراسية..."
                    className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] transition resize-none"
                  />
                </div>

                <div className="flex items-center justify-end gap-3 pt-4 border-t border-[var(--admin-border)]">
                  <button
                    type="button"
                    onClick={handleCloseModal}
                    className="rounded-full border border-[var(--admin-border)] px-6 py-2.5 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)]"
                  >
                    إلغاء
                  </button>
                  <button
                    type="submit"
                    disabled={isSaving}
                    className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-primary)] px-6 py-2.5 text-sm font-bold text-[var(--admin-primary-contrast)] shadow-lg hover:opacity-90 active:scale-95 disabled:opacity-50"
                  >
                    {isSaving ? (
                      <div className="h-4 w-4 animate-spin rounded-full border-2 border-[var(--admin-primary-contrast)] border-t-transparent"></div>
                    ) : (
                      <Check className="h-4 w-4" />
                    )}
                    حفظ البيانات
                  </button>
                </div>
              </form>
            </motion.div>
          </div>
        )}
      </AnimatePresence>

      {/* Delete Confirmation Dialog */}
      <ConfirmDialog
        open={subjectToDelete !== null}
        title="حذف المادة الدراسية"
        description={`هل أنت متأكد من رغبتك في حذف مادة "${subjectToDelete?.name}"؟ لا يمكن التراجع عن هذا الإجراء، ولن ينجح إذا كانت المادة تحتوي على برامج أو باقات تابعة.`}
        onConfirm={handleDeleteConfirm}
        onCancel={() => setSubjectToDelete(null)}
      />
    </AdminShellChrome>
  );
}
