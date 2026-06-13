'use client';

import { ArrowLeft, ArrowRight, Star } from 'lucide-react';
import Image from 'next/image';
import Link from 'next/link';
import { useEffect, useState } from 'react';

import {
  platformStats,
  teachers as hardcodedTeachers,
  topStudents,
} from './data';
import { studentService } from '@/services/student-service';
import { resolveMediaUrl } from '@/utils/resolve-media-url';

const medalStyles: Record<number, string> = {
  1: 'bg-[#D4A017] text-white',
  2: 'bg-[#9AA6B2] text-white',
  3: 'bg-[#B87333] text-white',
};

type TeacherCard = {
  name: string;
  subject: string;
  rating: string;
  avatar: string;
};

export function CircularGallerySection() {
  const [currentIndex, setCurrentIndex] = useState(0);
  const [activeTeachers, setActiveTeachers] = useState<TeacherCard[]>([]);

  useEffect(() => {
    async function loadTeachers() {
      try {
        const list = await studentService.getPublicTeachers();
        if (list.length > 0) {
          setActiveTeachers(
            list.map((teacher) => ({
              name: teacher.fullName,
              subject:
                teacher.specialization ||
                teacher.subjectNames.join(' - ') ||
                'معلم المنصة',
              rating: '4.9',
              avatar: teacher.profileImageUrl
                ? resolveMediaUrl(teacher.profileImageUrl)
                : `https://avatar.vercel.sh/${encodeURIComponent(teacher.fullName)}`,
            }))
          );
        } else {
          setActiveTeachers([...hardcodedTeachers]);
        }
      } catch {
        setActiveTeachers([...hardcodedTeachers]);
      }
    }

    void loadTeachers();
  }, []);

  const teachers =
    activeTeachers.length > 0 ? activeTeachers : hardcodedTeachers;
  const currentStudent = topStudents[currentIndex];

  const showPreviousStudent = () => {
    setCurrentIndex(
      (index) => (index - 1 + topStudents.length) % topStudents.length
    );
  };

  const showNextStudent = () => {
    setCurrentIndex((index) => (index + 1) % topStudents.length);
  };

  return (
    <>
      <section
        id="about-platform"
        className="landing-section mt-3 px-5 py-14 md:px-12 md:py-18 lg:px-16"
      >
        <div className="relative z-10 mx-auto max-w-[1180px] text-center">
          <div>
            <h2
              id="top-students-heading"
              className="text-balance text-3xl font-black leading-tight text-[var(--landing-ink)] md:text-5xl"
            >
              الأوائل مع مسار
            </h2>
            <p className="mt-3 text-base font-bold text-[var(--landing-muted)] md:text-lg">
              نفتخر بنجاح طلابنا المتفوقين
            </p>
          </div>

          <div
            className="mt-10 flex flex-col items-center"
            role="region"
            aria-roledescription="carousel"
            aria-labelledby="top-students-heading"
          >
            <div
              className="w-full max-w-[340px]"
              aria-live="polite"
              aria-atomic="true"
            >
              <article
                className="landing-panel flex min-h-[320px] flex-col items-center border border-[var(--landing-line-strong)] px-5 py-7 text-center"
                role="group"
                aria-roledescription="slide"
                aria-label={`${currentIndex + 1} من ${topStudents.length}: ${currentStudent.name}`}
              >
                <span
                  className={`flex h-10 w-10 items-center justify-center rounded-full text-sm font-black ${
                    medalStyles[currentStudent.rank] ??
                    'bg-[#0E8F8F] text-white'
                  }`}
                >
                  {currentStudent.rank}
                </span>
                <Image
                  src={currentStudent.avatar}
                  alt={currentStudent.name}
                  width={84}
                  height={84}
                  unoptimized
                  className="mt-4 h-[84px] w-[84px] rounded-full object-cover"
                />
                <h3 className="mt-4 text-xl font-black text-[var(--landing-ink)]">
                  {currentStudent.name}
                </h3>
                <p className="mt-1 text-sm font-bold text-[var(--landing-muted)]">
                  {currentStudent.stage}
                </p>
                <strong className="mt-4 text-3xl font-black text-[var(--landing-ink)]">
                  {currentStudent.score}
                </strong>
                <span className="mt-1 text-xs font-extrabold text-[var(--landing-muted)]">
                  النسبة النهائية
                </span>
              </article>
            </div>

            <div className="mt-5 flex items-center justify-center gap-3">
              <button
                type="button"
                onClick={showPreviousStudent}
                className="inline-flex min-h-11 min-w-11 items-center justify-center rounded-full border border-[var(--landing-line-strong)] bg-[var(--landing-card)] text-[var(--landing-ink)] transition-colors hover:bg-[var(--landing-card-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#0E8F8F] focus-visible:ring-offset-2"
                aria-label="عرض الطالب السابق"
              >
                <ArrowRight className="h-5 w-5" aria-hidden="true" />
              </button>

              <div
                className="flex justify-center gap-2"
                aria-label="اختيار طالب من الأوائل"
              >
                {topStudents.map((student, index) => (
                  <button
                    key={student.name}
                    type="button"
                    onClick={() => setCurrentIndex(index)}
                    className={`h-3 w-3 rounded-full transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#0E8F8F] focus-visible:ring-offset-2 ${
                      index === currentIndex
                        ? 'bg-[#0E8F8F]'
                        : 'bg-[var(--landing-line)]'
                    }`}
                    aria-label={`عرض ${student.name}`}
                    aria-current={index === currentIndex ? 'true' : undefined}
                  />
                ))}
              </div>

              <button
                type="button"
                onClick={showNextStudent}
                className="inline-flex min-h-11 min-w-11 items-center justify-center rounded-full border border-[var(--landing-line-strong)] bg-[var(--landing-card)] text-[var(--landing-ink)] transition-colors hover:bg-[var(--landing-card-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#0E8F8F] focus-visible:ring-offset-2"
                aria-label="عرض الطالب التالي"
              >
                <ArrowLeft className="h-5 w-5" aria-hidden="true" />
              </button>
            </div>
          </div>

          <Link
            href="/register"
            className="landing-primary-button mx-auto mt-8"
          >
            عرض المزيد من الأوائل
            <ArrowLeft className="h-4 w-4" />
          </Link>
        </div>
      </section>

      <section
        id="teachers"
        className="landing-section mt-3 px-5 py-14 md:px-12 md:py-16 lg:px-16"
      >
        <div className="relative z-10 flex flex-col gap-10">
          <div className="mx-auto max-w-2xl text-center">
            <h2 className="text-balance text-3xl font-black leading-tight text-[var(--landing-ink)] md:text-5xl">
              تعلم على يد نخبة من أفضل المعلمين
            </h2>
            <p className="mt-4 text-base font-semibold leading-8 text-[var(--landing-muted)] md:text-lg">
              خبرة عالية، شغف بالتعليم، دعم مستمر، وخطة واضحة تناسب مستوى كل
              طالب.
            </p>
            <Link
              href="/register"
              className="landing-primary-button mx-auto mt-5"
            >
              استكشف جميع المعلمين
            </Link>
          </div>

          <div
            className="mx-auto grid w-full max-w-[1080px] gap-5 sm:grid-cols-2 lg:grid-cols-4"
            aria-label="معلمو منصة مسار"
          >
            {teachers.map((teacher) => (
              <article
                key={`${teacher.name}-${teacher.subject}`}
                className="landing-panel overflow-hidden border border-[var(--landing-line-strong)] text-center"
              >
                <Image
                  src={teacher.avatar}
                  alt={teacher.name}
                  width={260}
                  height={280}
                  unoptimized
                  className="h-44 w-full object-cover"
                />
                <div className="px-4 py-4">
                  <h3 className="truncate text-base font-black text-[var(--landing-ink)]">
                    {teacher.name}
                  </h3>
                  <p className="mt-1 text-xs font-bold text-[var(--landing-muted)]">
                    {teacher.subject}
                  </p>
                  <div className="mt-3 flex items-center justify-center gap-1 text-sm font-black text-[var(--landing-ink)]">
                    {teacher.rating}
                    <Star
                      className="h-4 w-4 fill-[#D4A017] text-[#D4A017]"
                      aria-hidden="true"
                    />
                  </div>
                </div>
              </article>
            ))}
          </div>

          <div className="mx-auto mt-6 grid w-full max-w-4xl grid-cols-2 gap-6 sm:grid-cols-4">
            {platformStats.map(({ value, label, icon: Icon }) => (
              <div
                key={label}
                className="rounded-xl border border-[var(--landing-line-strong)] bg-[var(--landing-card)] p-4 text-center"
              >
                <Icon className="mx-auto h-6 w-6 text-[#0E8F8F]" />
                <strong className="mt-2 block text-xl font-black text-[var(--landing-ink)]">
                  {value}
                </strong>
                <span className="text-xs font-extrabold text-[var(--landing-muted)]">
                  {label}
                </span>
              </div>
            ))}
          </div>
        </div>
      </section>
    </>
  );
}
