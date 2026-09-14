export type EducationStage =
  | 'Secondary'
  | 'Baccalaureate'
  | 'Primary'
  | 'Preparatory'
  | 'Azhari'
  | 'American';

export type GradeLevel =
  | 'FirstSecondary'
  | 'SecondSecondary'
  | 'SecondaryGrade3'
  | 'ThirdSecondary'
  | 'FirstBaccalaureate'
  | 'SecondBaccalaureate'
  | 'PrimaryGrade1' | 'PrimaryGrade2' | 'PrimaryGrade3'
  | 'PrimaryGrade4' | 'PrimaryGrade5' | 'PrimaryGrade6'
  | 'FirstPrimary' | 'SecondPrimary' | 'ThirdPrimary'
  | 'FourthPrimary' | 'FifthPrimary' | 'SixthPrimary'
  | 'PrepGrade1' | 'PrepGrade2' | 'PrepGrade3'
  | 'FirstPreparatory' | 'SecondPreparatory' | 'ThirdPreparatory'
  | 'AzhariPrimary1' | 'AzhariPrimary2' | 'AzhariPrimary3'
  | 'AzhariPrimary4' | 'AzhariPrimary5' | 'AzhariPrimary6'
  | 'AzhariPrep1' | 'AzhariPrep2' | 'AzhariPrep3'
  | 'AzhariSecondary1' | 'AzhariSecondary2' | 'AzhariSecondary3'
  | 'AmericanGrade1' | 'AmericanGrade2' | 'AmericanGrade3'
  | 'AmericanGrade4' | 'AmericanGrade5' | 'AmericanGrade6'
  | 'AmericanGrade7' | 'AmericanGrade8' | 'AmericanGrade9'
  | 'AmericanGrade10' | 'AmericanGrade11' | 'AmericanGrade12';

export type StudyTrack =
  | 'Arts'
  | 'Science'
  | 'MedicineAndLifeSciences'
  | 'EngineeringAndComputerScience'
  | 'Business'
  | 'ArtsAndHumanities';

export interface AcademicData {
  educationStage: EducationStage | '';
  gradeLevel: GradeLevel | '';
  studyTrack: StudyTrack | '';
}

export type AcademicScopeLevel = 'Exact' | 'PlatformWide' | 'StageWide' | 'GradeAllSubjects';

export interface AcademicScopePayload {
  scopeLevel: AcademicScopeLevel;
  educationStage?: EducationStage | null;
  gradeLevel?: GradeLevel | null;
  subjectId?: string | null;
}

export interface AcademicScopeSummary {
  scopeLevel: AcademicScopeLevel;
  educationStage?: EducationStage | null;
  gradeLevel?: GradeLevel | null;
  subjectId?: string | null;
  label?: string | null;
}

export interface GradeGroup {
  groupLabel?: string;
  grades: { value: GradeLevel; label: string }[];
}

export const EDUCATION_STAGE_LABELS: Record<string, string> = {
  Primary: 'ابتدائي',
  Preparatory: 'إعدادي',
  Secondary: 'ثانوي',
  Baccalaureate: 'بكالوريا',
  Azhari: 'أزهري',
  American: 'أمريكي',
};

export const GRADE_LEVEL_LABELS: Record<string, string> = {
  FirstPrimary: 'الأول الابتدائي',
  SecondPrimary: 'الثاني الابتدائي',
  ThirdPrimary: 'الثالث الابتدائي',
  FourthPrimary: 'الرابع الابتدائي',
  FifthPrimary: 'الخامس الابتدائي',
  SixthPrimary: 'السادس الابتدائي',
  PrimaryGrade1: 'الأول الابتدائي',
  PrimaryGrade2: 'الثاني الابتدائي',
  PrimaryGrade3: 'الثالث الابتدائي',
  PrimaryGrade4: 'الرابع الابتدائي',
  PrimaryGrade5: 'الخامس الابتدائي',
  PrimaryGrade6: 'السادس الابتدائي',
  FirstPreparatory: 'الأول الإعدادي',
  SecondPreparatory: 'الثاني الإعدادي',
  ThirdPreparatory: 'الثالث الإعدادي',
  PrepGrade1: 'الأول الإعدادي',
  PrepGrade2: 'الثاني الإعدادي',
  PrepGrade3: 'الثالث الإعدادي',
  FirstSecondary: 'الأول الثانوي',
  SecondSecondary: 'الثاني الثانوي',
  SecondaryGrade3: 'الثالث الثانوي',
  ThirdSecondary: 'الثالث الثانوي',
  FirstBaccalaureate: 'الأول بكالوريا',
  SecondBaccalaureate: 'الثاني بكالوريا',
  AzhariPrimary1: 'الأول الابتدائي الأزهري',
  AzhariPrimary2: 'الثاني الابتدائي الأزهري',
  AzhariPrimary3: 'الثالث الابتدائي الأزهري',
  AzhariPrimary4: 'الرابع الابتدائي الأزهري',
  AzhariPrimary5: 'الخامس الابتدائي الأزهري',
  AzhariPrimary6: 'السادس الابتدائي الأزهري',
  AzhariPrep1: 'الأول الإعدادي الأزهري',
  AzhariPrep2: 'الثاني الإعدادي الأزهري',
  AzhariPrep3: 'الثالث الإعدادي الأزهري',
  AzhariSecondary1: 'الأول الثانوي الأزهري',
  AzhariSecondary2: 'الثاني الثانوي الأزهري',
  AzhariSecondary3: 'الثالث الثانوي الأزهري',
  AmericanGrade1: 'الصف الأول الأمريكي',
  AmericanGrade2: 'الصف الثاني الأمريكي',
  AmericanGrade3: 'الصف الثالث الأمريكي',
  AmericanGrade4: 'الصف الرابع الأمريكي',
  AmericanGrade5: 'الصف الخامس الأمريكي',
  AmericanGrade6: 'الصف السادس الأمريكي',
  AmericanGrade7: 'الصف السابع الأمريكي',
  AmericanGrade8: 'الصف الثامن الأمريكي',
  AmericanGrade9: 'الصف التاسع الأمريكي',
  AmericanGrade10: 'الصف العاشر الأمريكي',
  AmericanGrade11: 'الصف الحادي عشر الأمريكي',
  AmericanGrade12: 'الصف الثاني عشر الأمريكي',
  '1st Secondary': 'الأول الثانوي',
  '2nd Secondary': 'الثاني الثانوي',
  '3rd Secondary': 'الثالث الثانوي',
  Grade6: 'السادس الابتدائي',
  Grade7: 'الأول الإعدادي',
  Grade8: 'الثاني الإعدادي',
  Grade9: 'الثالث الإعدادي',
};

export const STUDY_TRACK_LABELS: Record<string, string> = {
  Arts: 'أدبي',
  Science: 'علمي',
  MedicineAndLifeSciences: 'الطب وعلوم الحياة',
  EngineeringAndComputerScience: 'الهندسة وعلوم الحاسب',
  Business: 'قطاع الأعمال',
  ArtsAndHumanities: 'الآداب والفنون',
};

export const ACADEMIC_SCOPE_LEVEL_LABELS: Record<AcademicScopeLevel, string> = {
  PlatformWide: 'عام للمنصة',
  StageWide: 'عام لمرحلة',
  GradeAllSubjects: 'عام لكل مواد صف',
  Exact: 'مرحلة وصف ومادة',
};

export function getAcademicScopeLabel(scope: AcademicScopeSummary | AcademicScopePayload): string {
  if ('label' in scope && scope.label) return scope.label;
  if (scope.scopeLevel === 'PlatformWide') return ACADEMIC_SCOPE_LEVEL_LABELS.PlatformWide;

  const stage = scope.educationStage ? EDUCATION_STAGE_LABELS[scope.educationStage] ?? scope.educationStage : '';
  const grade = scope.gradeLevel ? GRADE_LEVEL_LABELS[scope.gradeLevel] ?? scope.gradeLevel : '';

  if (scope.scopeLevel === 'StageWide') return stage ? `عام لمرحلة ${stage}` : ACADEMIC_SCOPE_LEVEL_LABELS.StageWide;
  if (scope.scopeLevel === 'GradeAllSubjects') return grade ? `عام لكل مواد ${grade}` : ACADEMIC_SCOPE_LEVEL_LABELS.GradeAllSubjects;
  return [stage, grade, scope.subjectId ? 'مادة محددة' : 'مادة غير محددة'].filter(Boolean).join(' / ');
}

export const STAGE_OPTIONS: { value: EducationStage; label: string }[] = [
  { value: 'Secondary', label: EDUCATION_STAGE_LABELS.Secondary },
  { value: 'Baccalaureate', label: EDUCATION_STAGE_LABELS.Baccalaureate },
  { value: 'Primary', label: EDUCATION_STAGE_LABELS.Primary },
  { value: 'Preparatory', label: EDUCATION_STAGE_LABELS.Preparatory },
  { value: 'Azhari', label: EDUCATION_STAGE_LABELS.Azhari },
  { value: 'American', label: EDUCATION_STAGE_LABELS.American },
];

export const GRADES_BY_STAGE: Record<EducationStage, GradeGroup[]> = {
  Secondary: [{
    grades: [
      { value: 'FirstSecondary', label: GRADE_LEVEL_LABELS.FirstSecondary },
      { value: 'SecondSecondary', label: GRADE_LEVEL_LABELS.SecondSecondary },
      { value: 'SecondaryGrade3', label: GRADE_LEVEL_LABELS.SecondaryGrade3 },
    ],
  }],
  Baccalaureate: [{
    grades: [
      { value: 'FirstBaccalaureate', label: GRADE_LEVEL_LABELS.FirstBaccalaureate },
      { value: 'SecondBaccalaureate', label: GRADE_LEVEL_LABELS.SecondBaccalaureate },
    ],
  }],
  Primary: [{
    grades: [
      { value: 'PrimaryGrade1', label: GRADE_LEVEL_LABELS.PrimaryGrade1 },
      { value: 'PrimaryGrade2', label: GRADE_LEVEL_LABELS.PrimaryGrade2 },
      { value: 'PrimaryGrade3', label: GRADE_LEVEL_LABELS.PrimaryGrade3 },
      { value: 'PrimaryGrade4', label: GRADE_LEVEL_LABELS.PrimaryGrade4 },
      { value: 'PrimaryGrade5', label: GRADE_LEVEL_LABELS.PrimaryGrade5 },
      { value: 'PrimaryGrade6', label: GRADE_LEVEL_LABELS.PrimaryGrade6 },
    ],
  }],
  Preparatory: [{
    grades: [
      { value: 'PrepGrade1', label: GRADE_LEVEL_LABELS.PrepGrade1 },
      { value: 'PrepGrade2', label: GRADE_LEVEL_LABELS.PrepGrade2 },
      { value: 'PrepGrade3', label: GRADE_LEVEL_LABELS.PrepGrade3 },
    ],
  }],
  Azhari: [
    {
      groupLabel: 'ابتدائي أزهري',
      grades: [
        { value: 'AzhariPrimary1', label: GRADE_LEVEL_LABELS.AzhariPrimary1 },
        { value: 'AzhariPrimary2', label: GRADE_LEVEL_LABELS.AzhariPrimary2 },
        { value: 'AzhariPrimary3', label: GRADE_LEVEL_LABELS.AzhariPrimary3 },
        { value: 'AzhariPrimary4', label: GRADE_LEVEL_LABELS.AzhariPrimary4 },
        { value: 'AzhariPrimary5', label: GRADE_LEVEL_LABELS.AzhariPrimary5 },
        { value: 'AzhariPrimary6', label: GRADE_LEVEL_LABELS.AzhariPrimary6 },
      ],
    },
    {
      groupLabel: 'إعدادي أزهري',
      grades: [
        { value: 'AzhariPrep1', label: GRADE_LEVEL_LABELS.AzhariPrep1 },
        { value: 'AzhariPrep2', label: GRADE_LEVEL_LABELS.AzhariPrep2 },
        { value: 'AzhariPrep3', label: GRADE_LEVEL_LABELS.AzhariPrep3 },
      ],
    },
    {
      groupLabel: 'ثانوي أزهري',
      grades: [
        { value: 'AzhariSecondary1', label: GRADE_LEVEL_LABELS.AzhariSecondary1 },
        { value: 'AzhariSecondary2', label: GRADE_LEVEL_LABELS.AzhariSecondary2 },
        { value: 'AzhariSecondary3', label: GRADE_LEVEL_LABELS.AzhariSecondary3 },
      ],
    },
  ],
  American: [{
    grades: [
      { value: 'AmericanGrade1', label: GRADE_LEVEL_LABELS.AmericanGrade1 },
      { value: 'AmericanGrade2', label: GRADE_LEVEL_LABELS.AmericanGrade2 },
      { value: 'AmericanGrade3', label: GRADE_LEVEL_LABELS.AmericanGrade3 },
      { value: 'AmericanGrade4', label: GRADE_LEVEL_LABELS.AmericanGrade4 },
      { value: 'AmericanGrade5', label: GRADE_LEVEL_LABELS.AmericanGrade5 },
      { value: 'AmericanGrade6', label: GRADE_LEVEL_LABELS.AmericanGrade6 },
      { value: 'AmericanGrade7', label: GRADE_LEVEL_LABELS.AmericanGrade7 },
      { value: 'AmericanGrade8', label: GRADE_LEVEL_LABELS.AmericanGrade8 },
      { value: 'AmericanGrade9', label: GRADE_LEVEL_LABELS.AmericanGrade9 },
      { value: 'AmericanGrade10', label: GRADE_LEVEL_LABELS.AmericanGrade10 },
      { value: 'AmericanGrade11', label: GRADE_LEVEL_LABELS.AmericanGrade11 },
      { value: 'AmericanGrade12', label: GRADE_LEVEL_LABELS.AmericanGrade12 },
    ],
  }],
};

export const TRACKS_BY_GRADE: Record<string, { value: StudyTrack; label: string }[]> = {
  SecondSecondary: [
    { value: 'Arts', label: STUDY_TRACK_LABELS.Arts },
    { value: 'Science', label: STUDY_TRACK_LABELS.Science },
  ],
  SecondBaccalaureate: [
    { value: 'MedicineAndLifeSciences', label: STUDY_TRACK_LABELS.MedicineAndLifeSciences },
    { value: 'EngineeringAndComputerScience', label: STUDY_TRACK_LABELS.EngineeringAndComputerScience },
    { value: 'Business', label: STUDY_TRACK_LABELS.Business },
    { value: 'ArtsAndHumanities', label: STUDY_TRACK_LABELS.ArtsAndHumanities },
  ],
};

export const TEACHER_GRADE_GROUPS = [
  {
    label: 'المرحلة الثانوية العامة',
    grades: [
      { value: 'FirstSecondary', label: GRADE_LEVEL_LABELS.FirstSecondary },
      { value: 'SecondSecondary', label: GRADE_LEVEL_LABELS.SecondSecondary },
      { value: 'SecondaryGrade3', label: GRADE_LEVEL_LABELS.SecondaryGrade3 },
    ],
  },
  {
    label: EDUCATION_STAGE_LABELS.Baccalaureate,
    grades: [
      { value: 'FirstBaccalaureate', label: GRADE_LEVEL_LABELS.FirstBaccalaureate },
      { value: 'SecondBaccalaureate', label: GRADE_LEVEL_LABELS.SecondBaccalaureate },
    ],
  },
  {
    label: 'المرحلة الإعدادية',
    grades: [
      { value: 'PrepGrade1', label: GRADE_LEVEL_LABELS.PrepGrade1 },
      { value: 'PrepGrade2', label: GRADE_LEVEL_LABELS.PrepGrade2 },
      { value: 'PrepGrade3', label: GRADE_LEVEL_LABELS.PrepGrade3 },
    ],
  },
  {
    label: 'المرحلة الابتدائية',
    grades: [
      { value: 'PrimaryGrade1', label: GRADE_LEVEL_LABELS.PrimaryGrade1 },
      { value: 'PrimaryGrade2', label: GRADE_LEVEL_LABELS.PrimaryGrade2 },
      { value: 'PrimaryGrade3', label: GRADE_LEVEL_LABELS.PrimaryGrade3 },
      { value: 'PrimaryGrade4', label: GRADE_LEVEL_LABELS.PrimaryGrade4 },
      { value: 'PrimaryGrade5', label: GRADE_LEVEL_LABELS.PrimaryGrade5 },
      { value: 'PrimaryGrade6', label: GRADE_LEVEL_LABELS.PrimaryGrade6 },
    ],
  },
  {
    label: 'التعليم الأزهري',
    grades: [
      { value: 'AzhariSecondary1', label: GRADE_LEVEL_LABELS.AzhariSecondary1 },
      { value: 'AzhariSecondary2', label: GRADE_LEVEL_LABELS.AzhariSecondary2 },
      { value: 'AzhariSecondary3', label: GRADE_LEVEL_LABELS.AzhariSecondary3 },
    ],
  },
  {
    label: 'التعليم الأمريكي',
    grades: [
      { value: 'AmericanGrade9', label: GRADE_LEVEL_LABELS.AmericanGrade9 },
      { value: 'AmericanGrade10', label: GRADE_LEVEL_LABELS.AmericanGrade10 },
      { value: 'AmericanGrade11', label: GRADE_LEVEL_LABELS.AmericanGrade11 },
      { value: 'AmericanGrade12', label: GRADE_LEVEL_LABELS.AmericanGrade12 },
    ],
  },
] as const;

export function requiresTrack(grade: string): boolean {
  return grade === 'SecondSecondary' || grade === 'SecondBaccalaureate';
}

export function getEducationStageLabel(value?: string | null): string {
  if (!value || value === 'N/A') return 'غير محددة';
  return EDUCATION_STAGE_LABELS[value] ?? value;
}

export function getGradeLevelLabel(value?: string | null): string {
  if (!value || value === 'N/A') return 'غير محدد';
  return GRADE_LEVEL_LABELS[value] ?? value.replace(/([a-z])([A-Z])/g, '$1 $2');
}

export function getStudyTrackLabel(value?: string | null): string {
  if (!value || value === 'N/A') return 'غير محددة';
  return STUDY_TRACK_LABELS[value] ?? value.replace(/([a-z])([A-Z])/g, '$1 $2');
}
