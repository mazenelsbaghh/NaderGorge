using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Infrastructure.Data;

/// <summary>
/// Creates a small, realistic catalog for staging/demo environments. It is opt-in and idempotent.
/// The people are fictional; the lesson videos point to public YouTube videos.
/// </summary>
public static class DemoCatalogSeeder
{
    private const string Marker = "demo-catalog-v1";

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.PlatformSettings.AnyAsync(x => x.Key == Marker, cancellationToken))
            return;

        var teacherRole = await db.Roles.SingleAsync(x => x.Name == "Teacher", cancellationToken);
        var videoType = await db.VideoTypes.FirstOrDefaultAsync(cancellationToken);
        if (videoType is null)
        {
            videoType = new VideoType { Name = "فيديو تعليمي", NormalizedName = "EDUCATIONAL", SortOrder = 1 };
            db.VideoTypes.Add(videoType);
        }

        var catalog = new[]
        {
            new DemoCourse(
                "أ/ أحمد المصري", "01090000001", "الرياضيات", "أحمد-المصري",
                "شرح مبسط للرياضيات مع تدريب مستمر على أسئلة الثانوية العامة.",
                "/uploads/demo/teacher-math.png", "/uploads/demo/course-math.png", "MATH_DEMO",
                "رياضيات الثانوية العامة", "منهج متكامل في الجبر والهندسة والتفاضل.", "ثالثة ثانوي", 249m,
                new[] { ("مقدمة في التفاضل", "فهم النهايات والمشتقات خطوة بخطوة", "aircAruvnKk"), ("الهندسة التحليلية", "المستقيمات والدوائر وتطبيقات الامتحان", "WUvTyaaNkzM") }),
            new DemoCourse(
                "أ/ مريم فوزي", "01090000002", "الفيزياء", "مريم-فوزي",
                "مدرسة فيزياء تهتم بالفهم العملي وحل المسائل بطريقة منظمة.",
                "/uploads/demo/teacher-physics.png", "/uploads/demo/course-physics.png", "PHYSICS_DEMO",
                "فيزياء الثانوية العامة", "شرح المنهج مع تجارب ذهنية ومسائل متدرجة الصعوبة.", "ثالثة ثانوي", 299m,
                new[] { ("الحركة في خط مستقيم", "الإزاحة والسرعة والعجلة مع أمثلة محلولة", "ZM8ECpBuQYE"), ("الكهرباء الكهربية", "قوانين الدوائر الكهربية وتطبيقاتها", "bHIhgxav9LY") }),
            new DemoCourse(
                "أ/ كريم عبدالسلام", "01090000003", "اللغة العربية", "كريم-عبدالسلام",
                "شرح الأدب والنحو والبلاغة بأسلوب عملي يركز على إجابة الامتحان.",
                "/uploads/demo/teacher-arabic.png", "/uploads/demo/course-arabic.png", "ARABIC_DEMO",
                "اللغة العربية للثانوية العامة", "النحو والبلاغة والأدب والتعبير في مسار واحد واضح.", "ثالثة ثانوي", 229m,
                new[] { ("النحو من البداية", "مراجعة الجملة الاسمية والفعلية والإعراب", "j9WZyLZCBzs"), ("مدخل إلى البلاغة", "التشبيه والاستعارة والكناية مع تدريبات", "8kK0YJ2zQ9U") })
        };

        foreach (var item in catalog)
        {
            var subject = new Subject
            {
                Name = item.Subject,
                NormalizedName = item.SubjectCode,
                Description = $"مادة {item.Subject} للمرحلة الثانوية"
            };
            db.Subjects.Add(subject);

            var user = new User
            {
                FullName = item.TeacherName,
                PhoneNumber = item.Phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo@12345"),
                IsActive = true,
                IsProfileComplete = true
            };
            db.Users.Add(user);
            db.UserRoles.Add(new UserRole { User = user, Role = teacherRole });

            var teacher = new TeacherProfile
            {
                User = user,
                Bio = item.Bio,
                PublicBio = item.Bio,
                Specialization = item.Subject,
                ContactInfo = "التواصل من خلال دعم مسار",
                ProfileImageUrl = item.TeacherImage,
                PublicSlug = item.Slug,
                IsPublicProfileEnabled = true,
                IsVisibleToStudents = true,
                IsContentVisibleToStudents = true,
                ShowOnLanding = true,
                RatingAverage = 4.8m,
                RatingCount = 124
            };
            db.TeacherProfiles.Add(teacher);
            db.TeacherSubjects.Add(new TeacherSubject { Teacher = teacher, Subject = subject });

            var package = new Package
            {
                Name = item.CourseName,
                Description = item.CourseDescription,
                ImageUrl = item.CourseImage,
                Price = item.Price,
                IsActive = true,
                Subject = subject,
                Teacher = teacher,
                TargetGrade = item.TargetGrade
            };
            db.Packages.Add(package);

            var term = new Term { Title = "الترم الأول", Order = 1, Price = item.Price, Package = package, ImageUrl = item.CourseImage };
            var section = new ContentSection { Title = "الوحدة الأولى: الأساسيات", Order = 1, Price = 0, Term = term, ImageUrl = item.CourseImage };
            db.Terms.Add(term);
            db.ContentSections.Add(section);

            for (var index = 0; index < item.Lessons.Length; index++)
            {
                var lessonData = item.Lessons[index];
                var lesson = new Lesson
                {
                    Title = $"الدرس {index + 1}: {lessonData.Title}",
                    Summary = lessonData.Summary,
                    Order = index + 1,
                    Price = 0,
                    ContentSection = section
                };
                db.Lessons.Add(lesson);
                db.LessonVideos.Add(new LessonVideo
                {
                    Title = $"شرح {lessonData.Title}",
                    Provider = "youtube",
                    ProviderVideoId = lessonData.VideoId,
                    Order = 1,
                    MaxWatchCount = 5,
                    VideoType = videoType,
                    Lesson = lesson,
                    IsActive = true
                });

                var exam = CreateExam(item, subject, teacher, lessonData.Title, lesson, index);
                db.Exams.Add(exam);
                lesson.ExamId = exam.Id;
            }
        }

        db.PlatformSettings.Add(new PlatformSetting { Key = Marker, Value = DateTime.UtcNow.ToString("O") });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static Exam CreateExam(DemoCourse item, Subject subject, TeacherProfile teacher, string lessonTitle, Lesson lesson, int index)
    {
        var exam = new Exam
        {
            Title = $"اختبار {lessonTitle}",
            Description = "اختبار قصير بعد الدرس لقياس الفهم والتطبيق.",
            DurationMinutes = 15,
            TotalScore = 10,
            PassingScore = 6,
            IsMandatory = false,
            CreatedByTeacher = teacher
        };
        var first = new QuestionBankItem
        {
            Text = $"ما الفكرة الأساسية التي يشرحها درس {lessonTitle}؟",
            DefaultPoints = 5,
            Subject = subject,
            CreatedByTeacher = teacher,
            Tags = item.Subject,
            HintText = "راجع أمثلة الدرس قبل اختيار الإجابة.",
            Options =
            {
                new QuestionOption { Text = "المفهوم الأساسي والتطبيق عليه", IsCorrect = true },
                new QuestionOption { Text = "موضوع مختلف تمامًا", IsCorrect = false },
                new QuestionOption { Text = "لا توجد فكرة محددة", IsCorrect = false }
            }
        };
        var second = new QuestionBankItem
        {
            Text = "هل يساعد حل التدريبات بعد مشاهدة الفيديو على تثبيت الفهم؟",
            DefaultPoints = 5,
            Subject = subject,
            CreatedByTeacher = teacher,
            Tags = item.Subject,
            Options =
            {
                new QuestionOption { Text = "نعم", IsCorrect = true },
                new QuestionOption { Text = "لا", IsCorrect = false }
            }
        };
        exam.ExamQuestions.Add(new ExamQuestion { Question = first, Order = 1, Points = 5 });
        exam.ExamQuestions.Add(new ExamQuestion { Question = second, Order = 2, Points = 5 });
        return exam;
    }

    private sealed record DemoCourse(
        string TeacherName, string Phone, string Subject, string Slug, string Bio,
        string TeacherImage, string CourseImage, string SubjectCode, string CourseName,
        string CourseDescription, string TargetGrade, decimal Price,
        (string Title, string Summary, string VideoId)[] Lessons);
}
