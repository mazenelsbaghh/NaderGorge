"use client";

import { useState, useEffect } from "react";
import { motion } from "framer-motion";
import { User, Smartphone, MapPin, School, Phone, CheckCircle2, AlertCircle, Sparkles } from "lucide-react";
import { studentService, StudentProfileDto, UpdateStudentProfileDto } from "@/services/student-service";
import { useStudentTheme } from "@/hooks/useStudentTheme";
import { fadeSlideUp } from "@/lib/motion";

export default function StudentProfilePage() {
  const { isReady } = useStudentTheme();
  const [profile, setProfile] = useState<StudentProfileDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);

  // Form states
  const [address, setAddress] = useState("");
  const [secondaryPhone, setSecondaryPhone] = useState("");
  const [parentPhone, setParentPhone] = useState("");
  const [secondaryParentPhone, setSecondaryParentPhone] = useState("");
  const [motherPhone, setMotherPhone] = useState("");
  const [schoolName, setSchoolName] = useState("");

  useEffect(() => {
    studentService.getProfile()
      .then((res) => {
        setProfile(res);
        setAddress(res.address || "");
        setSecondaryPhone(res.secondaryPhone || "");
        setParentPhone(res.parentPhone || "");
        setSecondaryParentPhone(res.secondaryParentPhone || "");
        setMotherPhone(res.motherPhone || "");
        setSchoolName(res.schoolName || "");
      })
      .catch((err) => console.error("Error fetching profile:", err))
      .finally(() => setLoading(false));
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setMessage(null);

    const payload: UpdateStudentProfileDto = {
      address,
      secondaryPhone: secondaryPhone || null,
      parentPhone: parentPhone || null,
      secondaryParentPhone: secondaryParentPhone || null,
      motherPhone: motherPhone || null,
      schoolName: schoolName || null,
    };

    try {
      await studentService.updateProfile(payload);
      setMessage({ type: "success", text: "تم تحديث الملف الشخصي بنجاح!" });
      // Refresh profile data
      const updatedProfile = await studentService.getProfile();
      setProfile(updatedProfile);
    } catch (err: any) {
      console.error("Error updating profile:", err);
      setMessage({ type: "error", text: err?.response?.data?.message || "فشل في تحديث الملف الشخصي، يرجى المحاولة لاحقاً." });
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex h-[60vh] items-center justify-center" dir="rtl">
        <div className="text-center space-y-4">
          <div className="h-12 w-12 border-4 border-[var(--admin-primary)] border-t-transparent rounded-full animate-spin mx-auto"></div>
          <p className="text-sm text-[var(--admin-muted)]">جاري تحميل بيانات الملف الشخصي...</p>
        </div>
      </div>
    );
  }

  if (!profile) {
    return (
      <div className="text-center py-12" dir="rtl">
        <AlertCircle className="h-12 w-12 text-rose-500 mx-auto mb-4" />
        <h3 className="text-lg font-bold text-[var(--admin-text)]">فشل تحميل الملف الشخصي</h3>
      </div>
    );
  }

  // Helper translations for stages/grades
  const translateStage = (stage: string) => {
    switch (stage.toLowerCase()) {
      case "primary": return "المرحلة الابتدائية";
      case "preparatory": return "المرحلة الإعدادية";
      case "secondary": return "المرحلة الثانوية";
      default: return stage;
    }
  };

  const translateGrade = (grade: string) => {
    switch (grade.toLowerCase()) {
      case "first": return "الصف الأول";
      case "second": return "الصف الثاني";
      case "third": return "الصف الثالث";
      default: return grade;
    }
  };

  const translateTrack = (track: string | null) => {
    if (!track) return "";
    switch (track.toLowerCase()) {
      case "general": return "عام";
      case "scientific": return "علمي";
      case "literary": return "أدبي";
      case "scientificmath": return "علمي رياضة";
      case "scientificscience": return "علمي علوم";
      default: return track;
    }
  };

  return (
    <motion.div
      className="space-y-8 max-w-5xl mx-auto"
      variants={fadeSlideUp}
      initial="hidden"
      animate={isReady ? "show" : undefined}
      dir="rtl"
    >
      {/* Header Banner */}
      <div className="relative overflow-hidden rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-8 shadow-[0_12px_40px_var(--admin-shadow)] backdrop-blur-2xl">
        <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_80%_10%,var(--admin-primary-15),transparent_42%)]" />
        <div className="relative flex flex-col md:flex-row md:items-center justify-between gap-6">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-primary-15)] px-4 py-1 text-xs font-black text-[var(--admin-primary)]">
              <Sparkles className="h-3.5 w-3.5" />
              الملف الشخصي للطالب
            </div>
            <h1 className="mt-4 text-3xl font-black text-[var(--admin-text)] md:text-4xl">
              إعدادات حسابك الشخصي
            </h1>
            <p className="mt-2 text-sm text-[var(--admin-muted)]">
              راجع بيانات تسجيلك الأكاديمي وقم بتحديث معلومات الاتصال والمدارس الخاصة بك.
            </p>
          </div>
        </div>
      </div>

      {message && (
        <div
          className={`p-4 rounded-2xl flex items-center gap-3 border ${
            message.type === "success"
              ? "bg-emerald-500/10 border-emerald-500/20 text-emerald-500"
              : "bg-rose-500/10 border-rose-500/20 text-rose-500"
          }`}
        >
          {message.type === "success" ? <CheckCircle2 className="h-5 w-5" /> : <AlertCircle className="h-5 w-5" />}
          <span className="text-sm font-bold">{message.text}</span>
        </div>
      )}

      <div className="grid grid-cols-1 gap-8 lg:grid-cols-3">
        {/* Left Column: Read-Only Registration Info & Devices */}
        <div className="space-y-6 lg:col-span-1">
          <div className="rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-xl space-y-6">
            <div className="text-center pb-6 border-b border-[var(--admin-border)]">
              <div className="inline-flex p-4 rounded-full bg-[var(--admin-primary-15)] text-[var(--admin-primary)] mb-3">
                <User className="h-10 w-10" />
              </div>
              <h3 className="text-lg font-black text-[var(--admin-text)]">{profile.fullName}</h3>
              <p className="text-xs text-[var(--admin-muted)] mt-1">{profile.phoneNumber}</p>
            </div>

            {/* Academic Info */}
            <div className="space-y-4">
              <h4 className="text-xs font-black text-[var(--admin-muted)] uppercase tracking-wider">البيانات الأكاديمية</h4>
              <div className="space-y-3 text-sm">
                <div className="flex justify-between py-1 border-b border-[var(--admin-border)]/50">
                  <span className="text-[var(--admin-muted)]">المرحلة الدراسية:</span>
                  <span className="font-semibold text-[var(--admin-text)]">{translateStage(profile.educationStage)}</span>
                </div>
                <div className="flex justify-between py-1 border-b border-[var(--admin-border)]/50">
                  <span className="text-[var(--admin-muted)]">الصف الدراسي:</span>
                  <span className="font-semibold text-[var(--admin-text)]">{translateGrade(profile.gradeLevel)}</span>
                </div>
                {profile.studyTrack && (
                  <div className="flex justify-between py-1 border-b border-[var(--admin-border)]/50">
                    <span className="text-[var(--admin-muted)]">التخصص/الشعبة:</span>
                    <span className="font-semibold text-[var(--admin-text)]">{translateTrack(profile.studyTrack)}</span>
                  </div>
                )}
                <div className="flex justify-between py-1 border-b border-[var(--admin-border)]/50">
                  <span className="text-[var(--admin-muted)]">المحافظة:</span>
                  <span className="font-semibold text-[var(--admin-text)]">{profile.governorate}</span>
                </div>
                {profile.district && (
                  <div className="flex justify-between py-1 border-b border-[var(--admin-border)]/50">
                    <span className="text-[var(--admin-muted)]">المركز/المنطقة:</span>
                    <span className="font-semibold text-[var(--admin-text)]">{profile.district}</span>
                  </div>
                )}
                <div className="flex justify-between py-1">
                  <span className="text-[var(--admin-muted)]">تاريخ الميلاد:</span>
                  <span className="font-semibold text-[var(--admin-text)]">{profile.dateOfBirth}</span>
                </div>
              </div>
            </div>

            {/* Device Limits */}
            <div className="pt-6 border-t border-[var(--admin-border)] space-y-3">
              <h4 className="text-xs font-black text-[var(--admin-muted)] uppercase tracking-wider flex items-center gap-1.5">
                <Smartphone className="h-4 w-4 text-[var(--admin-primary)]" />
                الأجهزة النشطة والحد المسموح
              </h4>
              <div className="bg-[var(--admin-bg)] p-4 rounded-2xl border border-[var(--admin-border)] space-y-2">
                <div className="flex justify-between text-xs">
                  <span className="text-[var(--admin-muted)]">عدد الأجهزة النشطة حالياً:</span>
                  <span className="font-black text-[var(--admin-text)]">{profile.deviceCount} من {profile.maxDevices}</span>
                </div>
                <div className="w-full bg-[var(--admin-border)] h-2 rounded-full overflow-hidden">
                  <div
                    className={`h-full rounded-full transition-all duration-500 ${
                      profile.deviceCount >= profile.maxDevices ? "bg-amber-500" : "bg-[var(--admin-primary)]"
                    }`}
                    style={{ width: `${Math.min(100, (profile.deviceCount / profile.maxDevices) * 100)}%` }}
                  />
                </div>
                <p className="text-[10px] text-[var(--admin-muted)] leading-relaxed mt-2">
                  يسمح النظام بربط حسابك بعدد أجهزة محدد لمنع مشاركة الحساب. يرجى التواصل مع الدعم الفني في حال رغبتك بتغيير جهازك النشط.
                </p>
              </div>
            </div>
          </div>
        </div>

        {/* Right Column: Editable Contact & Parent Info Forms */}
        <div className="lg:col-span-2">
          <form onSubmit={handleSubmit} className="rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-xl space-y-6">
            <h3 className="text-xl font-black text-[var(--admin-text)] font-tajawal pb-3 border-b border-[var(--admin-border)]">تحديث معلومات الاتصال والمدارس</h3>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              {/* Address */}
              <div className="md:col-span-2 space-y-2">
                <label className="text-xs font-black text-[var(--admin-text)] flex items-center gap-1">
                  <MapPin className="h-3.5 w-3.5 text-[var(--admin-primary)]" />
                  العنوان التفصيلي <span className="text-rose-500">*</span>
                </label>
                <input
                  type="text"
                  required
                  value={address}
                  onChange={(e) => setAddress(e.target.value)}
                  className="w-full bg-[var(--admin-bg)] border border-[var(--admin-border)] rounded-2xl px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] focus:outline-none focus:border-[var(--admin-primary)] transition"
                  placeholder="مثال: 12 شارع الجمهورية، الدور الثالث، بجوار مسجد..."
                />
              </div>

              {/* School Name */}
              <div className="space-y-2">
                <label className="text-xs font-black text-[var(--admin-text)] flex items-center gap-1">
                  <School className="h-3.5 w-3.5 text-[var(--admin-primary)]" />
                  اسم المدرسة
                </label>
                <input
                  type="text"
                  value={schoolName}
                  onChange={(e) => setSchoolName(e.target.value)}
                  className="w-full bg-[var(--admin-bg)] border border-[var(--admin-border)] rounded-2xl px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] focus:outline-none focus:border-[var(--admin-primary)] transition"
                  placeholder="اسم المدرسة المقيد بها"
                />
              </div>

              {/* Secondary Phone */}
              <div className="space-y-2">
                <label className="text-xs font-black text-[var(--admin-text)] flex items-center gap-1">
                  <Phone className="h-3.5 w-3.5 text-[var(--admin-primary)]" />
                  رقم الهاتف الإضافي
                </label>
                <input
                  type="text"
                  value={secondaryPhone}
                  onChange={(e) => setSecondaryPhone(e.target.value)}
                  className="w-full bg-[var(--admin-bg)] border border-[var(--admin-border)] rounded-2xl px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] focus:outline-none focus:border-[var(--admin-primary)] transition text-left"
                  dir="ltr"
                  placeholder="01xxxxxxxxx"
                />
              </div>

              {/* Parent Phone */}
              <div className="space-y-2">
                <label className="text-xs font-black text-[var(--admin-text)] flex items-center gap-1">
                  <Phone className="h-3.5 w-3.5 text-[var(--admin-primary)]" />
                  رقم هاتف ولي الأمر (الأب)
                </label>
                <input
                  type="text"
                  value={parentPhone}
                  onChange={(e) => setParentPhone(e.target.value)}
                  className="w-full bg-[var(--admin-bg)] border border-[var(--admin-border)] rounded-2xl px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] focus:outline-none focus:border-[var(--admin-primary)] transition text-left"
                  dir="ltr"
                  placeholder="01xxxxxxxxx"
                />
              </div>

              {/* Secondary Parent Phone */}
              <div className="space-y-2">
                <label className="text-xs font-black text-[var(--admin-text)] flex items-center gap-1">
                  <Phone className="h-3.5 w-3.5 text-[var(--admin-primary)]" />
                  رقم هاتف ولي الأمر الإضافي
                </label>
                <input
                  type="text"
                  value={secondaryParentPhone}
                  onChange={(e) => setSecondaryParentPhone(e.target.value)}
                  className="w-full bg-[var(--admin-bg)] border border-[var(--admin-border)] rounded-2xl px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] focus:outline-none focus:border-[var(--admin-primary)] transition text-left"
                  dir="ltr"
                  placeholder="01xxxxxxxxx"
                />
              </div>

              {/* Mother Phone */}
              <div className="space-y-2">
                <label className="text-xs font-black text-[var(--admin-text)] flex items-center gap-1">
                  <Phone className="h-3.5 w-3.5 text-[var(--admin-primary)]" />
                  رقم هاتف الأم
                </label>
                <input
                  type="text"
                  value={motherPhone}
                  onChange={(e) => setMotherPhone(e.target.value)}
                  className="w-full bg-[var(--admin-bg)] border border-[var(--admin-border)] rounded-2xl px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] focus:outline-none focus:border-[var(--admin-primary)] transition text-left"
                  dir="ltr"
                  placeholder="01xxxxxxxxx"
                />
              </div>
            </div>

            <div className="pt-6 border-t border-[var(--admin-border)] flex justify-end">
              <button
                type="submit"
                disabled={saving}
                className="bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] font-black text-sm px-8 py-3.5 rounded-2xl shadow-lg hover:brightness-110 active:scale-95 transition disabled:opacity-50 disabled:pointer-events-none"
              >
                {saving ? "جاري حفظ التغييرات..." : "حفظ التغييرات"}
              </button>
            </div>
          </form>
        </div>
      </div>
    </motion.div>
  );
}
