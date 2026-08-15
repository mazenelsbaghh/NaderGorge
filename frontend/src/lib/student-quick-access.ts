import type { QuickAccessItemDto } from '@/services/student-service';

export type StudentQuickAccessKind = 'term' | 'section' | 'lesson' | 'video';

const ACCESS_KIND_BY_VALUE: Record<string, StudentQuickAccessKind> = {
  '1': 'term',
  Term: 'term',
  '2': 'section',
  Month: 'section',
  '3': 'lesson',
  Lesson: 'lesson',
  '4': 'video',
  Video: 'video',
};

export function getStudentQuickAccessKind(
  accessType: QuickAccessItemDto['accessType']
): StudentQuickAccessKind | null {
  return ACCESS_KIND_BY_VALUE[String(accessType)] ?? null;
}

export function filterStudentQuickAccessItems(
  accessItems: QuickAccessItemDto[],
  kind: StudentQuickAccessKind
): QuickAccessItemDto[] {
  return accessItems.filter(
    (accessItem) => getStudentQuickAccessKind(accessItem.accessType) === kind
  );
}
