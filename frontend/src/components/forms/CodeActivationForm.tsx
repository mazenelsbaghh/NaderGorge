"use client";

import { useState } from "react";
import type { FormEvent } from "react";

import { AxiosError } from "axios";
import { ArrowUpLeft, KeyRound } from "lucide-react";
import { motion } from "framer-motion";
import Image from "next/image";

import { authService } from "@/services/auth-service";

interface CodeActivationFormProps {
  onSuccess: () => void;
}

export function CodeActivationForm({ onSuccess }: CodeActivationFormProps) {
  const inputId = "student-code-activation-input";
  const hintId = "student-code-activation-hint";
  const errorId = "student-code-activation-error";
  const successId = "student-code-activation-success";

  const [code, setCode] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [preview, setPreview] = useState<{
    code: string;
    codeType: string;
    targetName: string;
    teacherName: string;
    teacherProfileImageUrl?: string;
  } | null>(null);

  const handleValidate = async (e: FormEvent) => {
    e.preventDefault();
    setError("");
    setSuccess("");
    if (code.length < 6) return;

    try {
      setLoading(true);
      const res = await authService.validateCode(code);
      if (res.data?.success) {
        setPreview(res.data.data);
      } else {
        setError(res.data?.message || "الكود غير صحيح أو تم استخدامه من قبل");
      }
    } catch (err) {
      const message =
        err instanceof AxiosError
          ? err.response?.data?.message
          : "تعذر التحقق من الكود حاليًا";
      setError(message || "تعذر التحقق من الكود حاليًا");
    } finally {
      setLoading(false);
    }
  };

  const handleConfirmActivate = async () => {
    if (!preview) return;
    setError("");
    setSuccess("");
    setLoading(true);

    try {
      const { data } = await authService.activateCode(preview.code);
      setSuccess(data.message || "تم تفعيل الكود بنجاح.");
      setCode("");
      setPreview(null);
      onSuccess();
    } catch (err) {
      const message =
        err instanceof AxiosError
          ? err.response?.data?.message
          : "تعذر تفعيل الكود حاليًا";
      setError(message || "تعذر تفعيل الكود حاليًا");
    } finally {
      setLoading(false);
    }
  };

  if (preview) {
    return (
      <motion.div
        initial={{ opacity: 0, scale: 0.95 }}
        animate={{ opacity: 1, scale: 1 }}
        className="space-y-6 text-center py-4"
      >
        <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
          <KeyRound className="h-6 w-6 animate-pulse" />
        </div>

        <div className="space-y-1">
          <h3 className="text-xl font-black text-[var(--admin-text)]">تأكيد تفعيل الكود</h3>
          <p className="text-xs text-[var(--admin-muted)] leading-relaxed">
            الكود ده هيتفعل على حسابك ويفتح لك:
          </p>
        </div>

        {/* Target metadata card */}
        <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 space-y-4 shadow-sm max-w-md mx-auto">
          <div className="space-y-1">
            <span className="text-xs font-black uppercase tracking-wider text-[var(--admin-primary)]">المحتوى</span>
            <p className="text-base font-bold text-[var(--admin-text)]">{preview.targetName}</p>
          </div>

          <div className="flex flex-col items-center justify-center gap-3 border-t border-[var(--admin-border)] pt-4">
            <span className="text-xs font-black uppercase tracking-wider text-[var(--admin-muted)]">مع المعلم</span>
            <div className="flex items-center gap-3">
              <div className="relative h-9 w-9 overflow-hidden rounded-full border border-[var(--admin-border)] bg-[var(--admin-card-strong)]">
                <Image
                  src={preview.teacherProfileImageUrl || `https://avatar.vercel.sh/${encodeURIComponent(preview.teacherName)}`}
                  alt={preview.teacherName}
                  fill
                  className="object-cover"
                  sizes="36px"
                  unoptimized
                />
              </div>
              <span className="text-xs font-bold text-[var(--admin-text)]">{preview.teacherName}</span>
            </div>
          </div>
        </div>

        {error && (
          <div role="alert" className="rounded-[22px] border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-4 text-xs font-semibold text-[var(--admin-danger)]">
            {error}
          </div>
        )}

        <div className="flex flex-col gap-3 sm:flex-row justify-center pt-2">
          <button
            onClick={() => setPreview(null)}
            className="rounded-2xl border border-[var(--admin-border)] bg-transparent px-6 py-3.5 text-xs font-extrabold text-[var(--admin-text)] transition hover:bg-[var(--admin-card-strong)] cursor-pointer"
          >
            إلغاء
          </button>

          <button
            onClick={() => void handleConfirmActivate()}
            disabled={loading}
            className="inline-flex items-center justify-center gap-2 rounded-2xl bg-[var(--admin-primary)] px-8 py-3.5 text-xs font-extrabold text-[var(--admin-primary-contrast)] shadow-lg transition hover:scale-[1.02] hover:bg-[var(--admin-primary-strong)] cursor-pointer"
          >
            {loading ? "جارٍ التفعيل..." : "تأكيد التفعيل"}
            <ArrowUpLeft className="h-4 w-4" />
          </button>
        </div>
      </motion.div>
    );
  }

  return (
    <motion.form
      onSubmit={handleValidate}
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4 }}
      className="space-y-5"
    >
      {error && (
        <div id={errorId} role="alert" className="rounded-[22px] border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-4 text-sm font-semibold text-[var(--admin-danger)]">
          {error}
        </div>
      )}
      {success && (
        <div id={successId} role="status" aria-live="polite" className="rounded-[22px] border border-[var(--admin-success-20)] bg-[var(--admin-success-10)] p-4 text-sm font-semibold text-[var(--admin-success)]">
          {success}
        </div>
      )}

      <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-4 shadow-sm">
        <label htmlFor={inputId} className="mb-3 flex items-center gap-2 text-sm font-black text-[var(--admin-text)]">
          <KeyRound className="h-4 w-4 text-[var(--admin-primary)]" />
          كود التفعيل
        </label>
        <div className="flex flex-col gap-3 md:flex-row">
          <input
            id={inputId}
            type="text"
            required
            minLength={6}
            placeholder="مثال: MASSAR-2026"
            value={code}
            onChange={(e) => setCode(e.target.value.toUpperCase())}
            className="flex-1 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-3.5 text-sm font-mono tracking-[0.28em] text-[var(--admin-text)] placeholder:font-sans placeholder:tracking-normal placeholder:text-[var(--admin-muted)] focus:border-[var(--admin-primary)] focus:outline-none focus:ring-4 focus:ring-[color:rgba(100,116,139,0.14)] transition-all"
            dir="ltr"
            autoCapitalize="characters"
            autoCorrect="off"
            spellCheck={false}
            aria-describedby={[hintId, error ? errorId : "", success ? successId : ""].filter(Boolean).join(" ")}
          />
          <button
            type="submit"
            disabled={loading || code.length < 6}
            className="inline-flex items-center justify-center gap-2 rounded-2xl bg-[var(--admin-primary)] px-6 py-3.5 text-sm font-extrabold text-[var(--admin-primary-contrast)] shadow-[0_12px_24px_rgba(145,95,42,0.24)] transition hover:-translate-y-0.5 hover:bg-[var(--admin-primary-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)] disabled:cursor-not-allowed disabled:opacity-50"
          >
            {loading ? "جارٍ التحقق..." : "تفعيل الكود"}
            <ArrowUpLeft className="h-4 w-4" />
          </button>
        </div>
        <p id={hintId} className="mt-3 text-xs font-medium leading-6 text-[var(--admin-muted)]">
          اكتب الكود كما وصلك بدون مسافات زائدة. الحد الأدنى 6 أحرف.
        </p>
      </div>

      <div className="grid gap-3 sm:grid-cols-3">
        <HintCard title="6 أحرف أو أكثر" description="اكتب الكود كما وصلك بدون مسافات زائدة." />
        <HintCard title="ربط مباشر" description="عند النجاح تضاف الصلاحية إلى حسابك فورًا." />
        <HintCard title="ملف شخصي" description="قد يُطلب استكمال البيانات قبل إتمام التفعيل." />
      </div>
    </motion.form>
  );
}

function HintCard({ title, description }: { title: string; description: string }) {
  return (
    <div className="rounded-[20px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
      <p className="text-sm font-black text-[var(--admin-text)]">{title}</p>
      <p className="mt-1 text-xs leading-6 text-[var(--admin-muted)]">{description}</p>
    </div>
  );
}
