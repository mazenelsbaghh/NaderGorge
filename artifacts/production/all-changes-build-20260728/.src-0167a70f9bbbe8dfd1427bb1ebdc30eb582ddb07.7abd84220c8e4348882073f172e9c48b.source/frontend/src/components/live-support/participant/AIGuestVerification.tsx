'use client';

import { useState } from 'react';
import { getLiveSupportApiError, liveSupportService, type LiveSupportAIVerificationSession } from '@/services/live-support-service';
import { ShieldCheck, ArrowLeft, LoaderCircle, CheckCircle, AlertCircle } from 'lucide-react';

interface AIGuestVerificationProps {
  conversationId: string;
  initialSession?: LiveSupportAIVerificationSession | null;
  onVerified: () => void;
}

export function AIGuestVerification({ conversationId, initialSession, onVerified }: AIGuestVerificationProps) {
  const [session, setSession] = useState<LiveSupportAIVerificationSession | null>(initialSession?.status === 'AwaitingLookup' ? null : initialSession || null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [lookupKey, setLookupKey] = useState<'phone.full' | 'student_code.full'>('phone.full');
  const [lookupValue, setLookupValue] = useState('');
  const [challengeAnswer, setChallengeAnswer] = useState('');

  const handleLookup = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!lookupValue.trim()) return;
    setBusy(true);
    setError('');
    try {
      const res = await liveSupportService.aiVerificationLookup(conversationId, {
        lookupKey,
        value: lookupValue.trim()
      });
      setSession(res);
    } catch (error) {
      setError(getLiveSupportApiError(error, 'تعذر إكمال التحقق بهذه البيانات. حاول مرة أخرى.'));
    } finally {
      setBusy(false);
    }
  };

  const handleAnswerChallenge = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!challengeAnswer.trim() || !session) return;
    setBusy(true);
    setError('');
    try {
      const res = await liveSupportService.aiVerificationAnswer(conversationId, {
        sessionId: session.sessionId,
        answer: challengeAnswer.trim()
      });
      setSession(res);
      if (res.status === 'Verified') {
        onVerified();
      }
    } catch (error) {
      setError(getLiveSupportApiError(error, 'تعذر مطابقة الإجابة. حاول مرة أخرى.'));
    } finally {
      setBusy(false);
    }
  };

  const resetLookup = () => {
    setSession(null);
    setLookupValue('');
    setChallengeAnswer('');
    setError('');
  };

  // 1. Initial State: No active session (Lookup phase)
  if (!session) {
    return (
      <div dir="rtl" className="my-3 rounded-2xl border border-cyan-100 bg-cyan-50/50 p-4 shadow-sm">
        <div className="flex items-start gap-3 mb-3">
          <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-cyan-100 text-cyan-800">
            <ShieldCheck size={20} />
          </span>
          <div>
            <h4 className="text-xs font-semibold text-cyan-900">التحقق من ملكية الحساب</h4>
            <p className="mt-1 text-xs text-slate-600 leading-relaxed">
              يرجى إدخال بيانات حسابك للتحقق من هويتك لتمكين الخدمات الذاتية.
            </p>
          </div>
        </div>

        <form onSubmit={handleLookup} className="space-y-3">
          <div>
            <span className="text-xs font-medium text-slate-700 block mb-1">نوع معرف الحساب</span>
            <div className="flex gap-2">
              <button
                type="button"
                onClick={() => setLookupKey('phone.full')}
                className={`flex-1 h-9 rounded-xl border text-xs font-semibold transition-all ${
                  lookupKey === 'phone.full'
                    ? 'border-cyan-600 bg-cyan-100/50 text-cyan-800'
                    : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50'
                }`}
              >
                رقم الهاتف
              </button>
              <button
                type="button"
                onClick={() => setLookupKey('student_code.full')}
                className={`flex-1 h-9 rounded-xl border text-xs font-semibold transition-all ${
                  lookupKey === 'student_code.full'
                    ? 'border-cyan-600 bg-cyan-100/50 text-cyan-800'
                    : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50'
                }`}
              >
                كود الطالب
              </button>
            </div>
          </div>

          <label className="block">
            <span className="text-xs font-medium text-slate-700">
              {lookupKey === 'phone.full' ? 'رقم الهاتف المسجل' : 'كود الطالب المسجل'}
            </span>
            <input
              required
              disabled={busy}
              inputMode={lookupKey === 'phone.full' ? 'tel' : 'text'}
              value={lookupValue}
              onChange={(e) => setLookupValue(e.target.value)}
              placeholder={lookupKey === 'phone.full' ? 'مثال: 01xxxxxxxxx' : 'كود الطالب'}
              className="mt-1 h-10 w-full rounded-xl border border-slate-200 bg-white px-3 text-xs outline-none focus:border-cyan-600 disabled:opacity-50"
            />
          </label>

          {error && (
            <p className="text-xs font-medium text-red-600 leading-normal" role="alert">
              {error}
            </p>
          )}

          <button
            type="submit"
            disabled={busy || !lookupValue.trim()}
            className="h-10 w-full rounded-xl bg-cyan-700 text-xs font-bold text-white hover:bg-cyan-800 disabled:opacity-50 transition-colors flex items-center justify-center gap-1.5"
          >
            {busy && <LoaderCircle size={14} className="animate-spin" />}
            <span>البدء بالتحقق</span>
          </button>
        </form>
      </div>
    );
  }

  // 2. Verified State
  if (session.status === 'Verified') {
    return (
      <div dir="rtl" className="my-3 rounded-2xl border border-green-100 bg-green-50/50 p-4 text-center shadow-sm">
        <span className="mx-auto mb-2 grid size-12 place-items-center rounded-full bg-green-100 text-green-700">
          <CheckCircle size={24} />
        </span>
        <h4 className="text-sm font-bold text-green-900">تم التحقق بنجاح</h4>
        <p className="mt-1 text-xs text-green-800">
          لقد تم التحقق من هويتك بنجاح وربط حسابك بهذه المحادثة.
        </p>
      </div>
    );
  }

  // 3. Exhausted / Failed / HandedOff State
  if (['Exhausted', 'Failed', 'HandedOff'].includes(session.status)) {
    return (
      <div dir="rtl" className="my-3 rounded-2xl border border-red-100 bg-red-50/50 p-4 text-center shadow-sm">
        <span className="mx-auto mb-2 grid size-12 place-items-center rounded-full bg-red-100 text-red-700">
          <AlertCircle size={24} />
        </span>
        <h4 className="text-sm font-bold text-red-900">فشل التحقق</h4>
        <p className="mt-1 text-xs text-red-800 leading-relaxed">
          لقد تجاوزت الحد الأقصى للمحاولات أو انتهت صلاحية الجلسة.
          سيتم تحويلك للدعم الفني لمساعدتك.
        </p>
        <button
          type="button"
          onClick={resetLookup}
          className="mt-3 text-xs font-semibold text-cyan-700 hover:text-cyan-800 transition-colors"
        >
          البدء من جديد
        </button>
      </div>
    );
  }

  // 4. Challenging State (Submit verification challenge answers)
  return (
    <div dir="rtl" className="my-3 rounded-2xl border border-cyan-100 bg-cyan-50/50 p-4 shadow-sm">
      <div className="flex justify-between items-center mb-3">
        <div className="flex items-center gap-2">
          <span className="grid size-8 place-items-center rounded-lg bg-cyan-100 text-cyan-800">
            <ShieldCheck size={16} />
          </span>
          <h4 className="text-xs font-semibold text-cyan-900">سؤال التحقق</h4>
        </div>
        <button
          type="button"
          onClick={resetLookup}
          className="text-xs font-medium text-slate-500 hover:text-slate-700 flex items-center gap-0.5 transition-colors"
        >
          <ArrowLeft size={14} />
          <span>رجوع</span>
        </button>
      </div>

      <form onSubmit={handleAnswerChallenge} className="space-y-3">
        <div className="rounded-xl bg-white p-3 border border-slate-100">
          <span className="text-xs font-semibold text-slate-400 block mb-1">السؤال المطروح:</span>
          <p className="text-sm font-medium text-slate-800 leading-relaxed">
            {session.promptText || 'أجب على سؤال الأمان لتأكيد الهوية.'}
          </p>
        </div>

        <label className="block">
          <span className="text-xs font-medium text-slate-700">إجابتك</span>
          <input
            required
            disabled={busy}
            autoComplete="off"
            value={challengeAnswer}
            onChange={(e) => setChallengeAnswer(e.target.value)}
            placeholder="اكتب الإجابة هنا..."
            className="mt-1 h-10 w-full rounded-xl border border-slate-200 bg-white px-3 text-xs outline-none focus:border-cyan-600 disabled:opacity-50"
          />
        </label>

        <div className="flex justify-between text-[10px] font-medium text-slate-500 px-1">
          <span>المحاولات المستخدمة: {session.attemptCount}</span>
          <span>الحد الأقصى: {session.maxAttempts}</span>
        </div>

        {error && (
          <p className="text-xs font-medium text-red-600 leading-normal" role="alert">
            {error}
          </p>
        )}

        <button
          type="submit"
          disabled={busy || !challengeAnswer.trim()}
          className="h-10 w-full rounded-xl bg-cyan-700 text-xs font-bold text-white hover:bg-cyan-800 disabled:opacity-50 transition-colors flex items-center justify-center gap-1.5"
        >
          {busy && <LoaderCircle size={14} className="animate-spin" />}
          <span>إرسال الإجابة</span>
        </button>
      </form>
    </div>
  );
}
