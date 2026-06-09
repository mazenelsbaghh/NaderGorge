"use client";

import { useState, useEffect } from "react";
import { User, Sparkles, Loader2, Save, FileText, Bookmark, Phone, Image as ImageIcon } from "lucide-react";
import { teacherService } from "@/services/teacher-service";
import toast from "react-hot-toast";

export default function TeacherProfilePage() {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  // Form State
  const [bio, setBio] = useState("");
  const [specialization, setSpecialization] = useState("");
  const [contactInfo, setContactInfo] = useState("");
  const [profileImageUrl, setProfileImageUrl] = useState("");

  useEffect(() => {
    teacherService.getMyProfile()
      .then((res) => {
        if (res.success) {
          setBio(res.data.bio || "");
          setSpecialization(res.data.specialization || "");
          setContactInfo(res.data.contactInfo || "");
          setProfileImageUrl(res.data.profileImageUrl || "");
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
        profileImageUrl: profileImageUrl || undefined
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
      <div className="flex min-h-[400px] items-center justify-center" dir="rtl">
        <div className="flex flex-col items-center gap-4">
          <Loader2 className="h-10 w-10 animate-spin text-[var(--admin-primary)]" />
          <p className="text-sm text-[var(--admin-muted)]">جاري تحميل بيانات الملف الشخصي...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-8 animate-[fadeIn_0.4s_ease-out]" dir="rtl">
      {/* Header section */}
      <div className="relative overflow-hidden rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-8 shadow-[0_12px_40px_var(--admin-shadow)] backdrop-blur-2xl">
        <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_80%_10%,var(--admin-primary-15),transparent_42%)]" />
        <div className="relative flex flex-col md:flex-row md:items-center justify-between gap-6">
          <div className="flex items-center gap-6">
            <div className="relative">
              {profileImageUrl ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  src={profileImageUrl}
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

            {/* Avatar image url */}
            <div className="space-y-2">
              <label htmlFor="profileImageUrl" className="flex items-center gap-2 text-sm font-bold text-[var(--admin-text)]">
                <ImageIcon className="h-4 w-4 text-[var(--admin-primary)]" />
                رابط الصورة الشخصية
              </label>
              <input
                id="profileImageUrl"
                type="url"
                value={profileImageUrl}
                onChange={(e) => setProfileImageUrl(e.target.value)}
                placeholder="https://example.com/avatar.jpg"
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

          {/* Bio */}
          <div className="space-y-2">
            <label htmlFor="bio" className="flex items-center gap-2 text-sm font-bold text-[var(--admin-text)]">
              <FileText className="h-4 w-4 text-[var(--admin-primary)]" />
              نبذة تعريفية وسيرة ذاتية (Bio)
            </label>
            <textarea
              id="bio"
              value={bio}
              onChange={(e) => setBio(e.target.value)}
              placeholder="اكتب نبذة عن خبراتك الأكاديمية والمهنية لتظهر للطلاب في صفحات الاشتراك والتفعيل..."
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
  );
}
