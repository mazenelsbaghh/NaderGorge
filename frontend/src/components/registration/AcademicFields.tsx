'use client';

/**
 * AcademicFields — Conditional academic data inputs
 *
 * Stage  → filters available Grades
 * Grade  → conditionally shows/hides Track (study branch)
 *
 * Validation matrix (data-model.md):
 *  - Secondary     → FirstSecondary | SecondSecondary | SecondaryGrade3
 *  - Baccalaureate → FirstBaccalaureate | SecondBaccalaureate
 *  - Primary       → PrimaryGrade1-6                   (no track)
 *  - Preparatory   → PrepGrade1-3                      (no track)
 *  - Azhari        → AzhariPrimary1-6, Prep1-3, Sec1-3 (no track)
 *  - American      → AmericanGrade1-12                 (no track)
 *  - Track required only for SecondSecondary (Arts/Science) and SecondBaccalaureate
 */

import { motion, AnimatePresence } from 'framer-motion';

// ── Types ────────────────────────────────────────────────────────────────────
export type EducationStage =
  | 'Secondary'
  | 'Baccalaureate'
  | 'Primary'
  | 'Preparatory'
  | 'Azhari'
  | 'American';

export type GradeLevel =
  // Existing
  | 'FirstSecondary'
  | 'SecondSecondary'
  | 'FirstBaccalaureate'
  | 'SecondBaccalaureate'
  // Secondary 3rd year
  | 'SecondaryGrade3'
  // Primary
  | 'PrimaryGrade1' | 'PrimaryGrade2' | 'PrimaryGrade3'
  | 'PrimaryGrade4' | 'PrimaryGrade5' | 'PrimaryGrade6'
  // Preparatory
  | 'PrepGrade1' | 'PrepGrade2' | 'PrepGrade3'
  // Azhari
  | 'AzhariPrimary1' | 'AzhariPrimary2' | 'AzhariPrimary3'
  | 'AzhariPrimary4' | 'AzhariPrimary5' | 'AzhariPrimary6'
  | 'AzhariPrep1'    | 'AzhariPrep2'    | 'AzhariPrep3'
  | 'AzhariSecondary1' | 'AzhariSecondary2' | 'AzhariSecondary3'
  // American
  | 'AmericanGrade1'  | 'AmericanGrade2'  | 'AmericanGrade3'
  | 'AmericanGrade4'  | 'AmericanGrade5'  | 'AmericanGrade6'
  | 'AmericanGrade7'  | 'AmericanGrade8'  | 'AmericanGrade9'
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

interface AcademicFieldsProps {
  data: AcademicData;
  onChange: (data: AcademicData) => void;
  errors: Record<string, string | undefined>;
  inputCls: (name: string) => string;
}

// ── Grade groups (for optgroup rendering in Azhari) ──────────────────────────
interface GradeGroup {
  groupLabel?: string;   // if set, wraps in <optgroup>
  grades: { value: GradeLevel; label: string }[];
}

const GRADES_BY_STAGE: Record<EducationStage, GradeGroup[]> = {
  Secondary: [
    {
      grades: [
        { value: 'FirstSecondary',  label: 'الأول الثانوي' },
        { value: 'SecondSecondary', label: 'الثاني الثانوي' },
        { value: 'SecondaryGrade3', label: 'الثالث الثانوي' },
      ],
    },
  ],
  Baccalaureate: [
    {
      grades: [
        { value: 'FirstBaccalaureate',  label: 'الأول بكالوريا' },
        { value: 'SecondBaccalaureate', label: 'الثاني بكالوريا' },
      ],
    },
  ],
  Primary: [
    {
      grades: [
        { value: 'PrimaryGrade1', label: 'الأول الابتدائي' },
        { value: 'PrimaryGrade2', label: 'الثاني الابتدائي' },
        { value: 'PrimaryGrade3', label: 'الثالث الابتدائي' },
        { value: 'PrimaryGrade4', label: 'الرابع الابتدائي' },
        { value: 'PrimaryGrade5', label: 'الخامس الابتدائي' },
        { value: 'PrimaryGrade6', label: 'السادس الابتدائي' },
      ],
    },
  ],
  Preparatory: [
    {
      grades: [
        { value: 'PrepGrade1', label: 'الأول الإعدادي' },
        { value: 'PrepGrade2', label: 'الثاني الإعدادي' },
        { value: 'PrepGrade3', label: 'الثالث الإعدادي' },
      ],
    },
  ],
  Azhari: [
    {
      groupLabel: 'ابتدائي أزهري',
      grades: [
        { value: 'AzhariPrimary1', label: 'الأول الابتدائي الأزهري' },
        { value: 'AzhariPrimary2', label: 'الثاني الابتدائي الأزهري' },
        { value: 'AzhariPrimary3', label: 'الثالث الابتدائي الأزهري' },
        { value: 'AzhariPrimary4', label: 'الرابع الابتدائي الأزهري' },
        { value: 'AzhariPrimary5', label: 'الخامس الابتدائي الأزهري' },
        { value: 'AzhariPrimary6', label: 'السادس الابتدائي الأزهري' },
      ],
    },
    {
      groupLabel: 'إعدادي أزهري',
      grades: [
        { value: 'AzhariPrep1', label: 'الأول الإعدادي الأزهري' },
        { value: 'AzhariPrep2', label: 'الثاني الإعدادي الأزهري' },
        { value: 'AzhariPrep3', label: 'الثالث الإعدادي الأزهري' },
      ],
    },
    {
      groupLabel: 'ثانوي أزهري',
      grades: [
        { value: 'AzhariSecondary1', label: 'الأول الثانوي الأزهري' },
        { value: 'AzhariSecondary2', label: 'الثاني الثانوي الأزهري' },
        { value: 'AzhariSecondary3', label: 'الثالث الثانوي الأزهري' },
      ],
    },
  ],
  American: [
    {
      grades: [
        { value: 'AmericanGrade1',  label: 'Grade 1' },
        { value: 'AmericanGrade2',  label: 'Grade 2' },
        { value: 'AmericanGrade3',  label: 'Grade 3' },
        { value: 'AmericanGrade4',  label: 'Grade 4' },
        { value: 'AmericanGrade5',  label: 'Grade 5' },
        { value: 'AmericanGrade6',  label: 'Grade 6' },
        { value: 'AmericanGrade7',  label: 'Grade 7' },
        { value: 'AmericanGrade8',  label: 'Grade 8' },
        { value: 'AmericanGrade9',  label: 'Grade 9' },
        { value: 'AmericanGrade10', label: 'Grade 10' },
        { value: 'AmericanGrade11', label: 'Grade 11' },
        { value: 'AmericanGrade12', label: 'Grade 12' },
      ],
    },
  ],
};

// ── Track options per grade ──────────────────────────────────────────────────
const TRACKS_BY_GRADE: Record<string, { value: StudyTrack; label: string }[]> = {
  SecondSecondary: [
    { value: 'Arts',    label: 'أدبي' },
    { value: 'Science', label: 'علمي' },
  ],
  SecondBaccalaureate: [
    { value: 'MedicineAndLifeSciences',       label: 'الطب وعلوم الحياة' },
    { value: 'EngineeringAndComputerScience', label: 'الهندسة وعلوم الحاسب' },
    { value: 'Business',                      label: 'قطاع الأعمال' },
    { value: 'ArtsAndHumanities',             label: 'الآداب والفنون' },
  ],
};

// ── Stage display labels ─────────────────────────────────────────────────────
const STAGE_OPTIONS: { value: EducationStage; label: string }[] = [
  { value: 'Secondary',     label: 'ثانوية' },
  { value: 'Baccalaureate', label: 'بكالوريا' },
  { value: 'Primary',       label: 'ابتدائي' },
  { value: 'Preparatory',   label: 'إعدادي' },
  { value: 'Azhari',        label: 'أزهري' },
  { value: 'American',      label: 'أمريكي' },
];

// ── Helpers ───────────────────────────────────────────────────────────────────
export function requiresTrack(grade: string): boolean {
  return grade === 'SecondSecondary' || grade === 'SecondBaccalaureate';
}

const selectStyle = {
  backgroundColor: 'var(--admin-card-soft)',
  color: 'var(--admin-text)',
};

const optionStyle = {
  background: 'var(--admin-bg)',
  color: 'var(--admin-text)',
};

const revealAnim = {
  initial: { opacity: 0, height: 0, marginTop: 0 },
  animate: { opacity: 1, height: 'auto' as const, marginTop: 8 },
  exit: { opacity: 0, height: 0, marginTop: 0 },
  transition: { duration: 0.3, ease: [0.4, 0, 0.2, 1] as const },
};

// ── Component ────────────────────────────────────────────────────────────────
export function AcademicFields({ data, onChange, errors, inputCls }: AcademicFieldsProps) {
  const gradeGroups = data.educationStage ? GRADES_BY_STAGE[data.educationStage] : [];
  const trackOptions = data.gradeLevel ? TRACKS_BY_GRADE[data.gradeLevel] || [] : [];
  const showTrack = data.gradeLevel && requiresTrack(data.gradeLevel);

  const handleStageChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const stage = e.target.value as EducationStage;
    // Reset grade and track when stage changes
    onChange({ educationStage: stage, gradeLevel: '', studyTrack: '' });
  };

  const handleGradeChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const grade = e.target.value as GradeLevel;
    // Reset track when grade changes
    onChange({ ...data, gradeLevel: grade, studyTrack: '' });
  };

  const handleTrackChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    onChange({ ...data, studyTrack: e.target.value as StudyTrack });
  };

  return (
    <div className="space-y-4">
      {/* ── Stage ── */}
      <div key="stage-section">
        <label className="auth-label" htmlFor="reg-stage">
          المرحلة الدراسية
        </label>
        <select
          id="reg-stage"
          name="educationStage"
          data-select
          className={inputCls('educationStage')}
          value={data.educationStage}
          onChange={handleStageChange}
          style={selectStyle}
        >
          <option key="stage-placeholder" value="" disabled style={optionStyle}>
            اختر المرحلة الدراسية...
          </option>
          {STAGE_OPTIONS.map((s) => (
            <option key={s.value} value={s.value} style={optionStyle}>
              {s.label}
            </option>
          ))}
        </select>
        {errors.educationStage && (
          <p className="auth-field-error">{errors.educationStage}</p>
        )}
      </div>

      {/* ── Grade (appears after stage selection) ── */}
      <AnimatePresence key="grade-presence">
        {data.educationStage && (
          <motion.div {...revealAnim} key="grade-field">
            <div className="space-y-2">
              <label className="auth-label" htmlFor="reg-grade">
                الصف الدراسي
              </label>
              <select
                id="reg-grade"
                name="gradeLevel"
                data-select
                className={inputCls('gradeLevel')}
                value={data.gradeLevel}
                onChange={handleGradeChange}
                style={selectStyle}
              >
                <option key="grade-placeholder" value="" disabled style={optionStyle}>
                  اختر الصف الدراسي...
                </option>
                {gradeGroups.map((group, gi) =>
                  group.groupLabel ? (
                    <optgroup key={`group-${gi}`} label={group.groupLabel}>
                      {group.grades.map((g) => (
                        <option key={g.value} value={g.value} style={optionStyle}>
                          {g.label}
                        </option>
                      ))}
                    </optgroup>
                  ) : (
                    group.grades.map((g) => (
                      <option key={g.value} value={g.value} style={optionStyle}>
                        {g.label}
                      </option>
                    ))
                  )
                )}
              </select>
              {errors.gradeLevel ? (
                <p className="auth-field-error">{errors.gradeLevel}</p>
              ) : null}
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* ── Track / Branch (appears only for 2nd-level grades) ── */}
      <AnimatePresence key="track-presence">
        {showTrack && (
          <motion.div {...revealAnim} key="track-field">
            <div className="space-y-2">
              <label className="auth-label" htmlFor="reg-track">
                الشعبة / التخصص
              </label>
              <select
                id="reg-track"
                name="studyTrack"
                data-select
                className={inputCls('studyTrack')}
                value={data.studyTrack}
                onChange={handleTrackChange}
                style={selectStyle}
              >
                <option key="track-placeholder" value="" disabled style={optionStyle}>
                  اختر الشعبة أو التخصص...
                </option>
                {trackOptions.map((t) => (
                  <option key={t.value} value={t.value} style={optionStyle}>
                    {t.label}
                  </option>
                ))}
              </select>
              {errors.studyTrack ? (
                <p className="auth-field-error">{errors.studyTrack}</p>
              ) : null}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
