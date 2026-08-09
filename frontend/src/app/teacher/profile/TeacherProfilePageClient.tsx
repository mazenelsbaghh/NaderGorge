"use client";

import { useEffect, useState } from "react";
import {
  Bookmark,
  CheckSquare,
  FileText,
  Image as ImageIcon,
  Loader2,
  Phone,
  Plus,
  Power,
  Save,
  Sparkles,
  User,
  Users,
} from "lucide-react";
import toast from "react-hot-toast";

import {
  AdminColumn,
  AdminDataTable,
  AdminStatCard,
  AdminTab,
  AdminTabBar,
} from "@/components/admin";
import { TeacherPage } from "@/components/teacher/TeacherShellChrome";
import { teacherService, type TeacherStaffMemberDto } from "@/services/teacher-service";
import { resolveMediaUrl } from "@/utils/resolve-media-url";
import { compressImage, renameFileToMatchBase64 } from "@/utils/image-compressor";

type ProfileTab = "details" | "images" | "staff";

const TABS: AdminTab<ProfileTab>[] = [
  { key: "details", label: "بيانات المدرس", icon: User },
  { key: "images", label: "الصور", icon: ImageIcon },
  { key: "staff", label: "استاف المدرس", icon: Users },
];

const STAFF_PERMISSION_OPTIONS = [
  { key: "dashboard", label: "الرئيسية والإحصائيات" },
  { key: "activity", label: "نشاط الطلاب" },
  { key: "reports", label: "مركز التقارير" },
  { key: "students", label: "بيانات الطلاب" },
  { key: "content", label: "المحتوى والباقات" },
  { key: "codes", label: "أكواد الوصول" },
  { key: "publicExams", label: "الامتحانات العامة" },
  { key: "community", label: "مجتمع المدرس" },
  { key: "comments", label: "تعليقات الطلاب والردود" },
  { key: "essays", label: "تصحيح المقالي" },
  { key: "finance", label: "المالية" },
  { key: "profile", label: "بروفايل المدرس" },
  { key: "chat", label: "التواصل الداخلي" },
] as const;

const permissionLabel = (key: string) =>
  STAFF_PERMISSION_OPTIONS.find((permission) => permission.key === key)?.label ?? key;

const TelegramIcon = (props: React.SVGProps<SVGSVGElement>) => (
  <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
    <path d="m22 2-7 20-4-9-9-4Z" />
    <path d="M22 2 11 13" />
  </svg>
);

export default function TeacherProfilePageClient() {
  const [activeTab, setActiveTab] = useState<ProfileTab>("details");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const [bio, setBio] = useState("");
  const [specialization, setSpecialization] = useState("");
  const [contactInfo, setContactInfo] = useState("");
  const [profileImageUrl, setProfileImageUrl] = useState("");
  const [assistantPhoneNumbers, setAssistantPhoneNumbers] = useState("");
  const [facebookUrl, setFacebookUrl] = useState("");
  const [youtubeUrl, setYouTubeUrl] = useState("");
  const [telegramUrl, setTelegramUrl] = useState("");

  const [staff, setStaff] = useState<TeacherStaffMemberDto[]>([]);
  const [isOwner, setIsOwner] = useState(false);
  const [staffLoading, setStaffLoading] = useState(false);
  const [staffSaving, setStaffSaving] = useState(false);
  const [staffForm, setStaffForm] = useState({ fullName: "", phoneNumber: "", password: "", notes: "", permissionKeys: ["dashboard"] as string[] });
  const [savingPermissionsId, setSavingPermissionsId] = useState<string | null>(null);

  const [profileImagePreview, setProfileImagePreview] = useState<string | null>(null);
  const [isUploadingProfile, setIsUploadingProfile] = useState(false);
  const [aiPhotoPreview, setAiPhotoPreview] = useState<string | null>(null);
  const [isUploadingAi, setIsUploadingAi] = useState(false);

  const loadStaff = async () => {
    try {
      setStaffLoading(true);
      const res = await teacherService.getMyStaff();
      if (res.success) setStaff(res.data ?? []);
    } catch {
      // Staff-created accounts are intentionally hidden from staff users.
    } finally {
      setStaffLoading(false);
    }
  };

  useEffect(() => {
    teacherService.getMyProfile()
      .then((res) => {
        if (res.success) {
          setBio(res.data.bio || "");
          setSpecialization(res.data.specialization || "");
          setContactInfo(res.data.contactInfo || "");
          setProfileImageUrl(res.data.profileImageUrl || "");
          setAssistantPhoneNumbers(res.data.assistantPhoneNumbers || "");
          setFacebookUrl(res.data.facebookUrl || "");
          setYouTubeUrl(res.data.youtubeUrl || "");
          setTelegramUrl(res.data.telegramUrl || "");
          setProfileImagePreview(res.data.profileImageUrl || null);
        }
      })
      .catch((err) => {
        console.error("Error fetching profile:", err);
        toast.error("فشل في تحميل بيانات الملف الشخصي");
      })
      .finally(() => setLoading(false));

    teacherService.getActiveTeacherPhoto()
      .then((res) => {
        if (res.success && res.data?.url) setAiPhotoPreview(res.data.url);
      })
      .catch((err) => {
        console.error("Error fetching active AI photo:", err);
      });

    teacherService.getWorkspaceContext()
      .then((res) => {
        if (!res.success || !res.data?.isOwner) return;
        setIsOwner(true);
        void loadStaff();
      })
      .catch(() => setIsOwner(false));
  }, []);

  const handleCreateStaff = async () => {
    if (!staffForm.fullName.trim() || !staffForm.phoneNumber.trim() || staffForm.password.length < 8 || !/[A-Za-z\p{L}]/u.test(staffForm.password) || !/\d/.test(staffForm.password)) {
      toast.error("اكتب الاسم ورقم هاتف صحيح وكلمة سر من 8 أحرف تشمل حرفاً ورقماً.");
      return;
    }

    try {
      setStaffSaving(true);
      const res = await teacherService.createMyStaff({
        fullName: staffForm.fullName.trim(),
        phoneNumber: staffForm.phoneNumber.trim(),
        password: staffForm.password,
        notes: staffForm.notes.trim() || undefined,
        permissionKeys: staffForm.permissionKeys,
      });
      if (res.success && res.data) {
        setStaff((current) => [res.data, ...current]);
        setStaffForm({ fullName: "", phoneNumber: "", password: "", notes: "", permissionKeys: ["dashboard"] });
        toast.success("تم إضافة الاستاف وربطه بحسابك.");
      } else {
        toast.error(res.message || "تعذر إضافة الاستاف.");
      }
    } catch (error: any) {
      toast.error(error?.response?.data?.message || "تعذر إضافة الاستاف.");
    } finally {
      setStaffSaving(false);
    }
  };

  const toggleFormPermission = (permissionKey: string) => {
    setStaffForm((current) => ({
      ...current,
      permissionKeys: current.permissionKeys.includes(permissionKey)
        ? current.permissionKeys.filter((key) => key !== permissionKey)
        : [...current.permissionKeys, permissionKey],
    }));
  };

  const updateMemberPermissions = async (member: TeacherStaffMemberDto, permissionKey: string) => {
    const nextPermissions = member.permissionKeys.includes(permissionKey)
      ? member.permissionKeys.filter((key) => key !== permissionKey)
      : [...member.permissionKeys, permissionKey];

    try {
      setSavingPermissionsId(member.id);
      const res = await teacherService.setMyStaffPermissions(member.id, nextPermissions);
      if (res.success && res.data) {
        setStaff((current) => current.map((item) => item.id === member.id ? res.data : item));
        toast.success("تم تحديث صلاحيات الاستاف.");
      } else {
        toast.error(res.message || "تعذر تحديث الصلاحيات.");
      }
    } catch {
      toast.error("تعذر تحديث صلاحيات الاستاف.");
    } finally {
      setSavingPermissionsId(null);
    }
  };

  const toggleStaffStatus = async (member: TeacherStaffMemberDto) => {
    try {
      const res = await teacherService.setMyStaffStatus(member.id, !member.isActive);
      if (res.success && res.data) {
        setStaff((current) => current.map((item) => item.id === member.id ? res.data : item));
        toast.success(res.data.isActive ? "تم تفعيل الاستاف." : "تم إيقاف الاستاف.");
      }
    } catch {
      toast.error("تعذر تغيير حالة الاستاف.");
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      const res = await teacherService.updateMyProfile({
        bio,
        specialization,
        contactInfo,
        profileImageUrl: profileImageUrl || undefined,
        assistantPhoneNumbers: assistantPhoneNumbers || undefined,
        facebookUrl: facebookUrl || undefined,
        youtubeUrl: youtubeUrl || undefined,
        telegramUrl: telegramUrl || undefined,
      });
      if (res.success) toast.success("تم تحديث الملف الشخصي بنجاح");
      else toast.error(res.message || "حدث خطأ أثناء تحديث الملف الشخصي");
    } catch (err) {
      console.error(err);
      toast.error("فشل في تحديث الملف الشخصي");
    } finally {
      setSaving(false);
    }
  };

  const uploadProfileImage = async (file: File) => {
    setIsUploadingProfile(true);
    try {
      const base64 = await compressImage(file);
      const finalFileName = renameFileToMatchBase64(file.name, base64);
      setProfileImagePreview(base64);
      const res = await teacherService.uploadMyProfileImage(base64, finalFileName);
      if (res.success && res.data) {
        setProfileImageUrl(res.data);
        toast.success("تم رفع الصورة الشخصية بنجاح");
      } else {
        toast.error(res.message || "فشل رفع الصورة الشخصية");
      }
    } catch (err) {
      console.error(err);
      toast.error("حدث خطأ أثناء معالجة ورفع الصورة الشخصية");
    } finally {
      setIsUploadingProfile(false);
    }
  };

  const uploadAiPhoto = async (file: File) => {
    setIsUploadingAi(true);
    try {
      const base64 = await compressImage(file);
      const finalFileName = renameFileToMatchBase64(file.name, base64);
      setAiPhotoPreview(base64);
      const res = await teacherService.uploadMyAiPhoto(base64, finalFileName);
      if (res.success) toast.success("تم رفع صورة تحليل AI بنجاح");
      else toast.error(res.message || "فشل رفع صورة تحليل AI");
    } catch (err) {
      console.error(err);
      toast.error("حدث خطأ أثناء معالجة ورفع صورة التحليل");
    } finally {
      setIsUploadingAi(false);
    }
  };

  const staffColumns: AdminColumn<TeacherStaffMemberDto>[] = [
    {
      key: "member",
      label: "الاستاف",
      render: (member) => (
        <div>
          <p className="font-black text-[var(--admin-text)]">{member.fullName}</p>
          <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">{member.phoneNumber}</p>
          {member.notes ? <p className="mt-1 text-xs font-bold text-[var(--admin-primary)]">{member.notes}</p> : null}
          <div className="mt-3 flex max-w-xl flex-wrap gap-1.5">
            {member.permissionKeys.length > 0 ? member.permissionKeys.map((key) => (
              <span key={key} className="rounded-full bg-[var(--admin-primary)]/10 px-2.5 py-1 text-sm font-black text-[var(--admin-primary)]">
                {permissionLabel(key)}
              </span>
            )) : (
              <span className="rounded-full bg-rose-500/10 px-2.5 py-1 text-sm font-black text-rose-600">بدون صلاحيات</span>
            )}
          </div>
        </div>
      ),
    },
    {
      key: "permissions",
      label: "تعديل الصلاحيات",
      render: (member) => (
        <div className="grid min-w-72 grid-cols-1 gap-2 sm:grid-cols-2">
          {STAFF_PERMISSION_OPTIONS.map((permission) => {
            const checked = member.permissionKeys.includes(permission.key);
            return (
              <button
                key={permission.key}
                type="button"
                disabled={savingPermissionsId === member.id}
                onClick={() => updateMemberPermissions(member, permission.key)}
                className={`inline-flex items-center justify-between gap-2 rounded-xl border px-3 py-2 text-xs font-black transition disabled:opacity-60 ${
                  checked
                    ? "border-[var(--admin-primary)] bg-[var(--admin-primary)]/10 text-[var(--admin-primary)]"
                    : "border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)] hover:text-[var(--admin-text)]"
                }`}
              >
                <span>{permission.label}</span>
                <CheckSquare className={`h-4 w-4 ${checked ? "opacity-100" : "opacity-25"}`} />
              </button>
            );
          })}
        </div>
      ),
    },
    {
      key: "createdAt",
      label: "تاريخ الإضافة",
      render: (member) => (
        <span className="text-xs font-bold text-[var(--admin-muted)]">
          {new Intl.DateTimeFormat("ar-EG-u-nu-latn", { timeZone: 'Africa/Cairo', dateStyle: "medium" }).format(new Date(member.createdAt))}
        </span>
      ),
    },
    {
      key: "status",
      label: "الحالة",
      align: "center",
      render: (member) => (
        <span className={`inline-flex rounded-full px-3 py-1 text-xs font-black ${member.isActive ? "bg-emerald-500/10 text-emerald-600" : "bg-rose-500/10 text-rose-600"}`}>
          {member.isActive ? "نشط" : "موقوف"}
        </span>
      ),
    },
    {
      key: "action",
      label: "الإجراء",
      align: "left",
      render: (member) => (
        <button type="button" onClick={() => toggleStaffStatus(member)} className="admin-btn-ghost inline-flex items-center gap-2">
          <Power className="h-4 w-4" />
          {member.isActive ? "إيقاف" : "تفعيل"}
        </button>
      ),
    },
  ];

  if (loading) {
    return (
      <TeacherPage
        activePath="/teacher/profile"
        sectionLabel="الملف الشخصي"
        pageTitle="الملف الشخصي للمعلم"
        subtitle="تعديل سيرتك الذاتية وتفاصيل الاتصال الخاصة بك."
      >
        <div className="flex min-h-[400px] items-center justify-center" dir="rtl">
          <div className="flex flex-col items-center gap-4">
            <Loader2 className="h-10 w-10 animate-spin text-[var(--admin-primary)]" />
            <p className="text-sm text-[var(--admin-muted)]">جاري تحميل بيانات الملف الشخصي...</p>
          </div>
        </div>
      </TeacherPage>
    );
  }

  return (
    <TeacherPage
      activePath="/teacher/profile"
      sectionLabel="الملف الشخصي"
      pageTitle="الملف الشخصي للمعلم"
      subtitle="تعديل بياناتك، صورك، والاستاف التابع لحسابك."
    >
      <div className="space-y-8" dir="rtl">
        <section className="grid grid-cols-1 gap-6 md:grid-cols-3">
          <AdminStatCard variant="light" icon={User} label="حساب المدرس" value={specialization || "غير محدد"} subtitle="التخصص الظاهر للطلاب" />
        {isOwner ? <AdminStatCard variant="accent" icon={Users} label="استاف تابع" value={staff.length} subtitle="حسابات تعمل داخل نطاقك" /> : null}
          <AdminStatCard variant="muted" icon={ImageIcon} label="صور المدرس" value={profileImagePreview || aiPhotoPreview ? "مكتملة" : "تحتاج رفع"} subtitle="الصورة الشخصية وصورة AI" />
        </section>

        <AdminTabBar tabs={isOwner ? TABS : TABS.filter((tab) => tab.key !== "staff")} activeTab={activeTab} onSelect={setActiveTab} />

        {activeTab === "details" ? (
          <section className="admin-panel">
            <form onSubmit={handleSubmit} className="space-y-6">
              <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                <InputField icon={Bookmark} id="specialization" label="التخصص الدراسي / المادة">
                  <input
                    id="specialization"
                    type="text"
                    value={specialization}
                    onChange={(e) => setSpecialization(e.target.value)}
                    placeholder="مثال: خبير ومدرس اللغة العربية للثانوية العامة"
                    className="admin-input"
                    required
                  />
                </InputField>

                <InputField icon={Phone} id="assistantPhoneNumbers" label="أرقام هواتف المساعدين">
                  <input
                    id="assistantPhoneNumbers"
                    type="text"
                    value={assistantPhoneNumbers}
                    onChange={(e) => setAssistantPhoneNumbers(e.target.value)}
                    placeholder="01xxxxxxxxx, 01xxxxxxxxx"
                    className="admin-input"
                  />
                </InputField>
              </div>

              <InputField icon={Phone} id="contactInfo" label="معلومات الاتصال المباشر للطلاب">
                <input
                  id="contactInfo"
                  type="text"
                  value={contactInfo}
                  onChange={(e) => setContactInfo(e.target.value)}
                  placeholder="مثال: رقم الواتساب، الدعم الفني، أو البريد الإلكتروني..."
                  className="admin-input"
                  required
                />
              </InputField>

              <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-5">
                <h4 className="mb-4 flex items-center gap-2 text-sm font-bold text-[var(--admin-text)]">
                  <TelegramIcon className="h-4 w-4 text-[var(--admin-primary)]" />
                  روابط وسائل التواصل الاجتماعي
                </h4>
                <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
                  <SocialInput id="facebookUrl" label="رابط الفيسبوك" value={facebookUrl} onChange={setFacebookUrl} placeholder="https://facebook.com/..." />
                  <SocialInput id="youtubeUrl" label="رابط اليوتيوب" value={youtubeUrl} onChange={setYouTubeUrl} placeholder="https://youtube.com/..." />
                  <SocialInput id="telegramUrl" label="رابط التيليجرام" value={telegramUrl} onChange={setTelegramUrl} placeholder="https://t.me/..." />
                </div>
              </div>

              <InputField icon={FileText} id="bio" label="الوصف">
                <textarea
                  id="bio"
                  value={bio}
                  onChange={(e) => setBio(e.target.value)}
                  placeholder="اكتب وصفاً ترويجياً قصيراً يظهر للطلاب في صفحات الاشتراك والتفعيل..."
                  rows={5}
                  className="admin-input resize-none"
                  required
                />
              </InputField>

              <div className="flex justify-end border-t border-[var(--admin-border)] pt-4">
                <button type="submit" disabled={saving} className="admin-btn-primary inline-flex items-center gap-2">
                  {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
                  {saving ? "جاري الحفظ..." : "حفظ التعديلات"}
                </button>
              </div>
            </form>
          </section>
        ) : null}

        {activeTab === "images" ? (
          <section className="grid grid-cols-1 gap-6 md:grid-cols-2">
            <ImageUploadCard
              title="الصورة الشخصية الأساسية"
              icon={ImageIcon}
              preview={profileImagePreview}
              isUploading={isUploadingProfile}
              fallback={<User className="h-8 w-8" />}
              roundPreview
              onFile={uploadProfileImage}
            />
            <ImageUploadCard
              title="صورة التحليل للذكاء الاصطناعي"
              icon={Sparkles}
              preview={aiPhotoPreview}
              isUploading={isUploadingAi}
              fallback={<Sparkles className="h-8 w-8" />}
              onFile={uploadAiPhoto}
            />
          </section>
        ) : null}

        {isOwner && activeTab === "staff" ? (
          <section className="space-y-6">
            <div className="admin-panel">
              <h2 className="mb-4 flex items-center gap-2 text-lg font-black text-[var(--admin-text)]">
                <Users className="h-5 w-5 text-[var(--admin-primary)]" />
                إضافة استاف جديد
              </h2>
              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                <input aria-label="اسم الاستاف" maxLength={200} value={staffForm.fullName} onChange={(event) => setStaffForm({ ...staffForm, fullName: event.target.value })} placeholder="اسم الاستاف" className="admin-input" />
                <input aria-label="رقم الهاتف لتسجيل الدخول" inputMode="tel" maxLength={20} value={staffForm.phoneNumber} onChange={(event) => setStaffForm({ ...staffForm, phoneNumber: event.target.value })} placeholder="رقم الهاتف لتسجيل الدخول" className="admin-input" />
                <input aria-label="كلمة السر" type="password" minLength={8} autoComplete="new-password" value={staffForm.password} onChange={(event) => setStaffForm({ ...staffForm, password: event.target.value })} placeholder="كلمة السر: 8 أحرف تشمل حرفاً ورقماً" className="admin-input" />
                <input aria-label="ملاحظة أو دور الاستاف" maxLength={500} value={staffForm.notes} onChange={(event) => setStaffForm({ ...staffForm, notes: event.target.value })} placeholder="ملاحظة أو دوره عندك" className="admin-input" />
              </div>
              <div className="mt-5 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-4">
                <p className="mb-3 text-sm font-black text-[var(--admin-text)]">صلاحيات الاستاف</p>
                <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
                  {STAFF_PERMISSION_OPTIONS.map((permission) => {
                    const checked = staffForm.permissionKeys.includes(permission.key);
                    return (
                      <button
                        key={permission.key}
                        type="button"
                        aria-pressed={checked}
                        onClick={() => toggleFormPermission(permission.key)}
                        className={`inline-flex items-center justify-between gap-2 rounded-xl border px-3 py-2 text-xs font-black transition ${
                          checked
                            ? "border-[var(--admin-primary)] bg-[var(--admin-primary)]/10 text-[var(--admin-primary)]"
                            : "border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)] hover:text-[var(--admin-text)]"
                        }`}
                      >
                        <span>{permission.label}</span>
                        <CheckSquare className={`h-4 w-4 ${checked ? "opacity-100" : "opacity-25"}`} />
                      </button>
                    );
                  })}
                </div>
              </div>
              <div className="mt-4 flex justify-end">
                <button type="button" onClick={handleCreateStaff} disabled={staffSaving} className="admin-btn-primary inline-flex items-center gap-2">
                  {staffSaving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Plus className="h-4 w-4" />}
                  إضافة الاستاف
                </button>
              </div>
            </div>

            <AdminDataTable
              data={staff}
              columns={staffColumns}
              loading={staffLoading}
              rowKey={(member) => member.id}
              emptyMessage="لم تضف استاف بعد."
            />
          </section>
        ) : null}
      </div>
    </TeacherPage>
  );
}

function InputField({
  icon: Icon,
  id,
  label,
  children,
}: {
  icon: typeof User;
  id: string;
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-2">
      <label htmlFor={id} className="flex items-center gap-2 text-sm font-bold text-[var(--admin-text)]">
        <Icon className="h-4 w-4 text-[var(--admin-primary)]" />
        {label}
      </label>
      {children}
    </div>
  );
}

function SocialInput({
  id,
  label,
  value,
  onChange,
  placeholder,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  placeholder: string;
}) {
  return (
    <label className="grid gap-2 text-xs font-bold text-[var(--admin-text)]" htmlFor={id}>
      {label}
      <input
        id={id}
        type="url"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        className="admin-input text-xs"
      />
    </label>
  );
}

function ImageUploadCard({
  title,
  icon: Icon,
  preview,
  isUploading,
  fallback,
  roundPreview = false,
  onFile,
}: {
  title: string;
  icon: typeof User;
  preview: string | null;
  isUploading: boolean;
  fallback: React.ReactNode;
  roundPreview?: boolean;
  onFile: (file: File) => Promise<void>;
}) {
  return (
    <div className="admin-panel">
      <div className="mb-4 flex items-center gap-2 text-sm font-bold text-[var(--admin-text)]">
        <Icon className="h-4 w-4 text-[var(--admin-primary)]" />
        {title}
      </div>
      <label className="relative flex min-h-[220px] cursor-pointer flex-col items-center justify-center rounded-2xl border-2 border-dashed border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-5 transition hover:border-[var(--admin-primary)] focus-within:border-[var(--admin-primary)] focus-within:ring-2 focus-within:ring-[var(--admin-primary)]">
        <input
          type="file"
          accept="image/*"
          aria-label={`رفع ${title}`}
          className="absolute inset-0 cursor-pointer opacity-0 disabled:cursor-not-allowed"
          disabled={isUploading}
          onChange={(event) => {
            const file = event.target.files?.[0];
            if (file) void onFile(file);
          }}
        />
        {preview ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={resolveMediaUrl(preview)}
            alt={`معاينة ${title}`}
            className={`h-28 w-28 border border-[var(--admin-border)] object-cover shadow-sm ${roundPreview ? "rounded-full" : "rounded-2xl"}`}
          />
        ) : (
          <div className={`flex h-28 w-28 items-center justify-center bg-[var(--admin-primary-15)] text-[var(--admin-primary)] ${roundPreview ? "rounded-full" : "rounded-2xl"}`}>
            {fallback}
          </div>
        )}
        <span className="mt-3 text-xs font-bold text-[var(--admin-muted)]">
          {isUploading ? "جاري الرفع..." : "اسحب صورة أو انقر للرفع"}
        </span>
      </label>
    </div>
  );
}
