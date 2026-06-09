'use client';

import { useState, useEffect, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { 
  Plus, 
  Search, 
  Edit, 
  GraduationCap, 
  X, 
  Check, 
  User, 
  Phone, 
  Percent, 
  Mail, 
  Image as ImageIcon,
  BookOpen
} from 'lucide-react';
import { AdminShellChrome } from '@/components/admin';
import { teacherService, type TeacherDto, type SubjectDto } from '@/services/teacher-service';
import { adminService } from '@/services/admin-service';
import toast from 'react-hot-toast';

export default function AdminTeachersPage() {
  const [teachers, setTeachers] = useState<TeacherDto[]>([]);
  const [subjects, setSubjects] = useState<SubjectDto[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingTeacher, setEditingTeacher] = useState<TeacherDto | null>(null);

  // User search states for onboarding new teachers
  const [userSearchText, setUserSearchText] = useState('');
  const [userSearchResults, setUserSearchResults] = useState<any[]>([]);
  const [isSearchingUsers, setIsSearchingUsers] = useState(false);
  const [selectedUser, setSelectedUser] = useState<{ id: string; fullName: string; phoneNumber: string } | null>(null);

  // Form states
  const [bio, setBio] = useState('');
  const [specialization, setSpecialization] = useState('');
  const [commissionRate, setCommissionRate] = useState(0);
  const [contactInfo, setContactInfo] = useState('');
  const [profileImageUrl, setProfileImageUrl] = useState('');
  const [selectedSubjectIds, setSelectedSubjectIds] = useState<string[]>([]);
  const [isSaving, setIsSaving] = useState(false);

  const loadData = useCallback(async () => {
    setIsLoading(true);
    try {
      const [teachersRes, subjectsRes] = await Promise.all([
        teacherService.getTeachers(),
        teacherService.getSubjects(),
      ]);

      if (teachersRes.success) {
        setTeachers(teachersRes.data || []);
      }
      if (subjectsRes.success) {
        setSubjects(subjectsRes.data || []);
      }
    } catch (err) {
      console.error(err);
      toast.error('حدث خطأ أثناء تحميل البيانات');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Search users in DB to bind them to a Teacher Profile
  const handleUserSearch = async () => {
    if (!userSearchText.trim()) return;
    setIsSearchingUsers(true);
    try {
      const res = await adminService.listUsers(1, 10, userSearchText.trim());
      if (res && res.items) {
        setUserSearchResults(res.items || []);
      } else {
        setUserSearchResults([]);
      }
    } catch (err) {
      console.error(err);
      toast.error('فشل في البحث عن المستخدمين');
    } finally {
      setIsSearchingUsers(false);
    }
  };

  const handleOpenModal = (teacher: TeacherDto | null = null) => {
    if (teacher) {
      setEditingTeacher(teacher);
      setSelectedUser({
        id: teacher.userId,
        fullName: teacher.fullName,
        phoneNumber: teacher.phoneNumber,
      });
      setBio(teacher.bio);
      setSpecialization(teacher.specialization);
      setCommissionRate(teacher.commissionRate);
      setContactInfo(teacher.contactInfo);
      setProfileImageUrl(teacher.profileImageUrl || '');
      setSelectedSubjectIds(teacher.subjectIds || []);
    } else {
      setEditingTeacher(null);
      setSelectedUser(null);
      setUserSearchText('');
      setUserSearchResults([]);
      setBio('');
      setSpecialization('');
      setCommissionRate(0);
      setContactInfo('');
      setProfileImageUrl('');
      setSelectedSubjectIds([]);
    }
    setIsModalOpen(true);
  };

  const handleCloseModal = () => {
    setIsModalOpen(false);
    setEditingTeacher(null);
    setSelectedUser(null);
    setUserSearchText('');
    setUserSearchResults([]);
  };

  const handleSubjectToggle = (subjectId: string) => {
    setSelectedSubjectIds((prev) =>
      prev.includes(subjectId)
        ? prev.filter((id) => id !== subjectId)
        : [...prev, subjectId]
    );
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedUser) {
      toast.error('يرجى تحديد حساب المستخدم أولاً');
      return;
    }

    setIsSaving(true);
    try {
      if (editingTeacher) {
        const res = await teacherService.updateTeacher(editingTeacher.id, {
          bio: bio.trim(),
          specialization: specialization.trim(),
          commissionRate: Number(commissionRate),
          contactInfo: contactInfo.trim(),
          profileImageUrl: profileImageUrl.trim() || undefined,
          subjectIds: selectedSubjectIds,
        });

        if (res.success) {
          toast.success('تم تحديث ملف المعلم بنجاح ✅');
          handleCloseModal();
          loadData();
        } else {
          toast.error(res.message || 'فشل في تحديث ملف المعلم');
        }
      } else {
        const res = await teacherService.createTeacher({
          userId: selectedUser.id,
          bio: bio.trim(),
          specialization: specialization.trim(),
          commissionRate: Number(commissionRate),
          contactInfo: contactInfo.trim(),
          profileImageUrl: profileImageUrl.trim() || undefined,
          subjectIds: selectedSubjectIds,
        });

        if (res.success) {
          toast.success('تم إنشاء ملف المعلم بنجاح وتعيين الصلاحيات ✅');
          handleCloseModal();
          loadData();
        } else {
          toast.error(res.message || 'فشل في إنشاء ملف المعلم');
        }
      }
    } catch (err) {
      console.error(err);
      toast.error('حدث خطأ أثناء حفظ الملف');
    } finally {
      setIsSaving(false);
    }
  };

  const filteredTeachers = teachers.filter(
    (t) =>
      t.fullName.toLowerCase().includes(searchQuery.toLowerCase()) ||
      t.specialization.toLowerCase().includes(searchQuery.toLowerCase())
  );

  return (
    <AdminShellChrome
      activePath="/admin/teachers"
      sectionLabel="المستخدمين"
      pageTitle="المعلمين"
      subtitle="إدارة الحسابات التعليمية المعتمدة وتعيين تخصصاتهم ونسب عمولاتهم والمواد الدراسية التابعة لهم."
      action={
        <button
          onClick={() => handleOpenModal()}
          className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-primary)] px-5 py-2.5 text-sm font-bold text-[var(--admin-primary-contrast)] shadow-lg transition hover:scale-105 hover:opacity-90 active:scale-95"
        >
          <Plus className="h-4 w-4" />
          إضافة معلم جديد
        </button>
      }
    >
      {/* Search Bar */}
      <div className="relative mb-8 max-w-md">
        <input
          type="text"
          placeholder="ابحث عن معلم بالاسم أو التخصص..."
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]/80 py-3.5 pl-4 pr-12 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none backdrop-blur-xl transition focus:border-[var(--admin-primary)]"
        />
        <Search className="absolute right-4 top-1/2 h-5 w-5 -translate-y-1/2 text-[var(--admin-muted)]" />
      </div>

      {isLoading ? (
        <div className="flex min-h-[400px] flex-col items-center justify-center gap-4">
          <div className="h-10 w-10 animate-spin rounded-full border-4 border-[var(--admin-primary)] border-t-transparent"></div>
          <p className="text-sm text-[var(--admin-muted)]">جاري تحميل المعلمين...</p>
        </div>
      ) : filteredTeachers.length === 0 ? (
        <div className="flex min-h-[300px] flex-col items-center justify-center rounded-[2rem] border border-dashed border-[var(--admin-border)] bg-[var(--admin-card)]/50 p-8 text-center backdrop-blur-xl">
          <GraduationCap className="mb-4 h-12 w-12 text-[var(--admin-muted)]" />
          <h3 className="text-lg font-bold text-[var(--admin-text)]">لا يوجد معلمون</h3>
          <p className="mt-2 text-sm text-[var(--admin-muted)]">
            {searchQuery ? 'لم يتم العثور على نتائج تطابق بحثك' : 'ابدأ بتهيئة أول حساب معلم في المنصة'}
          </p>
        </div>
      ) : (
        <motion.div 
          layout
          className="grid grid-cols-1 gap-6 lg:grid-cols-2"
        >
          <AnimatePresence mode="popLayout">
            {filteredTeachers.map((teacher) => (
              <motion.div
                key={teacher.id}
                layout
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, scale: 0.9 }}
                className="group flex flex-col justify-between overflow-hidden rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-6 shadow-md transition hover:shadow-xl"
              >
                <div>
                  <div className="flex items-start gap-4">
                    {teacher.profileImageUrl ? (
                      // eslint-disable-next-line @next/next/no-img-element
                      <img
                        src={teacher.profileImageUrl}
                        alt={teacher.fullName}
                        className="h-16 w-16 rounded-[1.25rem] object-cover border border-[var(--admin-border)] shadow"
                      />
                    ) : (
                      <div className="flex h-16 w-16 items-center justify-center rounded-[1.25rem] bg-[var(--admin-primary)]/10 text-[var(--admin-primary)] border border-[var(--admin-border)]">
                        <GraduationCap className="h-8 w-8" />
                      </div>
                    )}

                    <div className="flex-1">
                      <h2 className="text-lg font-extrabold text-[var(--admin-text)] group-hover:text-[var(--admin-primary)] transition">
                        {teacher.fullName}
                      </h2>
                      <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-[var(--admin-muted)]">
                        <span className="flex items-center gap-1">
                          <Phone className="h-3 w-3" />
                          {teacher.phoneNumber}
                        </span>
                        <span className="flex items-center gap-1">
                          <Percent className="h-3 w-3" />
                          العمولة: {teacher.commissionRate}%
                        </span>
                      </div>
                    </div>
                  </div>

                  {/* Bio & Subjects */}
                  <p className="mt-4 text-sm text-[var(--admin-muted)] line-clamp-2">
                    {teacher.bio || 'لم يتم كتابة سيرة ذاتية بعد.'}
                  </p>

                  <div className="mt-4 flex flex-wrap gap-1.5">
                    {teacher.subjectNames && teacher.subjectNames.length > 0 ? (
                      teacher.subjectNames.map((name, i) => (
                        <span
                          key={i}
                          className="inline-flex items-center gap-1 rounded-full bg-[var(--admin-primary)]/10 px-2.5 py-1 text-xs font-semibold text-[var(--admin-primary)]"
                        >
                          <BookOpen className="h-3 w-3" />
                          {name}
                        </span>
                      ))
                    ) : (
                      <span className="text-xs text-red-500">لم يتم تعيين أي مادة بعد</span>
                    )}
                  </div>
                </div>

                <div className="mt-6 flex items-center justify-between border-t border-[var(--admin-border)] pt-4">
                  <span className="text-xs text-[var(--admin-muted)]">
                    التخصص: <strong className="text-[var(--admin-text)]">{teacher.specialization || 'غير محدد'}</strong>
                  </span>
                  <div className="flex items-center gap-2">
                    <button
                      onClick={() => handleOpenModal(teacher)}
                      className="inline-flex h-9 px-4 items-center gap-2 rounded-xl bg-blue-500/10 text-blue-500 hover:bg-blue-500 hover:text-white transition active:scale-95 text-xs font-bold"
                    >
                      <Edit className="h-4 w-4" />
                      تعديل الملف
                    </button>
                  </div>
                </div>
              </motion.div>
            ))}
          </AnimatePresence>
        </motion.div>
      )}

      {/* Onboarding / Edit Modal */}
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
              className="relative w-full max-w-2xl overflow-y-auto max-h-[90vh] rounded-[2.5rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-2xl backdrop-blur-3xl"
            >
              <button
                onClick={handleCloseModal}
                className="absolute left-6 top-6 rounded-xl border border-[var(--admin-border)] p-2 text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]"
              >
                <X className="h-4 w-4" />
              </button>

              <h2 className="text-xl font-black text-[var(--admin-text)]">
                {editingTeacher ? 'تعديل ملف المعلم' : 'تهيئة حساب معلم جديد'}
              </h2>
              <p className="mt-1 text-sm text-[var(--admin-muted)]">
                يرجى ربط ملف المعلم بحساب مستخدم مسجل بالمنصة وتحديد الصلاحيات والمواد.
              </p>

              {/* Step 1: Select User (Only for New Onboarding) */}
              {!editingTeacher && !selectedUser ? (
                <div className="mt-6 space-y-4">
                  <label className="block text-xs font-bold text-[var(--admin-text)]">البحث عن حساب المستخدم *</label>
                  <div className="flex gap-2">
                    <input
                      type="text"
                      placeholder="ابحث بالاسم أو برقم الهاتف..."
                      value={userSearchText}
                      onChange={(e) => setUserSearchText(e.target.value)}
                      onKeyDown={(e) => e.key === 'Enter' && handleUserSearch()}
                      className="flex-1 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)] transition"
                    />
                    <button
                      type="button"
                      onClick={handleUserSearch}
                      disabled={isSearchingUsers}
                      className="rounded-2xl bg-[var(--admin-primary)] px-6 text-sm font-bold text-[var(--admin-primary-contrast)] transition hover:opacity-90 active:scale-95"
                    >
                      {isSearchingUsers ? 'جاري البحث...' : 'بحث'}
                    </button>
                  </div>

                  {/* Search Results list */}
                  <div className="mt-2 divide-y divide-[var(--admin-border)] rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] max-h-60 overflow-y-auto">
                    {userSearchResults.length === 0 ? (
                      <div className="p-4 text-center text-xs text-[var(--admin-muted)]">
                        {userSearchText ? 'لا توجد نتائج بحث' : 'اكتب الاسم أو الهاتف للبحث عن حساب المستخدم'}
                      </div>
                    ) : (
                      userSearchResults.map((userItem) => (
                        <div
                          key={userItem.id}
                          onClick={() => {
                            setSelectedUser({
                              id: userItem.id,
                              fullName: userItem.fullName,
                              phoneNumber: userItem.phoneNumber,
                            });
                          }}
                          className="flex items-center justify-between p-3.5 hover:bg-[var(--admin-hover)] cursor-pointer transition"
                        >
                          <div>
                            <div className="text-sm font-bold text-[var(--admin-text)]">{userItem.fullName}</div>
                            <div className="text-xs text-[var(--admin-muted)]">{userItem.phoneNumber}</div>
                          </div>
                          <div className="text-xs font-semibold rounded-full bg-[var(--admin-bg)] px-3 py-1 text-[var(--admin-primary)] border border-[var(--admin-border)]">
                            {userItem.roles?.join(', ') || 'طالب/عام'}
                          </div>
                        </div>
                      ))
                    )}
                  </div>
                </div>
              ) : (
                /* Onboarding Form */
                <form onSubmit={handleSave} className="mt-6 space-y-6">
                  {/* Readonly Selected User Display */}
                  <div className="flex items-center justify-between rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-4">
                    <div>
                      <div className="text-xs text-[var(--admin-muted)] mb-1">المستخدم المربوط بالحساب</div>
                      <div className="text-sm font-bold text-[var(--admin-text)] flex items-center gap-1.5">
                        <User className="h-4 w-4 text-[var(--admin-primary)]" />
                        {selectedUser?.fullName}
                      </div>
                      <div className="text-xs text-[var(--admin-muted)] flex items-center gap-1 mt-0.5">
                        <Phone className="h-3 w-3" />
                        {selectedUser?.phoneNumber}
                      </div>
                    </div>
                    {!editingTeacher && (
                      <button
                        type="button"
                        onClick={() => setSelectedUser(null)}
                        className="text-xs font-bold text-red-500 hover:underline"
                      >
                        تغيير الحساب
                      </button>
                    )}
                  </div>

                  {/* Form fields */}
                  <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                    <div>
                      <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">التخصص التعليمي *</label>
                      <input
                        type="text"
                        required
                        value={specialization}
                        onChange={(e) => setSpecialization(e.target.value)}
                        placeholder="مثال: مدرس أول فيزياء، خبير اللغة الإنجليزية..."
                        className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] transition"
                      />
                    </div>

                    <div>
                      <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">نسبة العمولة (%) *</label>
                      <div className="relative">
                        <input
                          type="number"
                          required
                          min="0"
                          max="100"
                          step="0.01"
                          value={commissionRate}
                          onChange={(e) => setCommissionRate(Number(e.target.value))}
                          placeholder="مثال: 12.5"
                          className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] py-3 pl-4 pr-12 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] transition"
                        />
                        <Percent className="absolute right-4 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--admin-muted)]" />
                      </div>
                    </div>
                  </div>

                  <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                    <div>
                      <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">نقاط الاتصال / البريد الإلكتروني *</label>
                      <div className="relative">
                        <input
                          type="text"
                          required
                          value={contactInfo}
                          onChange={(e) => setContactInfo(e.target.value)}
                          placeholder="معلومات التواصل أو إيميل المعلم..."
                          className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] py-3 pl-4 pr-12 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] transition"
                        />
                        <Mail className="absolute right-4 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--admin-muted)]" />
                      </div>
                    </div>

                    <div>
                      <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">رابط الصورة الشخصية (اختياري)</label>
                      <div className="relative">
                        <input
                          type="url"
                          value={profileImageUrl}
                          onChange={(e) => setProfileImageUrl(e.target.value)}
                          placeholder="https://example.com/avatar.jpg"
                          className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] py-3 pl-4 pr-12 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] transition"
                        />
                        <ImageIcon className="absolute right-4 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--admin-muted)]" />
                      </div>
                    </div>
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">السيرة الذاتية (البيو)</label>
                    <textarea
                      rows={3}
                      value={bio}
                      onChange={(e) => setBio(e.target.value)}
                      placeholder="اكتب نبذة ترويجية قصيرة تظهر للطلاب في صفحة الباقات..."
                      className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] transition resize-none"
                    />
                  </div>

                  {/* Subjects checkboxes */}
                  <div>
                    <label className="block text-xs font-bold text-[var(--admin-text)] mb-3">المواد الدراسية التي يدرسها المعلم *</label>
                    {subjects.length === 0 ? (
                      <div className="rounded-2xl border border-dashed border-[var(--admin-border)] p-4 text-center text-xs text-[var(--admin-muted)]">
                        لم يتم إضافة أي مواد دراسية بعد. يرجى تهيئة المواد من قسم إدارة المواد أولاً.
                      </div>
                    ) : (
                      <div className="grid grid-cols-2 gap-3 max-h-40 overflow-y-auto rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-4">
                        {subjects.map((sub) => {
                          const isChecked = selectedSubjectIds.includes(sub.id);
                          return (
                            <div
                              key={sub.id}
                              onClick={() => handleSubjectToggle(sub.id)}
                              className={`flex items-center gap-3 rounded-xl border p-3 cursor-pointer transition select-none ${
                                isChecked
                                  ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)]/5 text-[var(--admin-text)]'
                                  : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)]'
                              }`}
                            >
                              <div
                                className={`flex h-4 w-4 items-center justify-center rounded border transition ${
                                  isChecked
                                    ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]'
                                    : 'border-[var(--admin-muted)]'
                                }`}
                              >
                                {isChecked && <Check className="h-3 w-3 stroke-[3]" />}
                              </div>
                              <span className="text-xs font-bold">{sub.name}</span>
                            </div>
                          );
                        })}
                      </div>
                    )}
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
              )}
            </motion.div>
          </div>
        )}
      </AnimatePresence>
    </AdminShellChrome>
  );
}
