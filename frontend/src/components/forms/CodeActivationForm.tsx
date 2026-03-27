"use client";

import { useState } from "react";
import type { FormEvent } from "react";

import { AxiosError } from "axios";
import { ArrowUpLeft, KeyRound } from "lucide-react";
import { motion } from "framer-motion";

import { authService } from "@/services/auth-service";

interface CodeActivationFormProps {
  onSuccess: () => void;
}

export function CodeActivationForm({ onSuccess }: CodeActivationFormProps) {
  const [code, setCode] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError("");
    setSuccess("");
    setLoading(true);

    try {
      const { data } = await authService.activateCode(code);
      setSuccess(data.data.message || "تم تفعيل الكود بنجاح.");
      setCode("");
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

  return (
    <motion.form
      onSubmit={handleSubmit}
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4 }}
      className="space-y-5"
    >
      {error && (
        <div className="rounded-[22px] border border-red-200 bg-red-50 p-4 text-sm font-semibold text-red-700">
          {error}
        </div>
      )}
      {success && (
        <div className="rounded-[22px] border border-emerald-200 bg-emerald-50 p-4 text-sm font-semibold text-emerald-700">
          {success}
        </div>
      )}

      <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-4 shadow-sm">
        <label className="mb-3 flex items-center gap-2 text-sm font-black text-[var(--admin-text)]">
          <KeyRound className="h-4 w-4 text-[var(--admin-primary)]" />
          كود التفعيل
        </label>
        <div className="flex flex-col gap-3 md:flex-row">
          <input
            type="text"
            required
            minLength={6}
            placeholder="مثال: NADER-2026"
            value={code}
            onChange={(e) => setCode(e.target.value.toUpperCase())}
            className="flex-1 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-3.5 text-sm font-mono tracking-[0.28em] text-[var(--admin-text)] placeholder:font-sans placeholder:tracking-normal placeholder:text-[var(--admin-muted)] focus:border-[var(--admin-primary)] focus:outline-none focus:ring-4 focus:ring-[color:rgba(154,105,51,0.12)] transition-all"
            dir="ltr"
          />
          <button
            type="submit"
            disabled={loading || code.length < 6}
            className="inline-flex items-center justify-center gap-2 rounded-2xl bg-[var(--admin-primary)] px-6 py-3.5 text-sm font-extrabold text-[var(--admin-primary-contrast)] shadow-[0_12px_24px_rgba(145,95,42,0.24)] transition hover:-translate-y-0.5 hover:bg-[var(--admin-primary-strong)] disabled:cursor-not-allowed disabled:opacity-50"
          >
            {loading ? "جارٍ التفعيل..." : "تفعيل الكود"}
            <ArrowUpLeft className="h-4 w-4" />
          </button>
        </div>
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
