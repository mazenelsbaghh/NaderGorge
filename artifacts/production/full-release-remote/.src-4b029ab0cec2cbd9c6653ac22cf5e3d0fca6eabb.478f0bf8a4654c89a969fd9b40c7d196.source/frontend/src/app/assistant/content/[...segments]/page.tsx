import type { ReactNode } from 'react';
import { notFound } from 'next/navigation';

import ExamDashboardPageClient from '@/app/admin/content/exams/[id]/dashboard/ExamDashboardPageClient';
import AddExamQuestionPageClient from '@/app/admin/content/exams/[id]/add-question/AddExamQuestionPageClient';
import ExamProfilePageClient from '@/app/admin/content/exams/[id]/ExamProfilePageClient';
import AddHomeworkQuestionPageClient from '@/app/admin/content/homework/[id]/add-question/AddHomeworkQuestionPageClient';
import HomeworkProfilePageClient from '@/app/admin/content/homework/[id]/HomeworkProfilePageClient';
import LessonProfilePageClient from '@/app/admin/content/lessons/[id]/LessonProfilePageClient';
import PackageProfilePageClient from '@/app/admin/content/packages/[id]/PackageProfilePageClient';
import SectionProfilePageClient from '@/app/admin/content/sections/[id]/SectionProfilePageClient';
import TermProfilePageClient from '@/app/admin/content/terms/[id]/TermProfilePageClient';
import { NavRouteGuard } from '@/components/layout/NavRouteGuard';

type AssistantContentRouteProps = {
  params: Promise<{ segments: string[] }>;
};

export default async function AssistantContentRoute({
  params,
}: AssistantContentRouteProps) {
  const { segments } = await params;
  const [resource, id, action] = segments;
  let content: ReactNode;

  if (segments.length === 2 && resource === 'packages') {
    content = <PackageProfilePageClient params={{ id }} />;
  } else if (segments.length === 2 && resource === 'terms') {
    content = <TermProfilePageClient params={{ id }} />;
  } else if (segments.length === 2 && resource === 'sections') {
    content = <SectionProfilePageClient params={{ id }} />;
  } else if (segments.length === 2 && resource === 'lessons') {
    content = <LessonProfilePageClient params={{ id }} />;
  } else if (segments.length === 2 && resource === 'exams') {
    content = <ExamProfilePageClient id={id} />;
  } else if (
    segments.length === 3 &&
    resource === 'exams' &&
    action === 'add-question'
  ) {
    content = <AddExamQuestionPageClient params={{ id }} />;
  } else if (
    segments.length === 3 &&
    resource === 'exams' &&
    action === 'dashboard'
  ) {
    content = <ExamDashboardPageClient params={{ id }} />;
  } else if (segments.length === 2 && resource === 'homework') {
    content = <HomeworkProfilePageClient id={id} />;
  } else if (
    segments.length === 3 &&
    resource === 'homework' &&
    action === 'add-question'
  ) {
    content = <AddHomeworkQuestionPageClient params={{ id }} />;
  } else {
    notFound();
  }

  return (
    <NavRouteGuard routePath="/assistant/content" permission="content.manage">
      {content}
    </NavRouteGuard>
  );
}
