'use client';

import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import { BookOpen, CheckCircle2, XCircle, AlertTriangle, ShieldCheck, Loader2 } from 'lucide-react';
import { reportService, ParentReportDto } from '@/services/report-service';

export default function ParentReportPage() {
    const params = useParams();
    const studentId = params.studentId as string;

    const [report, setReport] = useState<ParentReportDto | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');

    useEffect(() => {
        if (!studentId) return;

        const fetchReport = async () => {
            try {
                const res = await reportService.getParentSummary(studentId);
                setReport(res.data.data);
            } catch (err: any) {
                setError(err.response?.data?.message || 'فشل في تحميل التقرير.');
            } finally {
                setLoading(false);
            }
        };

        fetchReport();
    }, [studentId]);

    if (loading) {
        return (
            <div className="flex min-h-screen items-center justify-center bg-[var(--background)]">
                <Loader2 className="h-10 w-10 animate-spin text-[var(--primary)]" />
            </div>
        );
    }

    if (error || !report) {
        return (
            <div className="flex min-h-screen flex-col items-center justify-center bg-[var(--background)] p-4">
                <div className="w-full max-w-md rounded-[28px] border border-[color:rgba(239,68,68,0.2)] bg-[var(--card)] p-8 text-center shadow-[0_20px_60px_rgba(78,70,57,0.08)]">
                    <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-[color:rgba(239,68,68,0.1)]">
                        <AlertTriangle className="h-8 w-8 text-[#ef4444]" />
                    </div>
                    <h2 className="text-2xl font-black text-[var(--foreground)]">تنبيه</h2>
                    <p className="mt-2 text-sm font-bold text-[#ef4444]">{error || 'التقرير غير موجود'}</p>
                </div>
            </div>
        );
    }

    const {
        studentName,
        overallStatus,
        completedLessonsCount,
        passedExamsCount,
        failedExamsCount,
        recentWarnings
    } = report;

    const getStatusStyle = (status: string) => {
        switch (status) {
            case 'Excellent': return { bg: 'rgba(34,197,94,0.1)', text: '#22c55e', label: 'ممتاز' };
            case 'Good': return { bg: 'rgba(34,197,94,0.08)', text: '#16a34a', label: 'جيد' };
            case 'NeedsAttention': return { bg: 'rgba(234,179,8,0.1)', text: '#ca8a04', label: 'يحتاج متابعة' };
            case 'AtRisk': return { bg: 'rgba(239,68,68,0.1)', text: '#ef4444', label: 'في خطر' };
            default: return { bg: 'var(--muted)', text: 'var(--muted-foreground)', label: status };
        }
    };

    const statusStyle = getStatusStyle(overallStatus);

    const metrics = [
        {
            label: 'الدروس المكتملة',
            value: completedLessonsCount,
            icon: BookOpen,
            color: 'var(--primary)',
            bg: 'var(--secondary)',
        },
        {
            label: 'الامتحانات الناجحة',
            value: passedExamsCount,
            icon: CheckCircle2,
            color: '#22c55e',
            bg: 'rgba(34,197,94,0.1)',
        },
        {
            label: 'الامتحانات غير الناجحة',
            value: failedExamsCount,
            icon: XCircle,
            color: '#ef4444',
            bg: 'rgba(239,68,68,0.1)',
        },
    ];

    const getSeverityStyle = (severity: string) => {
        switch (severity) {
            case 'Critical': return { bg: 'rgba(239,68,68,0.1)', text: '#ef4444', label: 'حرج' };
            case 'Medium': return { bg: 'rgba(234,179,8,0.1)', text: '#ca8a04', label: 'متوسط' };
            default: return { bg: 'rgba(234,179,8,0.08)', text: '#eab308', label: 'تنبيه' };
        }
    };

    return (
        <div className="min-h-screen bg-[var(--background)] py-12 px-4 sm:px-6 lg:px-8">
            <div className="mx-auto max-w-4xl space-y-8">

                {/* Header Profile */}
                <div className="relative overflow-hidden rounded-[28px] border border-[var(--border)] bg-[var(--card)] p-8 shadow-[0_20px_60px_rgba(78,70,57,0.08)]">
                    <div className="absolute left-0 top-0 h-64 w-64 rounded-br-full bg-[var(--secondary)] opacity-40" />

                    <div className="relative flex flex-col items-center justify-between gap-6 md:flex-row md:items-start">
                        <div className="flex flex-col items-center gap-6 md:flex-row">
                            <div className="flex h-24 w-24 items-center justify-center rounded-full bg-gradient-to-br from-[var(--primary)] to-[color:rgba(154,105,51,0.7)] text-4xl font-black text-[var(--primary-foreground)] shadow-[0_12px_40px_rgba(78,70,57,0.15)]">
                                {studentName.charAt(0)}
                            </div>
                            <div className="text-center md:text-right">
                                <h1 className="text-3xl font-black text-[var(--foreground)]">{studentName}</h1>
                                <p className="mt-1 text-sm font-bold text-[var(--muted-foreground)]">تقرير التقدم الأكاديمي</p>
                            </div>
                        </div>

                        <div className="text-center">
                            <p className="mb-2 text-xs font-black tracking-[0.2em] text-[var(--muted-foreground)]">
                                مستوى الالتزام
                            </p>
                            <span
                                className="inline-block rounded-2xl px-5 py-2.5 text-sm font-black shadow-sm"
                                style={{ backgroundColor: statusStyle.bg, color: statusStyle.text }}
                            >
                                {statusStyle.label}
                            </span>
                        </div>
                    </div>
                </div>

                {/* Metrics */}
                <div className="grid grid-cols-1 gap-5 md:grid-cols-3">
                    {metrics.map((m, i) => {
                        const Icon = m.icon;
                        return (
                            <div key={i} className="flex items-center gap-4 rounded-[24px] border border-[var(--border)] bg-[var(--card)] p-6 shadow-[0_12px_40px_rgba(78,70,57,0.06)]">
                                <div
                                    className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl"
                                    style={{ backgroundColor: m.bg }}
                                >
                                    <Icon className="h-7 w-7" style={{ color: m.color }} />
                                </div>
                                <div>
                                    <p className="text-sm font-bold text-[var(--muted-foreground)]">{m.label}</p>
                                    <p className="text-3xl font-black text-[var(--foreground)]">{m.value}</p>
                                </div>
                            </div>
                        );
                    })}
                </div>

                {/* Warnings */}
                <div className="overflow-hidden rounded-[28px] border border-[var(--border)] bg-[var(--card)] shadow-[0_12px_40px_rgba(78,70,57,0.06)]">
                    <div className="bg-[var(--secondary)] p-6">
                        <h2 className="flex items-center gap-3 text-xl font-black text-[var(--foreground)]">
                            <AlertTriangle className="h-6 w-6 text-[#eab308]" />
                            التنبيهات والإشعارات الأخيرة
                        </h2>
                    </div>

                    <div className="p-6">
                        {recentWarnings.length === 0 ? (
                            <div className="py-12 text-center">
                                <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-[color:rgba(34,197,94,0.1)]">
                                    <ShieldCheck className="h-8 w-8 text-[#22c55e]" />
                                </div>
                                <h3 className="text-lg font-black text-[var(--foreground)]">لا توجد تنبيهات</h3>
                                <p className="mt-1 text-sm text-[var(--muted-foreground)]">السجل الأكاديمي للطالب نظيف حالياً.</p>
                            </div>
                        ) : (
                            <div className="space-y-4">
                                {recentWarnings.map((warning, index) => {
                                    const sevStyle = getSeverityStyle(warning.severity);
                                    return (
                                        <div key={index} className="flex items-start gap-4 rounded-2xl border border-[var(--border)] bg-[var(--secondary)] p-4">
                                            <div
                                                className="mt-1 flex h-10 w-10 shrink-0 items-center justify-center rounded-full"
                                                style={{ backgroundColor: sevStyle.bg }}
                                            >
                                                <AlertTriangle className="h-5 w-5" style={{ color: sevStyle.text }} />
                                            </div>
                                            <div>
                                                <h4 className="font-black text-[var(--foreground)]">
                                                    تنبيه {sevStyle.label}
                                                </h4>
                                                <p className="mt-1 text-sm font-medium text-[var(--muted-foreground)]">
                                                    {warning.reason}
                                                </p>
                                                <p className="mt-2 text-xs font-bold tracking-wider text-[var(--muted-foreground)] opacity-60">
                                                    {new Date(warning.generatedAt).toLocaleDateString('ar-EG')}
                                                </p>
                                            </div>
                                        </div>
                                    );
                                })}
                            </div>
                        )}
                    </div>
                </div>

            </div>
        </div>
    );
}
