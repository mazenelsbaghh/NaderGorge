"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { AlertTriangle, Loader2, RefreshCw } from "lucide-react";

import { codeService } from "@/services/code-service";
import { useAuthStore } from "@/stores/auth-store";

type RedeemState = "loading" | "redeeming" | "error";

export function QrRedeemClient({ codeHash }: { codeHash: string }) {
  const router = useRouter();
  const { isAuthenticated, isLoading, loadFromStorage } = useAuthStore();
  const [state, setState] = useState<RedeemState>("loading");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadFromStorage();
  }, [loadFromStorage]);

  useEffect(() => {
    if (isLoading) return;

    if (!isAuthenticated) {
      router.replace(`/login?returnUrl=${encodeURIComponent(`/qr/${codeHash}`)}`);
      return;
    }

    let cancelled = false;

    async function redeem() {
      setState("redeeming");
      setError(null);
      try {
        const response = await codeService.redeemCode(codeHash);
        if (cancelled) return;
        const redirectUrl = response.data.data?.redirectUrl || "/student";
        router.replace(redirectUrl);
      } catch (err) {
        if (cancelled) return;
        const apiError = err as { response?: { data?: { message?: string } }; message?: string };
        setError(apiError.response?.data?.message || apiError.message || "تعذر تفعيل الكود.");
        setState("error");
      }
    }

    void redeem();

    return () => {
      cancelled = true;
    };
  }, [codeHash, isAuthenticated, isLoading, router]);

  const isBusy = state === "loading" || state === "redeeming" || isLoading;

  return (
    <main className="min-h-screen bg-[var(--background)] px-4 py-10 text-[var(--foreground)]">
      <section className="mx-auto flex min-h-[70vh] w-full max-w-md flex-col items-center justify-center text-center">
        <div className="w-full rounded-2xl border border-[var(--border)] bg-[var(--card)] p-6 shadow-sm">
          {isBusy ? (
            <>
              <Loader2 className="mx-auto mb-4 h-8 w-8 animate-spin text-[var(--secondary)]" aria-hidden="true" />
              <h1 className="text-xl font-bold">جاري تفعيل الكود</h1>
              <p className="mt-2 text-sm text-[var(--muted-foreground)]">
                نتحقق من جلستك ونضيف المحتوى لحسابك.
              </p>
            </>
          ) : (
            <>
              <AlertTriangle className="mx-auto mb-4 h-8 w-8 text-red-600" aria-hidden="true" />
              <h1 className="text-xl font-bold">تعذر تفعيل الكود</h1>
              <p className="mt-2 text-sm text-[var(--muted-foreground)]">{error}</p>
              <div className="mt-5 flex flex-col gap-3 sm:flex-row sm:justify-center">
                <button
                  type="button"
                  onClick={() => window.location.reload()}
                  className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[var(--primary)] px-4 py-2 text-sm font-bold text-[var(--primary-foreground)] transition-colors hover:bg-[var(--secondary)] focus:outline-none focus:ring-2 focus:ring-[var(--ring)] focus:ring-offset-2"
                >
                  <RefreshCw className="h-4 w-4" aria-hidden="true" />
                  إعادة المحاولة
                </button>
                <Link
                  href="/student/code-redemption"
                  className="inline-flex min-h-11 items-center justify-center rounded-xl border border-[var(--border)] px-4 py-2 text-sm font-bold transition-colors hover:bg-[var(--muted)] focus:outline-none focus:ring-2 focus:ring-[var(--ring)] focus:ring-offset-2"
                >
                  إدخال الكود يدويا
                </Link>
              </div>
            </>
          )}
        </div>
      </section>
    </main>
  );
}
