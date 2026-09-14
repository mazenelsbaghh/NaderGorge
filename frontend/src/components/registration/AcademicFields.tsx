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
import {
  type AcademicData,
  type EducationStage,
  type GradeLevel,
  type StudyTrack,
  GRADES_BY_STAGE,
  STAGE_OPTIONS,
  TRACKS_BY_GRADE,
  requiresTrack,
} from '@/lib/academic-labels';

export type { AcademicData };
export { requiresTrack };

interface AcademicFieldsProps {
  data: AcademicData;
  onChange: (data: AcademicData) => void;
  errors: Record<string, string | undefined>;
  inputCls: (name: string) => string;
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
