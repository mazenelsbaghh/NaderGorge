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
            EducationStage.Secondary => grade is GradeLevel.FirstSecondary or GradeLevel.SecondSecondary,
            EducationStage.Baccalaureate => grade is GradeLevel.FirstBaccalaureate or GradeLevel.SecondBaccalaureate,
            _ => false
        };
    }

    /// <summary>
    /// Determines if a grade requires a study track selection.
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
