using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Services;

/// <summary>
/// Validates the conditional relationships between EducationStage, GradeLevel, and StudyTrack.
/// Single source of truth for academic field validation — used by both registration and admin updates.
/// </summary>
public class AcademicValidationService
{
    /// <summary>
    /// Validates that the grade matches the education stage, and the track (if provided) matches the grade.
    /// Returns a list of validation error messages. Empty list means valid.
    /// </summary>
    public List<string> Validate(EducationStage stage, GradeLevel grade, StudyTrack? track)
    {
        var errors = new List<string>();

        // 1. Validate grade matches stage
        if (!IsGradeValidForStage(stage, grade))
        {
            errors.Add($"Grade '{grade}' is not valid for education stage '{stage}'.");
            return errors; // No point validating track if grade is wrong
        }

        // 2. Validate track requirement
        if (RequiresTrack(grade))
        {
            if (track == null)
            {
                errors.Add($"A study track is required for grade '{grade}'.");
            }
            else if (!IsTrackValidForGrade(grade, track.Value))
            {
                errors.Add($"Track '{track}' is not valid for grade '{grade}'.");
            }
        }
        else
        {
            if (track != null)
            {
                errors.Add($"Track must not be specified for grade '{grade}'.");
            }
        }

        return errors;
    }

    /// <summary>
    /// Determines if a grade is valid for the given education stage.
    /// </summary>
    public static bool IsGradeValidForStage(EducationStage stage, GradeLevel grade)
    {
        return stage switch
        {
            // ── Existing stages ───────────────────────────────────────────────────────
            EducationStage.Secondary =>
                grade is GradeLevel.FirstSecondary
                      or GradeLevel.SecondSecondary
                      or GradeLevel.SecondaryGrade3,

            EducationStage.Baccalaureate =>
                grade is GradeLevel.FirstBaccalaureate
                      or GradeLevel.SecondBaccalaureate,

            // ── New stages ────────────────────────────────────────────────────────────
            EducationStage.Primary =>
                grade is GradeLevel.PrimaryGrade1
                      or GradeLevel.PrimaryGrade2
                      or GradeLevel.PrimaryGrade3
                      or GradeLevel.PrimaryGrade4
                      or GradeLevel.PrimaryGrade5
                      or GradeLevel.PrimaryGrade6,

            EducationStage.Preparatory =>
                grade is GradeLevel.PrepGrade1
                      or GradeLevel.PrepGrade2
                      or GradeLevel.PrepGrade3,

            EducationStage.Azhari =>
                grade is GradeLevel.AzhariPrimary1
                      or GradeLevel.AzhariPrimary2
                      or GradeLevel.AzhariPrimary3
                      or GradeLevel.AzhariPrimary4
                      or GradeLevel.AzhariPrimary5
                      or GradeLevel.AzhariPrimary6
                      or GradeLevel.AzhariPrep1
                      or GradeLevel.AzhariPrep2
                      or GradeLevel.AzhariPrep3
                      or GradeLevel.AzhariSecondary1
                      or GradeLevel.AzhariSecondary2
                      or GradeLevel.AzhariSecondary3,

            EducationStage.American =>
                grade is GradeLevel.AmericanGrade1
                      or GradeLevel.AmericanGrade2
                      or GradeLevel.AmericanGrade3
                      or GradeLevel.AmericanGrade4
                      or GradeLevel.AmericanGrade5
                      or GradeLevel.AmericanGrade6
                      or GradeLevel.AmericanGrade7
                      or GradeLevel.AmericanGrade8
                      or GradeLevel.AmericanGrade9
                      or GradeLevel.AmericanGrade10
                      or GradeLevel.AmericanGrade11
                      or GradeLevel.AmericanGrade12,

            _ => false
        };
    }

    /// <summary>
    /// Determines if a grade requires a study track selection.
    /// Only SecondSecondary and SecondBaccalaureate require a track.
    /// All new stages (Primary, Preparatory, Azhari, American) do NOT require a track.
    /// </summary>
    public static bool RequiresTrack(GradeLevel grade)
    {
        return grade is GradeLevel.SecondSecondary or GradeLevel.SecondBaccalaureate;
    }

    /// <summary>
    /// Determines if a track is valid for the given grade.
    /// </summary>
    public static bool IsTrackValidForGrade(GradeLevel grade, StudyTrack track)
    {
        return grade switch
        {
            GradeLevel.SecondSecondary => track is StudyTrack.Arts or StudyTrack.Science,
            GradeLevel.SecondBaccalaureate => track is StudyTrack.MedicineAndLifeSciences
                or StudyTrack.EngineeringAndComputerScience
                or StudyTrack.Business
                or StudyTrack.ArtsAndHumanities,
            _ => false
        };
    }
}
