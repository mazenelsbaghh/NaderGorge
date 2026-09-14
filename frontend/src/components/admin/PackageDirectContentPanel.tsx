'use client';

import { BookOpenText, Folder } from 'lucide-react';
import { ContentHierarchyPanel, type HierarchyItem } from './ContentHierarchyPanel';
import { adminService } from '@/services/admin-service';
import {
  type PackageContentMode,
  type PackageDirectLessonDto,
  type PackageDirectSectionDto,
} from '@/services/content-service';
import toast from 'react-hot-toast';

interface PackageDirectContentPanelProps {
  packageId: string;
  mode: PackageContentMode;
  rootTermId?: string;
  rootSectionId?: string;
  sections?: PackageDirectSectionDto[];
  lessons?: PackageDirectLessonDto[];
  basePath: '/admin/content' | '/teacher/packages';
  onChanged: () => Promise<void>;
}

export function PackageDirectContentPanel({
  packageId,
  mode,
  rootTermId,
  rootSectionId,
  sections = [],
  lessons = [],
  basePath,
  onChanged,
}: PackageDirectContentPanelProps) {
  const showSections = mode === 'SectionWithLessons';
  const showLessons = mode === 'LessonsOnly' || mode === 'SingleLesson';
  const sectionItems: HierarchyItem[] = sections.map((section) => ({
    id: section.id,
    title: section.title,
    order: section.order,
    price: section.price,
    imageUrl: section.imageUrl,
    href: `${basePath}/sections/${section.id}`,
    archiveMode: section.archiveMode,
    archivedAt: section.archivedAt,
    archiveTargetType: 'Section',
  }));
  const lessonItems: HierarchyItem[] = lessons.map((lesson) => ({
    id: lesson.id,
    title: lesson.title,
    order: lesson.order,
    price: lesson.price,
    subtitle: lesson.summary || undefined,
    href: `${basePath}/lessons/${lesson.id}`,
    archiveMode: lesson.archiveMode,
    archivedAt: lesson.archivedAt,
    archiveTargetType: 'Lesson',
  }));

  if (!showSections && !showLessons) {
    return (
      <div className="rounded-2xl border border-dashed border-[var(--admin-border)] p-6 text-sm text-[var(--admin-muted)]">
        هذا الكورس يستخدم هيكل الترم والأقسام والحصص. أضف الترم من تبويب الأترام.
      </div>
    );
  }

  if (showSections && !rootTermId) {
    return <p className="text-sm text-red-500">تعذر تجهيز مساحة الأقسام المباشرة.</p>;
  }

  if (showLessons && !rootSectionId) {
    return <p className="text-sm text-red-500">تعذر تجهيز مساحة الحصص المباشرة.</p>;
  }

  return (
    <div className="space-y-6">
      {showSections && rootTermId && (
        <ContentHierarchyPanel
          label="الأقسام المباشرة"
          icon={<Folder className="h-5 w-5" />}
          items={sectionItems}
          loading={false}
          loadError={false}
          hasImage
          emptyDescription="أضف القسم الأول ليظهر مباشرة داخل هذا الكورس."
          addPlaceholder="اسم القسم، مثال: الوحدة الأولى..."
          onCreate={async ({ title, order, price, imageFile }) => {
            const sectionId = await adminService.createSection({ packageId, termId: rootTermId, title, order, price });
            if (imageFile && sectionId) {
              await adminService.uploadContentImage('section', sectionId, imageFile);
            }
            toast.success('تمت إضافة القسم المباشر.');
            await onChanged();
          }}
          onUpdate={async (id, { title, order, price }) => {
            await adminService.updateSection(id, { title, order, price });
            toast.success('تم تحديث القسم.');
            await onChanged();
          }}
          onImageUpload={async (id, file) => {
            await adminService.uploadContentImage('section', id, file);
            await onChanged();
          }}
          onRetry={() => void onChanged()}
          onArchiveChanged={onChanged}
        />
      )}

      {showLessons && rootSectionId && (
        <ContentHierarchyPanel
          label={mode === 'SingleLesson' ? 'الحصة المستقلة' : 'الحصص المباشرة'}
          icon={<BookOpenText className="h-5 w-5" />}
          items={lessonItems}
          loading={false}
          loadError={false}
          hasSummary
          canCreate={mode !== 'SingleLesson'}
          emptyDescription={mode === 'SingleLesson' ? 'تعذر تحميل الحصة المستقلة.' : 'أضف الحصة الأولى لتظهر مباشرة داخل هذا القسم.'}
          addPlaceholder="عنوان الحصة، مثال: مقدمة الدرس..."
          onCreate={async ({ title, summary, order, price }) => {
            await adminService.createLesson({
              sectionId: rootSectionId,
              title,
              summary: summary ?? '',
              order,
              price,
            });
            toast.success('تمت إضافة الحصة المباشرة.');
            await onChanged();
          }}
          onUpdate={mode === 'SingleLesson' ? undefined : async (id, { title, summary, order, price }) => {
              await adminService.updateLesson(id, { title, summary: summary ?? '', order, price });
              toast.success('تم تحديث الحصة.');
              await onChanged();
            }}
          onRetry={() => void onChanged()}
          onArchiveChanged={onChanged}
        />
      )}
    </div>
  );
}
