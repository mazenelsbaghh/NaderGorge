'use client';

import {
  ArrowLeft,
  ArrowUpLeft,
  BookOpenCheck,
  ChevronLeft,
  ChevronRight,
  Star,
} from 'lucide-react';
import { motion, useReducedMotion } from 'framer-motion';
import Image from 'next/image';
import Link from 'next/link';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { platformStats, teachers as hardcodedTeachers } from './data';
import { studentService } from '@/services/student-service';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import {
  EDUCATION_STAGE_LABELS,
  GRADE_LEVEL_LABELS,
  STUDY_TRACK_LABELS,
} from '@/lib/academic-labels';

type TeacherCard = {
  id?: string;
  name: string;
  subject: string;
  rating: string;
  avatar: string;
};

const revealEase = [0.22, 1, 0.36, 1] as const;
const carouselEase = [0.16, 1, 0.3, 1] as const;

const SUBJECT_LABELS: Record<string, string> = {
  Arabic: 'اللغة العربية',
  Biology: 'الأحياء',
  Chemistry: 'الكيمياء',
  English: 'اللغة الإنجليزية',
  French: 'اللغة الفرنسية',
  Geography: 'الجغرافيا',
  History: 'التاريخ',
  Mathematics: 'الرياضيات',
  Physics: 'الفيزياء',
  Science: 'العلوم',
};

function localizeTeacherFocus(value: string) {
  const labels = {
    ...EDUCATION_STAGE_LABELS,
    ...GRADE_LEVEL_LABELS,
    ...STUDY_TRACK_LABELS,
    ...SUBJECT_LABELS,
  };
  const localized = value
    .split(/\s*[,/\\-|]\s*/)
    .map((item) => labels[item] ?? item)
    .filter(Boolean);
  return localized.some((item) => /[A-Za-z]/.test(item))
    ? 'شرح ومراجعة منظمة'
    : localized.join(' • ') || 'شرح ومراجعة منظمة';
}

function circularOffset(index: number, activeIndex: number, length: number) {
  let offset = index - activeIndex;
  if (offset > length / 2) offset -= length;
  if (offset < -length / 2) offset += length;
  return offset;
}

export function CircularGallerySection() {
  const [activeTeachers, setActiveTeachers] = useState<TeacherCard[]>([]);
  const [currentIndex, setCurrentIndex] = useState(0);
  const touchStart = useRef<number | null>(null);
  const prefersReducedMotion = useReducedMotion();

  useEffect(() => {
    async function loadTeachers() {
      try {
        const landingTeachers = await studentService.getLandingTeachers();
        const list =
          landingTeachers.length > 0
            ? landingTeachers
            : await studentService.getPublicTeachers();
        setActiveTeachers(
          list.length > 0
            ? list.map((teacher) => ({
                name: teacher.fullName,
                id: teacher.slug || teacher.teacherId || teacher.id,
                subject: localizeTeacherFocus(
                  teacher.specialization || teacher.subjectNames.join(',')
                ),
                rating: '4.9',
                avatar: teacher.profileImageUrl
                  ? resolveMediaUrl(teacher.profileImageUrl)
                  : `https://avatar.vercel.sh/${encodeURIComponent(teacher.fullName)}`,
              }))
            : [...hardcodedTeachers]
        );
      } catch {
        setActiveTeachers([...hardcodedTeachers]);
      }
    }
    void loadTeachers();
  }, []);

  const teachers = useMemo<TeacherCard[]>(
    () => (activeTeachers.length > 0 ? activeTeachers : [...hardcodedTeachers]),
    [activeTeachers]
  );
  const paginate = useCallback(
    (direction: number) => {
      setCurrentIndex(
        (index) => (index + direction + teachers.length) % teachers.length
      );
    },
    [teachers.length]
  );

  useEffect(() => {
    if (prefersReducedMotion || teachers.length < 2) return;
    const timer = window.setInterval(() => paginate(1), 2600);
    return () => window.clearInterval(timer);
  }, [paginate, prefersReducedMotion, teachers.length]);

  useEffect(() => {
    setCurrentIndex((index) =>
      Math.min(index, Math.max(teachers.length - 1, 0))
    );
  }, [teachers.length]);

  const activeTeacher = teachers[currentIndex];

  return (
    <section
      id="teachers"
      className="landing-section landing-section--teachers mt-3 overflow-hidden px-5 py-14 md:px-12 md:py-18 lg:px-16"
    >
      <div className="relative z-10 mx-auto max-w-[1240px]">
        <motion.div
          initial={prefersReducedMotion ? false : { opacity: 0, y: 18 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, margin: '-80px' }}
          transition={{ duration: 0.55, ease: revealEase }}
          className="flex flex-col"
        >
          <div className="mx-auto max-w-2xl text-center">
            <span className="inline-flex items-center gap-2 text-sm font-black text-[var(--landing-accent)]">
              <span className="h-2 w-2 rounded-full bg-[#D4A017]" /> اختَر من
              فريقك التعليمي
            </span>
            <h2 className="mt-3 text-3xl font-black leading-tight text-[var(--landing-ink)] md:text-4xl">
              معلم يشرح لك بالطريقة التي تناسبك
            </h2>
            <p className="mt-4 text-base font-semibold leading-8 text-[var(--landing-muted)] md:text-lg">
              تعرّف على المعلمين، موادهم، وتقييمات الطلاب، ثم ابدأ مع الشخص
              المناسب لخطتك الدراسية.
            </p>
          </div>

          <div
            role="region"
            aria-roledescription="carousel"
            aria-label="معلمو منصة مسار"
            tabIndex={0}
            onKeyDown={(event) => {
              if (event.key === 'ArrowLeft') {
                event.preventDefault();
                paginate(1);
              }
              if (event.key === 'ArrowRight') {
                event.preventDefault();
                paginate(-1);
              }
            }}
            onTouchStart={(event) => {
              touchStart.current = event.touches[0]?.clientX ?? null;
            }}
            onTouchEnd={(event) => {
              const start = touchStart.current;
              const end = event.changedTouches[0]?.clientX;
              touchStart.current = null;
              if (
                start === null ||
                end === undefined ||
                Math.abs(start - end) < 48
              )
                return;
              paginate(start > end ? 1 : -1);
            }}
            className="relative isolate mt-8 h-[45rem] outline-none sm:h-[48rem] lg:mt-10 lg:h-[51rem]"
          >
            <div
              aria-hidden="true"
              className="absolute inset-x-8 top-[47%] h-px bg-[linear-gradient(90deg,transparent,rgba(14,143,143,.32),transparent)]"
            />
            <div className="absolute inset-x-0 top-4 h-[34rem] [perspective:1600px] sm:top-6 sm:h-[38rem] lg:h-[40rem]">
              {teachers.map((teacher, index) => {
                const offset = circularOffset(
                  index,
                  currentIndex,
                  teachers.length
                );
                const distance = Math.abs(offset);
                const hidden = distance > 2;
                const x =
                  offset === 0
                    ? 0
                    : Math.sign(offset) * (distance === 2 ? 590 : 320);
                const href = teacher.id
                  ? `/teachers/${teacher.id}`
                  : '/teachers';
                return (
                  <motion.div
                    key={`${teacher.id ?? teacher.name}-${index}`}
                    className="absolute left-1/2 top-1/2 w-[min(21rem,88vw)] -translate-x-1/2 -translate-y-1/2 lg:w-[28rem]"
                    initial={false}
                    animate={{
                      x: hidden ? Math.sign(offset || 1) * 900 : x,
                      z: hidden ? -400 : -distance * 150,
                      rotateY: offset * -14,
                      scale: distance === 0 ? 1 : distance === 1 ? 0.78 : 0.56,
                      opacity: hidden
                        ? 0
                        : distance === 0
                          ? 1
                          : distance === 1
                            ? 0.68
                            : 0.28,
                    }}
                    transition={
                      prefersReducedMotion
                        ? { duration: 0 }
                        : { duration: 0.85, ease: carouselEase }
                    }
                    style={{
                      zIndex: 10 - distance,
                      pointerEvents: hidden ? 'none' : 'auto',
                    }}
                  >
                    <Link
                      href={href}
                      tabIndex={hidden ? -1 : undefined}
                      aria-label={
                        teacher.id
                          ? `عرض بروفايل ${teacher.name}`
                          : 'عرض قائمة المعلمين'
                      }
                      className="group block overflow-hidden rounded-[1.4rem] bg-[var(--landing-card-strong)] shadow-[0_20px_42px_rgba(10,29,61,.18)] ring-1 ring-[var(--landing-line)] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-[var(--landing-accent)]"
                    >
                      <div className="relative h-[26rem] lg:h-[34rem]">
                        <Image
                          src={teacher.avatar}
                          alt={distance === 0 ? teacher.name : ''}
                          width={384}
                          height={400}
                          unoptimized
                          className="h-full w-full object-cover transition-transform duration-500 group-hover:scale-105"
                        />
                      </div>
                    </Link>
                  </motion.div>
                );
              })}
            </div>

            <div className="absolute inset-x-0 bottom-8 translate-y-10 text-center sm:bottom-5">
              {activeTeacher && (
                <motion.div
                  key={activeTeacher.name}
                  initial={prefersReducedMotion ? false : { opacity: 0, y: 8 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ duration: 0.3, ease: carouselEase }}
                >
                  <h3 className="text-2xl font-black text-[var(--landing-ink)]">
                    {activeTeacher.name}
                  </h3>
                  <span className="mx-auto mt-2 flex w-fit items-center rounded-full bg-[var(--landing-gold-soft)] px-2.5 py-1 text-xs font-black text-[var(--public-achievement)]">
                    معلم مميز
                  </span>
                  <span className="mx-auto mt-2 flex w-fit items-center gap-1 rounded-md bg-[var(--primary)] px-2.5 py-1.5 text-xs font-black text-[var(--primary-foreground)]">
                    <Star
                      className="h-3.5 w-3.5 fill-[#D4A017] text-[#D4A017]"
                      aria-hidden="true"
                    />
                    {activeTeacher.rating}
                  </span>
                  <p className="mt-2 inline-flex items-center gap-2 text-sm font-bold text-[var(--landing-muted)]">
                    <BookOpenCheck
                      className="h-4 w-4 text-[var(--landing-accent)]"
                      aria-hidden="true"
                    />
                    {activeTeacher.subject}
                  </p>
                </motion.div>
              )}
              <div className="mt-4 flex items-center justify-center gap-2.5">
                <button
                  type="button"
                  onClick={() => paginate(-1)}
                  aria-label="المعلم السابق"
                  className="grid h-10 w-10 place-items-center rounded-full border border-[var(--landing-line-strong)] bg-[var(--landing-card-strong)] text-[var(--landing-ink)] transition-colors hover:border-[var(--landing-accent)] hover:text-[var(--landing-accent)] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--landing-accent)]"
                >
                  <ChevronRight className="h-5 w-5" />
                </button>
                <div
                  className="flex items-center gap-1.5"
                  aria-label={`المعلم ${currentIndex + 1} من ${teachers.length}`}
                >
                  {teachers.map((teacher, index) => (
                    <button
                      type="button"
                      key={teacher.id ?? teacher.name}
                      onClick={() => setCurrentIndex(index)}
                      aria-label={`عرض ${teacher.name}`}
                      aria-current={index === currentIndex ? 'true' : undefined}
                      className={`h-2 rounded-full transition-all ${index === currentIndex ? 'w-6 bg-[var(--landing-accent)]' : 'w-2 bg-[color-mix(in_srgb,var(--landing-ink)_20%,transparent)] hover:bg-[color-mix(in_srgb,var(--landing-ink)_45%,transparent)]'}`}
                    />
                  ))}
                </div>
                <button
                  type="button"
                  onClick={() => paginate(1)}
                  aria-label="المعلم التالي"
                  className="grid h-10 w-10 place-items-center rounded-full border border-[var(--landing-line-strong)] bg-[var(--landing-card-strong)] text-[var(--landing-ink)] transition-colors hover:border-[var(--landing-accent)] hover:text-[var(--landing-accent)] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--landing-accent)]"
                >
                  <ChevronLeft className="h-5 w-5" />
                </button>
              </div>
            </div>
          </div>

          <div className="mt-2 text-center sm:mt-4">
            <Link href="/teachers" className="landing-primary-button">
              تصفّح جميع المعلمين <ArrowLeft className="h-4 w-4" />
            </Link>
          </div>
        </motion.div>

        <motion.div
          initial={prefersReducedMotion ? false : { opacity: 0, y: 14 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, margin: '-70px' }}
          transition={{
            delay: prefersReducedMotion ? 0 : 0.12,
            duration: 0.5,
            ease: revealEase,
          }}
          className="mt-10 grid grid-cols-2 border-y border-[var(--landing-line-strong)] sm:grid-cols-4"
        >
          {platformStats.map(({ value, label, icon: Icon }) => (
            <div
              key={label}
              className="flex min-h-28 flex-col items-center justify-center px-3 text-center not-last:border-l not-last:border-[var(--landing-line-strong)]"
            >
              <Icon className="h-5 w-5 text-[var(--landing-accent)]" aria-hidden="true" />
              <strong className="mt-2 text-xl font-black text-[var(--landing-ink)]">
                {value}
              </strong>
              <span className="mt-1 text-xs font-extrabold text-[var(--landing-muted)]">
                {label}
              </span>
            </div>
          ))}
        </motion.div>
        <Link
          href="/register"
          className="mt-6 inline-flex items-center gap-2 text-sm font-black text-[var(--landing-accent)] transition-colors hover:text-[var(--landing-ink)]"
        >
          ابدأ رحلتك التعليمية <ArrowUpLeft className="h-4 w-4" />
        </Link>
      </div>
    </section>
  );
}
