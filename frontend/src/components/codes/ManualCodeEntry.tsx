'use client';

/**
 * ManualCodeEntry — Student manual code redemption page
 *
 * Text input for code string, submit button, success/error feedback
 * with redirect to unlocked content on success.
 */

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { motion, AnimatePresence } from 'framer-motion';
import { Gift, CheckCircle, XCircle } from 'lucide-react';
import { InlineLoader } from '@/components/ui/loading-indicator';
import { codeService } from '@/services/code-service';

export function ManualCodeEntry() {
  const router = useRouter();
  const [code, setCode] = useState('');
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<{ success: boolean; message: string } | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!code.trim() || loading) return;

    setLoading(true);
    setResult(null);

    try {
      const response = await codeService.redeemCode(code.trim().toUpperCase());
      const data = response.data?.data;

      setResult({ success: true, message: data?.message || 'تم تفعيل الكود بنجاح!' });

      // Redirect after short delay
      setTimeout(() => {
        router.push(data?.redirectUrl || '/student');
      }, 1500);
    } catch (err: any) {
      const message = err.response?.data?.message || 'كود غير صالح أو مستخدم من قبل';
      setResult({ success: false, message });
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="w-full max-w-md mx-auto">
      <form onSubmit={handleSubmit} className="space-y-4">
        {/* Code Input */}
        <div className="relative">
          <Gift
            className="absolute right-3 top-1/2 -translate-y-1/2 opacity-60"
            size={18}
            style={{ color: 'var(--admin-muted)' }}
          />
          <input
            type="text"
            value={code}
            onChange={(e) => {
              setCode(e.target.value.toUpperCase());
              setResult(null);
            }}
            placeholder="أدخل الكود هنا..."
            dir="ltr"
            className="auth-input text-center text-lg tracking-widest font-mono"
            style={{ paddingRight: '2.5rem' }}
            maxLength={20}
            autoFocus
          />
        </div>

        {/* Submit */}
        <button
          type="submit"
          disabled={loading || !code.trim()}
          className="auth-btn-primary flex items-center justify-center gap-2"
        >
          {loading ? (
            <>
              <InlineLoader />
              جاري التفعيل...
            </>
          ) : (
            'تفعيل الكود'
          )}
        </button>
      </form>

      {/* Result Feedback */}
      <AnimatePresence>
        {result && (
          <motion.div
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -10 }}
            className={`mt-4 flex items-center gap-3 rounded-xl p-4 text-sm ${
              result.success
                ? 'bg-green-500/10 text-green-400 border border-green-500/20'
                : 'bg-red-500/10 text-red-400 border border-red-500/20'
            }`}
          >
            {result.success ? <CheckCircle size={20} /> : <XCircle size={20} />}
            <span>{result.message}</span>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
