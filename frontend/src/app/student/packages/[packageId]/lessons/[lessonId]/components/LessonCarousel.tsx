"use client";

import { CSSProperties, useEffect, useState, MouseEvent, useRef } from "react";
import { AnimatePresence, motion, useMotionTemplate, useMotionValue } from "framer-motion";
import { cn } from "@/lib/utils";
import clsx from "clsx";
import SecureVideoPlayer from "../../../../../../../components/video/SecureVideoPlayer";
import type { SecureVideoPlayerRef, WatchStatus } from "../../../../../../../components/video/SecureVideoPlayer";
import { WatchStatusBar } from "../../../../../../../components/video/WatchStatusBar";
import { ChapterList } from "../../../../../../../components/video/ChapterList";
import { LessonMindmapDisplay } from "../../../../../../../components/video/LessonMindmapDisplay";
import { useRouter, useParams } from "next/navigation";
import { Lock, Award, ClipboardCheck } from "lucide-react";

// --- Icons ---
function IconCheck({ className, ...props }: React.ComponentProps<"svg">) {
    return (
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" fill="currentColor" className={cn("h-4 w-4", className)} {...props}>
            <path d="m229.66 77.66-128 128a8 8 0 0 1-11.32 0l-56-56a8 8 0 0 1 11.32-11.32L96 188.69 218.34 66.34a8 8 0 0 1 11.32 11.32Z" />
        </svg>
    );
}

interface VideoModel {
    id: string;
    title: string;
    examId?: string;
    examPassed?: boolean;
    isExamLocked?: boolean;
    exams?: { examId: string; title: string; passed: boolean; isMandatory: boolean }[];
    chapters?: import("@/services/content-service").VideoChapterDto[];
}

interface LessonCarouselProps {
    videos: VideoModel[];
    activeStep: number;
    onStepChange: (index: number) => void;
    homeworkId?: string;
    homeworkPassed?: boolean;
    examId?: string;
    examPassed?: boolean;
    isExamLocked?: boolean;
    examLockedReason?: string;
    lessonPrice?: number;
    lessonId?: string;
}

// --- Subcomponents ---
function Steps({ videos, current, onChange }: { videos: VideoModel[]; current: number; onChange: (index: number) => void; }) {
    if (videos.length <= 1) return null;

    return (
        <nav aria-label="Progress" className="flex justify-start overflow-x-auto px-4 py-4 md:px-10 md:py-6">
            <ol className="flex w-max min-w-full flex-nowrap items-start justify-start gap-3 sm:w-full sm:flex-row sm:flex-wrap" role="list">
                {videos.map((video, stepIdx) => {
                    const isCompleted = current > stepIdx;
                    const isCurrent = current === stepIdx;
                    const isExamLocked = video.isExamLocked;
                    const isFuture = !isCompleted && !isCurrent && !isExamLocked;

                    return (
                        <motion.li
                            key={video.id}
                            initial={{ opacity: 0, y: -10 }}
                            animate={{ opacity: 1, y: 0 }}
                            transition={{ duration: 0.3, delay: stepIdx * 0.1 }}
                            className={cn(
                                "relative z-50 rounded-full px-4 py-2 transition-all duration-300 ease-in-out flex items-center gap-2",
                                isCompleted ? "bg-[var(--admin-success-10)]" : "",
                                isCurrent ? "bg-[var(--admin-primary-10)] border border-[var(--admin-primary)]/20" : "",
                                isFuture ? "bg-[var(--admin-card-soft)]" : "",
                                isExamLocked ? "bg-gray-500/5 opacity-60 border border-dashed border-gray-500/20" : ""
                            )}
                        >
                            <button
                                type="button"
                                className={cn(
                                    "group flex items-center focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)]",
                                    isCurrent && "pointer-events-none"
                                )}
                                onClick={() => onChange(stepIdx)}
                                disabled={isCurrent}
                                aria-current={isCurrent ? "step" : undefined}
                            >
                                <span className="flex items-center gap-3 text-sm font-bold">
                                    <motion.span
                                        initial={false}
                                        animate={{ scale: isCurrent ? 1.15 : 1 }}
                                        className={cn(
                                            "flex h-6 w-6 shrink-0 items-center justify-center rounded-full duration-300",
                                            isCompleted && "bg-[var(--admin-success)] text-white",
                                            isCurrent && "bg-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-lg shadow-[var(--admin-primary-strong)]/30",
                                            isFuture && "bg-[var(--admin-card-strong)] text-[var(--admin-muted)] border border-[var(--admin-border)]",
                                            isExamLocked && "bg-gray-500/20 text-gray-400 border border-gray-500/30"
                                        )}
                                    >
                                        {isExamLocked ? (
                                            <Lock className="h-3.5 w-3.5" />
                                        ) : isCompleted ? (
                                            <motion.div initial={{ scale: 0 }} animate={{ scale: 1 }} transition={{ type: "spring", stiffness: 300, damping: 20 }}>
                                                <IconCheck className="h-4 w-4" />
                                            </motion.div>
                                        ) : (
                                            <span className="text-xs leading-none">{stepIdx + 1}</span>
                                        )}
                                    </motion.span>
                                    <motion.span
                                        className={clsx(
                                            "max-w-[11rem] truncate text-sm tracking-tight duration-300 sm:max-w-[13rem]",
                                            isCompleted && "text-[var(--admin-muted)]",
                                            isCurrent && "text-[var(--admin-primary)] font-black",
                                            isFuture && "text-[var(--admin-muted)] opacity-60 group-hover:opacity-100",
                                            isExamLocked && "text-gray-400 font-medium"
                                        )}
                                        title={video.title}
                                    >
                                        {video.title}
                                    </motion.span>
                                </span>
                            </button>

                            {/* Removed nested exam badges from list items per request */}
                        </motion.li>
                    );
                })}
            </ol>
        </nav>
    );
}

// --- Main Component ---
export function LessonCarousel({
    videos,
    activeStep,
    onStepChange,
    homeworkId,
    homeworkPassed,
    examId,
    examPassed,
    lessonPrice
}: LessonCarouselProps) {
    const router = useRouter();
    const params = useParams();
    const lessonId = params?.lessonId as string;
    const packageId = params?.packageId as string;
    const precedingVideoExamUnpassed = videos.slice(0, activeStep).some(v => v.examId && !v.examPassed);

    const [mounted, setMounted] = useState(false);
    const [watchStatus, setWatchStatus] = useState<WatchStatus | null>(null);
    const [mobilePanel, setMobilePanel] = useState<"chapters" | "mindmap">("chapters");
    const mouseX = useMotionValue(0);
    const mouseY = useMotionValue(0);
    const playerRef = useRef<SecureVideoPlayerRef>(null);
    const [currentTime, setCurrentTime] = useState(0);

    useEffect(() => {
        setMounted(true);
    }, []);

    function handleMouseMove({ currentTarget, clientX, clientY }: MouseEvent) {
        if (window.innerWidth <= 768) return;
        const { left, top } = currentTarget.getBoundingClientRect();
        mouseX.set(clientX - left);
        mouseY.set(clientY - top);
    }

    if (!videos || videos.length === 0) return null;

    const activeVideo = videos[activeStep];
    const hasChapters = Boolean(activeVideo.chapters && activeVideo.chapters.length > 0);
    const hasMindmaps = Boolean(activeVideo.chapters?.some((chapter) => chapter.mindmapImageUrl));

    return (
        <motion.div
            className="animated-cards relative w-full rounded-[24px]"
            onMouseMove={handleMouseMove}
            style={{
                "--x": useMotionTemplate`${mouseX}px`,
                "--y": useMotionTemplate`${mouseY}px`,
            } as CSSProperties}
        >
            <div
                className={clsx(
                    "group relative w-full overflow-hidden rounded-[24px] border border-[var(--admin-border)] bg-gradient-to-b from-[var(--admin-card)]/95 to-[var(--admin-background)] backdrop-blur-md transition duration-500",
                    "hover:border-[var(--admin-primary)]/30"
                )}
            >
                <div className="relative z-20 w-full flex flex-col xl:flex-row pb-6">

                    {/* Left Column (Titles & Animated Progress Steps) */}
                    <div className="flex w-full flex-col xl:w-[35%] shrink-0 pt-2 relative z-30">
                        {/* Exam & Homework buttons right above the steps list */}
                        {(examId || homeworkId) && (
                            <div className="flex flex-col gap-2 px-4 md:px-10 mb-2 mt-2">
                                <div className="flex gap-2">
                                    {examId && (
                                        <button
                                            type="button"
                                            disabled={precedingVideoExamUnpassed && !examPassed}
                                            onClick={() => router.push(`/student/exams/${examId}?packageId=${packageId}&lessonId=${lessonId}`)}
                                            className={cn(
                                                "flex flex-1 items-center justify-center gap-1.5 px-3 py-2.5 rounded-xl text-xs font-black transition-all hover:scale-[1.02] shadow-sm",
                                                examPassed
                                                    ? "bg-[var(--admin-success-10)] text-[var(--admin-success)] border border-[var(--admin-success-20)]"
                                                    : precedingVideoExamUnpassed
                                                    ? "bg-gray-500/10 text-gray-400 border border-gray-500/20 opacity-60 cursor-not-allowed"
                                                    : "bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] hover:bg-[var(--admin-primary-strong)]"
                                            )}
                                            title={examPassed ? "الاختبار مجتاز" : "ابدأ اختبار الدرس"}
                                        >
                                            <Award className="h-3.5 w-3.5 shrink-0" />
                                            <span>{examPassed ? "الاختبار مجتاز" : "اختبار الدرس"}</span>
                                        </button>
                                    )}
                                    {homeworkId && (
                                        <button
                                            type="button"
                                            onClick={() => router.push(`/student/homework/${homeworkId}?packageId=${packageId}&lessonId=${lessonId}`)}
                                            className={cn(
                                                "flex flex-1 items-center justify-center gap-1.5 px-3 py-2.5 rounded-xl text-xs font-black transition-all hover:scale-[1.02] shadow-sm",
                                                homeworkPassed
                                                    ? "bg-[var(--admin-success-10)] text-[var(--admin-success)] border border-[var(--admin-success-20)]"
                                                    : "bg-amber-500/15 text-amber-600 border border-amber-500/30 hover:bg-amber-500/25"
                                            )}
                                            title={homeworkPassed ? "تم اجتياز الواجب" : "حل الواجب"}
                                        >
                                            <ClipboardCheck className="h-3.5 w-3.5 shrink-0" />
                                            <span>{homeworkPassed ? "الواجب مجتاز" : "واجب الدرس"}</span>
                                        </button>
                                    )}
                                </div>
                            </div>
                        )}

                        <Steps current={activeStep} onChange={onStepChange} videos={videos} />

                        <div className="mt-4 flex flex-col gap-4 px-6 md:px-10 xl:mt-12">
                            <AnimatePresence mode="wait">
                                <motion.div
                                    key={activeStep}
                                    initial={{ opacity: 0, y: 15 }}
                                    animate={{ opacity: 1, y: 0 }}
                                    exit={{ opacity: 0, y: -15 }}
                                    transition={{ duration: 0.35, ease: [0.23, 1, 0.32, 1] }}
                                    className="space-y-4"
                                >
                                    <motion.div
                                        initial={{ opacity: 0, x: 20 }}
                                        animate={{ opacity: 1, x: 0 }}
                                        transition={{ delay: 0.1, duration: 0.4 }}
                                        className="inline-flex rounded-full bg-[var(--admin-primary)]/10 px-3 py-1 text-xs font-bold text-[var(--admin-primary)] sm:text-sm"
                                    >
                                        الفيديو {activeStep + 1} من {videos.length}
                                    </motion.div>

                                    <motion.h2
                                        initial={{ opacity: 0, x: 20 }}
                                        animate={{ opacity: 1, x: 0 }}
                                        transition={{ delay: 0.15, duration: 0.4 }}
                                        className="text-2xl sm:text-3xl font-black text-[var(--admin-text)] leading-tight tracking-tight"
                                    >
                                        {activeVideo.title}
                                    </motion.h2>

                                    <motion.div
                                        initial={{ opacity: 0, x: 20 }}
                                        animate={{ opacity: 1, x: 0 }}
                                        transition={{ delay: 0.2, duration: 0.4 }}
                                        className="h-1 w-12 rounded-full bg-[var(--admin-primary)]"
                                    />
                                </motion.div>
                            </AnimatePresence>
                        </div>
                    </div>

                    {/* Right Column (The Magic Video Area) */}
                    <div className="flex-1 mt-8 xl:mt-0 p-4 md:p-8 flex items-center justify-center relative z-20">
                        <AnimatePresence mode="wait">
                            {mounted && (
                                <motion.div
                                    key={activeVideo.id}
                                    initial={{ opacity: 0, scale: 0.96 }}
                                    animate={{ opacity: 1, scale: 1 }}
                                    exit={{ opacity: 0, scale: 0.96 }}
                                    transition={{ type: "spring", stiffness: 300, damping: 25, mass: 0.5 }}
                                    className="w-full relative z-30"
                                >
                                    <div className="relative rounded-[20px] overflow-hidden shadow-2xl border border-[var(--admin-primary)]/20 bg-black aspect-video ring-4 ring-black/5">
                                        <SecureVideoPlayer
                                            ref={playerRef}
                                            className="absolute inset-0 w-full h-full object-cover"
                                            lessonVideoId={activeVideo.id}
                                            isExamLocked={activeVideo.isExamLocked}
                                            blockingExamId={activeVideo.isExamLocked ? videos.find(v => v.examId && !v.examPassed)?.examId : undefined}
                                            videoExamId={activeVideo.examId}
                                            chapters={activeVideo.chapters}
                                            onWatchStatusChange={(s: WatchStatus) => setWatchStatus(s)}
                                            onWatchProgress={(time) => setCurrentTime(time)}
                                            onEnded={() => {
                                                if (activeStep < videos.length - 1) {
                                                    onStepChange(activeStep + 1);
                                                }
                                            }}
                                            lessonPrice={lessonPrice}
                                            lessonId={lessonId}
                                        />
                                    </div>

                                    {/* Watch Status Bar — standalone, outside the player */}
                                    <div className="mt-4">
                                        <WatchStatusBar
                                            status={watchStatus}
                                            title={activeVideo.title}
                                        />
                                    </div>

                                    {hasChapters && (
                                        <>
                                            <div className="mt-5 flex gap-2 lg:hidden">
                                                <button
                                                    type="button"
                                                    onClick={() => setMobilePanel("chapters")}
                                                    className={cn(
                                                        "min-h-11 flex-1 rounded-full px-4 text-sm font-bold transition-colors",
                                                        mobilePanel === "chapters"
                                                            ? "bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]"
                                                            : "bg-[var(--admin-card-soft)] text-[var(--admin-muted)]"
                                                    )}
                                                >
                                                    فصول الدرس
                                                </button>
                                                {hasMindmaps && (
                                                    <button
                                                        type="button"
                                                        onClick={() => setMobilePanel("mindmap")}
                                                        className={cn(
                                                            "min-h-11 flex-1 rounded-full px-4 text-sm font-bold transition-colors",
                                                            mobilePanel === "mindmap"
                                                                ? "bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]"
                                                                : "bg-[var(--admin-card-soft)] text-[var(--admin-muted)]"
                                                        )}
                                                    >
                                                        الخريطة الذهنية
                                                    </button>
                                                )}
                                            </div>

                                            <div className={cn("mt-6", mobilePanel !== "chapters" && "hidden lg:block")}>
                                                <ChapterList
                                                    chapters={activeVideo.chapters!}
                                                    currentTime={currentTime}
                                                    onSeek={(sec) => playerRef.current?.seekTo(sec)}
                                                />
                                            </div>

                                            {hasMindmaps && (
                                                <div className={cn("mt-6", mobilePanel !== "mindmap" && "hidden lg:block")}>
                                                    <LessonMindmapDisplay
                                                        chapters={activeVideo.chapters!}
                                                        currentTime={currentTime}
                                                    />
                                                </div>
                                            )}
                                        </>
                                    )}
                                </motion.div>
                            )}
                        </AnimatePresence>
                    </div>
                </div>

            </div>
        </motion.div>
    );
}
