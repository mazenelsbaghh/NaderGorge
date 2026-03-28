'use client';

/**
 * AcademicFields — Conditional academic data inputs
 *
 * Stage  → filters available Grades
 * Grade  → conditionally shows/hides Track (study branch)
 *
 * Follows the validation matrix from data-model.md:
 *  - Secondary → FirstSecondary | SecondSecondary
 *  - Baccalaureate → FirstBaccalaureate | SecondBaccalaureate
 *  - Track required only for SecondSecondary and SecondBaccalaureate
 */

import { motion, AnimatePresence } from 'framer-motion';

// ── Types ────────────────────────────────────────────────────────────────────
export type EducationStage = 'Secondary' | 'Baccalaureate';
export type GradeLevel = 'FirstSecondary' | 'SecondSecondary' | 'FirstBaccalaureate' | 'SecondBaccalaureate';
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

// ── Grade options per stage ──────────────────────────────────────────────────
const GRADES_BY_STAGE: Record<EducationStage, { value: GradeLevel; label: string }[]> = {
  Secondary: [
    { value: 'FirstSecondary', label: 'الأول الثانوي' },
    { value: 'SecondSecondary', label: 'الثاني الثانوي' },
  ],
  Baccalaureate: [
    { value: 'FirstBaccalaureate', label: 'الأول بكالوريا' },
    { value: 'SecondBaccalaureate', label: 'الثاني بكالوريا' },
  ],
};

// ── Track options per grade ──────────────────────────────────────────────────
const TRACKS_BY_GRADE: Record<string, { value: StudyTrack; label: string }[]> = {
  SecondSecondary: [
    { value: 'Arts', label: 'أدبي' },
    { value: 'Science', label: 'علمي' },
  ],
  SecondBaccalaureate: [
    { value: 'MedicineAndLifeSciences', label: 'الطب وعلوم الحياة' },
    { value: 'EngineeringAndComputerScience', label: 'الهندسة وعلوم الحاسب' },
    { value: 'Business', label: 'قطاع الأعمال' },
    { value: 'ArtsAndHumanities', label: 'الآداب والفنون' },
  ],
};

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
  const gradeOptions = data.educationStage ? GRADES_BY_STAGE[data.educationStage] : [];
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
          <option key="stage-secondary" value="Secondary" style={optionStyle}>ثانوية</option>
          <option key="stage-baccalaureate" value="Baccalaureate" style={optionStyle}>بكالوريا</option>
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
                {gradeOptions.map((g) => (
                  <option key={g.value} value={g.value} style={optionStyle}>
                    {g.label}
                  </option>
                ))}
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
