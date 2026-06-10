using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record SeedTestCourseCommand : IRequest<ApiResponse<Guid>>;

public class SeedTestCourseCommandHandler : IRequestHandler<SeedTestCourseCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public SeedTestCourseCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<Guid>> Handle(SeedTestCourseCommand request, CancellationToken ct)
    {
        // Find existing Package
        var package = await _db.Packages.FirstOrDefaultAsync(ct);
        if (package == null)
        {
            var subject = await _db.Subjects.FirstOrDefaultAsync(ct);
            if (subject == null)
            {
                subject = new Subject { Id = Guid.NewGuid(), Name = "مادة اختبار النظام", NormalizedName = "TEST_SUBJECT", Description = "Test Subject", CreatedAt = DateTime.UtcNow };
                _db.Subjects.Add(subject);
            }
            var teacher = await _db.TeacherProfiles.FirstOrDefaultAsync(ct);
            package = new Package { Id = Guid.NewGuid(), Name = "باقة اختبار النظام", Description = "Test", Price = 0, IsActive = true, SubjectId = subject.Id, TargetGrade = "All", TeacherId = teacher?.Id ?? Guid.Empty, CreatedAt = DateTime.UtcNow };
            _db.Packages.Add(package);
            await _db.SaveChangesAsync(ct);
        }

        var term = new Term
        {
            Id = Guid.NewGuid(),
            Title = "التيرم الشامل المجاني (اختبار النظام)",
            Order = 1,
            Price = 0,
            PackageId = package.Id,
            CreatedAt = DateTime.UtcNow
        };
        _db.Terms.Add(term);

        var section = new ContentSection
        {
            Id = Guid.NewGuid(),
            Title = "الوحدة الأولى: اختبار النظام",
            Order = 1,
            TermId = term.Id,
            Price = 0,
            CreatedAt = DateTime.UtcNow
        };
        _db.ContentSections.Add(section);

        var lesson1 = new Lesson
        {
            Id = Guid.NewGuid(),
            Title = "الدرس الأول: مقدمة",
            Summary = "هذا الدرس يحتوي على واجب يجب إتمامه.",
            Order = 1,
            Price = 0,
            ContentSectionId = section.Id,
            CreatedAt = DateTime.UtcNow
        };
        _db.Lessons.Add(lesson1);

        var hw1 = new NaderGorge.Domain.Entities.Homework.Homework
        {
            Id = Guid.NewGuid(),
            Title = "واجب الدرس الأول",
            Description = "قم باجتياز هذا الواجب لتفتح الدرس الثاني",
            IsMandatory = true,
            PassingScoreThreshold = 5,
            LessonId = lesson1.Id,
            CreatedAt = DateTime.UtcNow
        };
        _db.Homeworks.Add(hw1);

        _db.HomeworkQuestions.Add(new HomeworkQuestion
        {
            Id = Guid.NewGuid(),
            BodyText = "ما هو ناتج 1+1؟",
            Order = 1,
            PointsActive = 5,
            HomeworkId = hw1.Id,
            PossibleAnswers = new[] { "1", "2", "3", "4" },
            CorrectAnswerKey = "2",
            QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.MCQ
        });

        var exam = new Exam
        {
            Id = Guid.NewGuid(),
            Title = "امتحان الدرس الثاني المتقدم",
            Description = "امتحان بوقت محدد لتجربة العداد والتنقل.",
            DurationMinutes = 10,
            TotalScore = 15,
            PassingScore = 10,
            CreatedAt = DateTime.UtcNow
        };
        _db.Exams.Add(exam);

        var bankItem1 = new QuestionBankItem
        {
            Id = Guid.NewGuid(),
            Text = "ما هو لون السماء؟",
            DefaultPoints = 5,
            CreatedAt = DateTime.UtcNow
        };
        _db.QuestionBankItems.Add(bankItem1);
        _db.QuestionOptions.Add(new QuestionOption { Id = Guid.NewGuid(), QuestionBankItemId = bankItem1.Id, Text = "أزرق", IsCorrect = true });
        _db.QuestionOptions.Add(new QuestionOption { Id = Guid.NewGuid(), QuestionBankItemId = bankItem1.Id, Text = "أحمر", IsCorrect = false });

        var bankItem2 = new QuestionBankItem
        {
            Id = Guid.NewGuid(),
            Text = "هل يمكن تخطي هذا السؤال؟",
            DefaultPoints = 5,
            CreatedAt = DateTime.UtcNow
        };
        _db.QuestionBankItems.Add(bankItem2);
        _db.QuestionOptions.Add(new QuestionOption { Id = Guid.NewGuid(), QuestionBankItemId = bankItem2.Id, Text = "نعم", IsCorrect = true });
        _db.QuestionOptions.Add(new QuestionOption { Id = Guid.NewGuid(), QuestionBankItemId = bankItem2.Id, Text = "لا", IsCorrect = false });

        _db.ExamQuestions.Add(new ExamQuestion
        {
            Id = Guid.NewGuid(),
            ExamId = exam.Id,
            QuestionBankItemId = bankItem1.Id,
            Order = 1,
            Points = 5,
            CreatedAt = DateTime.UtcNow
        });

        _db.ExamQuestions.Add(new ExamQuestion
        {
            Id = Guid.NewGuid(),
            ExamId = exam.Id,
            QuestionBankItemId = bankItem2.Id,
            Order = 2,
            Points = 5,
            CreatedAt = DateTime.UtcNow
        });

        var lesson2 = new Lesson
        {
            Id = Guid.NewGuid(),
            Title = "الدرس الثاني: التقدم",
            Summary = "تظهر هذه الحصة مقفلة حتى تنهي واجب الدرس الأول.",
            Order = 2,
            Price = 0,
            ContentSectionId = section.Id,
            ExamId = exam.Id,
            CreatedAt = DateTime.UtcNow
        };
        _db.Lessons.Add(lesson2);

        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(term.Id, "Free test term created successfully.");
    }
}
