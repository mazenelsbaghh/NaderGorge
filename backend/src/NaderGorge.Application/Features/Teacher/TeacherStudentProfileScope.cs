using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Teacher;

public static class TeacherStudentProfileScope
{
    public static IQueryable<StudentAccessGrant> Grants(IAppDbContext db, Guid teacherId) =>
        db.StudentAccessGrants.AsNoTracking().Where(grant =>
            (grant.GrantType == CodeType.Package && db.Packages.Any(package => package.Id == grant.PackageId && package.TeacherId == teacherId)) ||
            (grant.GrantType == CodeType.Term && db.Terms.Any(term => term.Id == grant.TermId && term.Package.TeacherId == teacherId)) ||
            (grant.GrantType == CodeType.Month && db.ContentSections.Any(section => section.Id == grant.ContentSectionId && section.Term.Package.TeacherId == teacherId)) ||
            (grant.GrantType == CodeType.Lesson && db.Lessons.Any(lesson => lesson.Id == grant.LessonId && lesson.ContentSection.Term.Package.TeacherId == teacherId)) ||
            (grant.GrantType == CodeType.Video && db.LessonVideos.Any(video => video.Id == grant.LessonVideoId && video.Lesson.ContentSection.Term.Package.TeacherId == teacherId)) ||
            (grant.GrantType == CodeType.Video && grant.VideoTypeId.HasValue && grant.AccessCode != null && grant.AccessCode.CodeGroup.TeacherId == teacherId) ||
            (grant.GrantType == CodeType.Exam && db.PublicExamProducts.Any(product => product.TeacherId == teacherId &&
                (product.Id == grant.PublicExamProductId || product.ExamId == grant.ExamId))));
}
