'use client';

import { ColorSettings } from '@/components/ui/ColorSettings';
import { useStudentTheme } from '@/hooks/useStudentTheme';

export function StudentColorSettings() {
  return <ColorSettings {...useStudentTheme()} />;
}
