'use client';

import { FormEvent, useState } from 'react';
import { CheckCircle2, GraduationCap, LoaderCircle, Search, ShieldCheck, Trophy } from 'lucide-react';
import apiClient from '@/services/api-client';

type ThanaweyaResult = {
  seatingNumber: string;
  arabicName: string;
  totalDegree: number | null;
  studentCaseDescription: string;
};

type ThanaweyaDetailedResult = {
  subjects: Array<{ subject: string; mark: string; percentage: string }>;
};

const digitsOnly = (value: string) => value.replace(/[^0-9]/g, '');
const TOTAL_MARKS = 320;

const formatPercentage = (totalDegree: number | null) =>
  totalDegree === null ? '—' : `${((totalDegree / TOTAL_MARKS) * 100).toFixed(2)}٪`;

export function ThanaweyaResultsClient() {
  const [seatingNumber, setSeatingNumber] = useState('');
  const [system, setSystem] = useState<'1' | '2'>('1');
  const [result, setResult] = useState<ThanaweyaResult | null>(null);
  const [message, setMessage] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [subjects, setSubjects] = useState<ThanaweyaDetailedResult['subjects'] | null>(null);
  const [isLoadingSubjects, setIsLoadingSubjects] = useState(false);

  const search = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const normalized = digitsOnly(seatingNumber);
    setSeatingNumber(normalized);
    setResult(null);
    setSubjects(null);

    if (normalized.length < 4) {
      setMessage('اكتب رقم الجلوس كاملًا ثم اضغط استعلام.');
      return;
    }

    setIsLoading(true);
    setMessage('');
    try {
      const response = await apiClient.get<ThanaweyaResult>(`/thanaweya-results/${normalized}`);
      setResult(response.data);
    } catch (error: any) {
      setMessage(error?.response?.data?.message || 'تعذر الوصول للنتيجة الآن. حاول مرة أخرى بعد قليل.');
    } finally {
      setIsLoading(false);
    }
  };

  const loadSubjects = async () => {
    if (!result || isLoadingSubjects) return;

    setIsLoadingSubjects(true);
    setMessage('');
    try {
      const response = await apiClient.get<ThanaweyaDetailedResult>(`/thanaweya-results/${result.seatingNumber}/subjects?system=${system}`);
      setSubjects(response.data.subjects);
    } catch (error: any) {
      setMessage(error?.response?.data?.message || 'تعذر جلب الدرجات التفصيلية الآن. حاول مرة أخرى بعد قليل.');
    } finally {
      setIsLoadingSubjects(false);
    }
  };

  return (
    <main className="relative isolate min-h-[calc(100svh-4rem)] overflow-hidden bg-[var(--landing-bg)] px-4 py-10 text-[var(--landing-ink)] sm:px-6 sm:py-16">
      <div aria-hidden="true" className="absolute inset-0 -z-10 bg-[radial-gradient(circle_at_82%_8%,color-mix(in_srgb,var(--landing-accent)_16%,transparent),transparent_24%),radial-gradient(circle_at_12%_88%,color-mix(in_srgb,var(--primary)_12%,transparent),transparent_30%)]" />

      <section className="mx-auto grid w-full max-w-5xl overflow-hidden rounded-2xl border border-[var(--landing-line)] bg-[var(--landing-card)] shadow-sm lg:grid-cols-[.9fr_1.1fr]">
        <div className="relative overflow-hidden bg-[var(--landing-accent)] px-6 py-10 text-[var(--landing-accent-foreground)] sm:px-10 lg:flex lg:flex-col lg:justify-between lg:p-12">
          <div aria-hidden="true" className="absolute -left-20 -top-24 h-64 w-64 rounded-full border-[32px] border-white/10" />
          <div aria-hidden="true" className="absolute -bottom-24 -right-24 h-64 w-64 rounded-full border-[32px] border-white/10" />
          <div className="relative">
            <span className="inline-flex h-14 w-14 items-center justify-center rounded-2xl bg-white/15"><GraduationCap className="h-7 w-7" aria-hidden="true" /></span>
            <p className="mt-8 text-sm font-extrabold tracking-[0.12em] text-white/75">الثانوية العامة · 2026</p>
            <h1 className="mt-3 text-4xl font-black leading-[1.15] sm:text-5xl">نتيجتك<br />بين إيديك</h1>
            <p className="mt-5 max-w-sm text-base font-semibold leading-8 text-white/85">اكتب رقم جلوسك فقط، وستظهر بياناتك ودرجتك من قاعدة البيانات الموحدة للمنصة.</p>
          </div>
          <div className="relative mt-10 flex items-center gap-3 text-sm font-bold text-white/85 lg:mt-16">
            <ShieldCheck className="h-5 w-5 shrink-0" aria-hidden="true" />
            تظهر نتيجة رقم الجلوس الذي تبحث عنه فقط.
          </div>
        </div>

        <div className="p-6 sm:p-10 lg:p-12">
          <div className="flex items-center gap-3 text-[var(--landing-accent)]">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-[var(--landing-teal-soft)]"><Search className="h-5 w-5" aria-hidden="true" /></span>
            <p className="text-sm font-black">الاستعلام عن النتيجة</p>
          </div>

          <form onSubmit={search} className="mt-8">
            <label htmlFor="seating-number" className="block text-sm font-black text-[var(--landing-ink)]">رقم الجلوس</label>
            <div className="mt-3 flex flex-col gap-3 sm:flex-row">
              <input
                id="seating-number"
                inputMode="numeric"
                autoComplete="off"
                dir="ltr"
                value={seatingNumber}
                onChange={(event) => setSeatingNumber(digitsOnly(event.target.value))}
                placeholder="مثال: 123456"
                className="min-h-14 flex-1 rounded-xl border border-[var(--landing-line)] bg-[var(--landing-bg-soft)] px-4 text-center text-lg font-black tracking-[0.12em] text-[var(--landing-ink)] outline-none transition focus:border-[var(--landing-accent)] focus:ring-4 focus:ring-[color-mix(in_srgb,var(--landing-accent)_16%,transparent)]"
              />
              <button type="submit" disabled={isLoading} className="inline-flex min-h-14 items-center justify-center gap-2 rounded-xl bg-[var(--landing-accent)] px-6 text-base font-black text-[var(--landing-accent-foreground)] transition hover:bg-[var(--landing-accent-strong)] disabled:cursor-wait disabled:opacity-70">
                {isLoading ? <LoaderCircle className="h-5 w-5 animate-spin" aria-hidden="true" /> : <Search className="h-5 w-5" aria-hidden="true" />}
                {isLoading ? 'جاري البحث' : 'استعلام'}
              </button>
            </div>
            <fieldset className="mt-4 flex flex-wrap gap-3" aria-label="نظام الثانوية العامة">
              <legend className="mb-2 text-sm font-black text-[var(--landing-ink)]">النظام</legend>
              <label className="inline-flex cursor-pointer items-center gap-2 rounded-xl border border-[var(--landing-line)] px-4 py-2 text-sm font-bold has-[:checked]:border-[var(--landing-accent)] has-[:checked]:bg-[var(--landing-teal-soft)]">
                <input type="radio" name="system" value="1" checked={system === '1'} onChange={() => setSystem('1')} />
                نظام حديث
              </label>
              <label className="inline-flex cursor-pointer items-center gap-2 rounded-xl border border-[var(--landing-line)] px-4 py-2 text-sm font-bold has-[:checked]:border-[var(--landing-accent)] has-[:checked]:bg-[var(--landing-teal-soft)]">
                <input type="radio" name="system" value="2" checked={system === '2'} onChange={() => setSystem('2')} />
                نظام قديم
              </label>
            </fieldset>
          </form>

          {message && <p role="status" className="mt-5 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm font-bold leading-6 text-amber-900">{message}</p>}

          {result && (
            <section aria-live="polite" className="mt-8 border-t border-[var(--landing-line)] pt-7">
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div>
                  <p className="text-xs font-extrabold text-[var(--landing-muted)]">اسم الطالب / الطالبة</p>
                  <h2 className="mt-1 text-2xl font-black text-[var(--landing-ink)]">{result.arabicName}</h2>
                  <p className="mt-2 text-sm font-bold text-[var(--landing-muted)]">رقم الجلوس: <span dir="ltr">{result.seatingNumber}</span></p>
                </div>
                <span className="inline-flex items-center gap-2 rounded-full bg-emerald-50 px-3 py-2 text-sm font-black text-emerald-800"><CheckCircle2 className="h-4 w-4" aria-hidden="true" />تم العثور على النتيجة</span>
              </div>

              <div className="mt-7 grid gap-4 sm:grid-cols-2">
                <div className="rounded-2xl bg-[var(--landing-bg-soft)] p-5">
                  <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-[var(--landing-card)] text-[var(--landing-accent)] shadow-sm"><Trophy className="h-5 w-5" aria-hidden="true" /></span>
                  <p className="mt-4 text-sm font-extrabold text-[var(--landing-muted)]">المجموع الكلي</p>
                  <p className="mt-1 text-3xl font-black text-[var(--landing-ink)]">{result.totalDegree ?? '—'}<span className="mr-1 text-base text-[var(--landing-muted)]">/ {TOTAL_MARKS}</span></p>
                </div>
                <div className="rounded-2xl border border-[var(--landing-line)] p-5">
                  <p className="text-sm font-extrabold text-[var(--landing-muted)]">النسبة المئوية</p>
                  <p className="mt-2 text-3xl font-black text-[var(--landing-ink)]">{formatPercentage(result.totalDegree)}</p>
                  <p className="mt-3 text-sm font-bold leading-6 text-[var(--landing-muted)]">{result.studentCaseDescription || 'لا توجد ملاحظات إضافية'}</p>
                </div>
              </div>

              {!subjects && (
                <button type="button" onClick={loadSubjects} disabled={isLoadingSubjects} className="mt-6 inline-flex min-h-12 items-center justify-center gap-2 rounded-xl border border-[var(--landing-accent)] px-5 text-sm font-black text-[var(--landing-accent)] transition hover:bg-[var(--landing-teal-soft)] disabled:cursor-wait disabled:opacity-70">
                  {isLoadingSubjects && <LoaderCircle className="h-4 w-4 animate-spin" aria-hidden="true" />}
                  {isLoadingSubjects ? 'جاري جلب الدرجات' : 'عرض الدرجات التفصيلية'}
                </button>
              )}

              {subjects && (
                <div className="mt-6 overflow-hidden rounded-2xl border border-[var(--landing-line)]">
                  <table className="w-full text-right text-sm">
                    <thead className="bg-[var(--landing-bg-soft)] text-[var(--landing-muted)]">
                      <tr>
                        <th className="px-4 py-3 font-black">المادة</th>
                        <th className="px-4 py-3 font-black">الدرجة</th>
                        <th className="px-4 py-3 font-black">النسبة</th>
                      </tr>
                    </thead>
                    <tbody>
                      {subjects.map((subject) => (
                        <tr key={subject.subject} className="border-t border-[var(--landing-line)] text-[var(--landing-ink)]">
                          <td className="px-4 py-3 font-bold">{subject.subject}</td>
                          <td className="px-4 py-3 font-black" dir="ltr">{subject.mark}</td>
                          <td className="px-4 py-3 font-bold" dir="ltr">{subject.percentage}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </section>
          )}
        </div>
      </section>
    </main>
  );
}
