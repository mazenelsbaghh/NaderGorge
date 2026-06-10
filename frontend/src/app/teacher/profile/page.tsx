"use client";

import { useState, useEffect } from "react";
import { User, Sparkles, Loader2, Save, FileText, Bookmark, Phone, Mail, Upload, Image as ImageIcon } from "lucide-react";
import { teacherService } from "@/services/teacher-service";
import toast from "react-hot-toast";
import { resolveMediaUrl } from "@/utils/resolve-media-url";
import { compressImage } from "@/utils/image-compressor";

import { TeacherShellChrome } from "@/components/teacher/TeacherShellChrome";

const FacebookIcon = (props: React.SVGProps<SVGSVGElement>) => (
  <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
    <path d="M18 2h-3a5 5 0 0 0-5 5v3H7v4h3v8h4v-8h3l1-4h-4V7a1 1 0 0 1 1-1h3z"/>
  </svg>
);

const YoutubeIcon = (props: React.SVGProps<SVGSVGElement>) => (
  <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
    <path d="M22.54 6.42a2.78 2.78 0 0 0-1.94-2C18.88 4 12 4 12 4s-6.88 0-8.6.46a2.78 2.78 0 0 0-1.94 2A29 29 0 0 0 1 11.75a29 29 0 0 0 .46 5.33A2.78 2.78 0 0 0 3.4 19c1.72.46 8.6.46 8.6.46s6.88 0 8.6-.46a2.78 2.78 0 0 0 1.94-2 29 29 0 0 0 .46-5.25 29 29 0 0 0-.46-5.33z"/>
    <polygon points="9.75 15.02 15.5 11.75 9.75 8.48 9.75 15.02"/>
  </svg>
);

const TelegramIcon = (props: React.SVGProps<SVGSVGElement>) => (
  <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
    <path d="m22 2-7 20-4-9-9-4Z"/>
    <path d="M22 2 11 13"/>
  </svg>
);

export default function TeacherProfilePage() {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  // Form State
  const [bio, setBio] = useState("");
  const [specialization, setSpecialization] = useState("");
  const [contactInfo, setContactInfo] = useState("");
  const [profileImageUrl, setProfileImageUrl] = useState("");
  const [assistantPhoneNumbers, setAssistantPhoneNumbers] = useState("");
  const [facebookUrl, setFacebookUrl] = useState("");
  const [youtubeUrl, setYouTubeUrl] = useState("");
  const [telegramUrl, setTelegramUrl] = useState("");

  // Upload previews and loading states
  const [profileImagePreview, setProfileImagePreview] = useState<string | null>(null);
  const [isUploadingProfile, setIsUploadingProfile] = useState(false);
  const [aiPhotoPreview, setAiPhotoPreview] = useState<string | null>(null);
  const [isUploadingAi, setIsUploadingAi] = useState(false);

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
  }, []);

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
      if (res.success) {
        toast.success("تم تحديث الملف الشخصي بنجاح");
      } else {
        toast.error(res.message || "حدث خطأ أثناء تحديث الملف الشخصي");
      }
    } catch (err) {
      console.error(err);
      toast.error("فشل في تحديث الملف الشخصي");
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <TeacherShellChrome
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
      </TeacherShellChrome>
    );
  }

  return (
    <TeacherShellChrome
      activePath="/teacher/profile"
      sectionLabel="الملف الشخصي"
      pageTitle="الملف الشخصي للمعلم"
      subtitle="تعديل سيرتك الذاتية وتفاصيل الاتصال الخاصة بك."
    >
      <div className="space-y-8 animate-[fadeIn_0.4s_ease-out]" dir="rtl">
        {/* Profile Card Header */}
        <div className="relative overflow-hidden rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-8 shadow-[0_12px_40px_var(--admin-shadow)] backdrop-blur-2xl">
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_80%_10%,var(--admin-primary-15),transparent_42%)]" />
          <div className="relative flex flex-col md:flex-row md:items-center justify-between gap-6">
            <div className="flex items-center gap-6">
              <div className="relative">
                {profileImageUrl ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img
                    src={resolveMediaUrl(profileImageUrl)}
                    alt="Avatar"
                    className="h-20 w-20 rounded-[1.5rem] object-cover border border-[var(--admin-border)] shadow-md"
                    onError={(e) => {
                      (e.target as HTMLImageElement).src = "https://placehold.co/150?text=Teacher";
                    }}
                  />
                ) : (
                  <div className="flex h-20 w-20 items-center justify-center rounded-[1.5rem] bg-[var(--admin-primary-15)] text-[var(--admin-primary)] shadow-md">
                    <User className="h-10 w-10" />
                  </div>
                )}
              </div>
              <div>
                <div className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-primary-15)] px-4 py-1 text-xs font-black text-[var(--admin-primary)]">
                  <Sparkles className="h-3.5 w-3.5" />
                  إعدادات المعلم
                </div>
                <h1 className="mt-4 text-3xl font-black text-[var(--admin-text)] md:text-4xl">
                  الملف الشخصي
                </h1>
                <p className="mt-2 text-sm text-[var(--admin-muted)]">
                  قم بتحديث سيرتك الذاتية وصورتك الشخصية ومعلومات تواصل الطلاب معك.
                </p>
              </div>
            </div>
          </div>
        </div>

        {/* Profile form */}
        <div className="rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-8 shadow-[0_12px_40px_var(--admin-shadow)] backdrop-blur-2xl">
          <form onSubmit={handleSubmit} className="space-y-6">
            <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
              {/* Specialization */}
              <div className="space-y-2">
                <label htmlFor="specialization" className="flex items-center gap-2 text-sm font-bold text-[var(--admin-text)]">
                  <Bookmark className="h-4 w-4 text-[var(--admin-primary)]" />
                  التخصص الدراسي / المادة
                </label>
                <input
                  id="specialization"
                  type="text"
                  value={specialization}
                  onChange={(e) => setSpecialization(e.target.value)}
                  placeholder="مثال: خبير ومدرس اللغة العربية للثانوية العامة"
                  className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none transition focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)]"
                  required
                />
              </div>

              {/* Assistant numbers */}
              <div className="space-y-2">
                <label htmlFor="assistantPhoneNumbers" className="flex items-center gap-2 text-sm font-bold text-[var(--admin-text)]">
                  <Phone className="h-4 w-4 text-[var(--admin-primary)]" />
                  أرقام هواتف المساعدين (اختياري)
                </label>
                <input
                  id="assistantPhoneNumbers"
                  type="text"
                  value={assistantPhoneNumbers}
                  onChange={(e) => setAssistantPhoneNumbers(e.target.value)}
                  placeholder="01xxxxxxxxx, 01xxxxxxxxx"
                  className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none transition focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)]"
                />
              </div>
            </div>

            {/* Contact Info */}
            <div className="space-y-2">
              <label htmlFor="contactInfo" className="flex items-center gap-2 text-sm font-bold text-[var(--admin-text)]">
                <Phone className="h-4 w-4 text-[var(--admin-primary)]" />
                معلومات الاتصال المباشر للطلاب
              </label>
              <input
                id="contactInfo"
                type="text"
                value={contactInfo}
                onChange={(e) => setContactInfo(e.target.value)}
                placeholder="مثال: رقم الواتساب، الدعم الفني، أو البريد الإلكتروني..."
                className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none transition focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)]"
                required
              />
            </div>

            <div className="rounded-[1.5rem] border border-[var(--admin-border)] bg-[var(--admin-bg)] p-6 space-y-4">
              <h4 className="text-sm font-bold text-[var(--admin-text)] flex items-center gap-2 mb-2">
                <TelegramIcon className="h-4 w-4 text-[var(--admin-primary)]" />
                روابط وسائل التواصل الاجتماعي (اختياري)
              </h4>
              <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
                <div className="space-y-2">
                  <label htmlFor="facebookUrl" className="block text-xs font-bold text-[var(--admin-text)]">رابط الفيسبوك</label>
                  <input
                    id="facebookUrl"
                    type="url"
                    value={facebookUrl}
                    onChange={(e) => setFacebookUrl(e.target.value)}
                    placeholder="https://facebook.com/..."
                    className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-xs text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none transition focus:border-[var(--admin-primary)]"
                  />
                </div>
                <div className="space-y-2">
                  <label htmlFor="youtubeUrl" className="block text-xs font-bold text-[var(--admin-text)]">رابط اليوتيوب</label>
                  <input
                    id="youtubeUrl"
                    type="url"
                    value={youtubeUrl}
                    onChange={(e) => setYouTubeUrl(e.target.value)}
                    placeholder="https://youtube.com/..."
                    className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-xs text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none transition focus:border-[var(--admin-primary)]"
                  />
                </div>
                <div className="space-y-2">
                  <label htmlFor="telegramUrl" className="block text-xs font-bold text-[var(--admin-text)]">رابط التيليجرام</label>
                  <input
                    id="telegramUrl"
                    type="url"
                    value={telegramUrl}
                    onChange={(e) => setTelegramUrl(e.target.value)}
                    placeholder="https://t.me/..."
                    className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-xs text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none transition focus:border-[var(--admin-primary)]"
                  />
                </div>
              </div>
            </div>

            {/* Images Upload Area */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6 rounded-[1.5rem] border border-[var(--admin-border)] bg-[var(--admin-bg)] p-6">
              {/* Main Profile Image Upload */}
              <div className="space-y-3">
                <label className="block text-sm font-bold text-[var(--admin-text)] flex items-center gap-2">
                  <ImageIcon className="h-4 w-4 text-[var(--admin-primary)]" />
                  الصورة الشخصية الأساسية
                </label>
                <div className="flex flex-col items-center justify-center border-2 border-dashed border-[var(--admin-border)] rounded-2xl p-4 bg-[var(--admin-card)] hover:border-[var(--admin-primary)] transition relative min-h-[160px]">
                  <input
                    type="file"
                    accept="image/*"
                    className="absolute inset-0 opacity-0 cursor-pointer"
                    disabled={isUploadingProfile}
                    onChange={async (e) => {
                      const file = e.target.files?.[0];
                      if (!file) return;
                      setIsUploadingProfile(true);
                      try {
                        const base64 = await compressImage(file);
                        setProfileImagePreview(base64);
                        const res = await teacherService.uploadMyProfileImage(base64, file.name);
                        if (res.success && res.data) {
                          setProfileImageUrl(res.data);
                          toast.success("تم رفع الصورة الشخصية بنجاح ✅");
                        } else {
                          toast.error(res.message || "فشل رفع الصورة الشخصية");
                        }
                      } catch (err) {
                        console.error(err);
                        toast.error("حدث خطأ أثناء معالجة ورفع الصورة الشخصية");
                      } finally {
                        setIsUploadingProfile(false);
                      }
                    }}
                  />
                  {profileImagePreview ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img
                      src={resolveMediaUrl(profileImagePreview)}
                      alt="Profile Preview"
                      className="h-24 w-24 rounded-full object-cover border border-[var(--admin-border)] shadow-sm"
                    />
                  ) : (
                    <div className="flex h-24 w-24 items-center justify-center rounded-full bg-[var(--admin-primary-15)] text-[var(--admin-primary)] font-bold text-xl">
                      <User className="h-8 w-8" />
                    </div>
                  )}
                  <span className="text-[10px] text-[var(--admin-muted)] mt-2">
                    {isUploadingProfile ? "جاري الرفع..." : "اسحب صورة أو انقر للرفع"}
                  </span>
                </div>
              </div>

              {/* AI Photo Upload */}
              <div className="space-y-3">
                <label className="block text-sm font-bold text-[var(--admin-text)] flex items-center gap-2">
                  <Sparkles className="h-4 w-4 text-[var(--admin-primary)]" />
                  صورة التحليل للذكاء الاصطناعي (AI)
                </label>
                <div className="flex flex-col items-center justify-center border-2 border-dashed border-[var(--admin-border)] rounded-2xl p-4 bg-[var(--admin-card)] hover:border-[var(--admin-primary)] transition relative min-h-[160px]">
                  <input
                    type="file"
                    accept="image/*"
                    className="absolute inset-0 opacity-0 cursor-pointer"
                    disabled={isUploadingAi}
                    onChange={async (e) => {
                      const file = e.target.files?.[0];
                      if (!file) return;
                      setIsUploadingAi(true);
                      try {
                        const base64 = await compressImage(file);
                        setAiPhotoPreview(base64);
                        const res = await teacherService.uploadMyAiPhoto(base64, file.name);
                        if (res.success) {
                          toast.success("تم رفع صورة تحليل الـ AI بنجاح ✅");
                        } else {
                          toast.error(res.message || "فشل رفع صورة تحليل الـ AI");
                        }
                      } catch (err) {
                        console.error(err);
                        toast.error("حدث خطأ أثناء معالجة ورفع صورة التحليل");
                      } finally {
                        setIsUploadingAi(false);
                      }
                    }}
                  />
                  {aiPhotoPreview ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img
                      src={aiPhotoPreview}
                      alt="AI Preview"
                      className="h-24 w-24 rounded-2xl object-cover border border-[var(--admin-border)] shadow-sm"
                    />
                  ) : (
                    <div className="flex h-24 w-24 items-center justify-center rounded-2xl bg-[var(--admin-bg)] text-[var(--admin-muted)]">
                      <Sparkles className="h-8 w-8" />
                    </div>
                  )}
                  <span className="text-[10px] text-[var(--admin-muted)] mt-2">
                    {isUploadingAi ? "جاري الرفع..." : "اسحب صورة أو انقر للرفع"}
                  </span>
                </div>
              </div>
            </div>

            {/* Description (bio) */}
            <div className="space-y-2">
              <label htmlFor="bio" className="flex items-center gap-2 text-sm font-bold text-[var(--admin-text)]">
                <FileText className="h-4 w-4 text-[var(--admin-primary)]" />
                الوصف
              </label>
              <textarea
                id="bio"
                value={bio}
                onChange={(e) => setBio(e.target.value)}
                placeholder="اكتب وصفاً ترويجياً قصيراً يظهر للطلاب في صفحات الاشتراك والتفعيل..."
                rows={5}
                className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none transition focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] resize-none"
                required
              />
            </div>

            {/* Action button */}
            <div className="flex justify-end pt-4">
              <button
                type="submit"
                disabled={saving}
                className="inline-flex items-center gap-2 rounded-2xl bg-[var(--admin-primary)] px-6 py-3 text-sm font-bold text-[var(--admin-primary-contrast)] shadow-lg shadow-[var(--admin-primary-15)] transition hover:bg-[var(--admin-primary)]/90 hover:scale-[1.02] active:scale-[0.98] disabled:opacity-50 disabled:pointer-events-none"
              >
                {saving ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" />
                    جاري الحفظ...
                  </>
                ) : (
                  <>
                    <Save className="h-4 w-4" />
                    حفظ التعديلات
                  </>
                )}
              </button>
            </div>
          </form>
        </div>
      </div>
    </TeacherShellChrome>
  );
}
