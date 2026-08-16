export type StudentPackageAccess = {
  id: string;
  contentMode?: 'TermWithSections' | 'SectionWithLessons' | 'LessonsOnly' | 'SingleLesson';
  rootTermId?: string;
  hasDirectPackageAccess?: boolean;
  hasRootContentAccess?: boolean;
};

export type StudentTermAccess = {
  id: string;
  isPurchased?: boolean;
};

/**
 * A SectionWithLessons package is sold through its hidden root term. In that
 * shape `hasRootContentAccess` is authoritative even though there is no
 * package-level grant. Keep this rule shared so term and section pages cannot
 * accidentally offer the same root term for sale again.
 */
export function hasStudentTermAccess(
  pkg: StudentPackageAccess | null | undefined,
  term: StudentTermAccess | null | undefined,
): boolean {
  if (term?.isPurchased || pkg?.hasDirectPackageAccess) {
    return true;
  }

  return Boolean(
    pkg?.contentMode === 'SectionWithLessons' &&
      pkg.hasRootContentAccess &&
      pkg.rootTermId &&
      term?.id &&
      pkg.rootTermId.toLowerCase() === term.id.toLowerCase(),
  );
}
