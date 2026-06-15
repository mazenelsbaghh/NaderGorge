using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.API.Configuration;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/e2e")]
[E2eOnly]
public class E2eTestingController : ControllerBase
{
    private static readonly System.Threading.SemaphoreSlim _semaphore = new(1, 1);
    private readonly DbContext _dbContext;

    public E2eTestingController(IAppDbContext dbContext)
    {
        _dbContext = (DbContext)dbContext;
    }

    [HttpPost("seed")]
    public async Task<IActionResult> SeedDatabase([FromBody] SeedRequest request)
    {
        await _semaphore.WaitAsync();
        try
        {
        if (request.ClearDatabase)
        {
            if (!UsesE2eDatabase())
                return BadRequest("E2E destructive reset requires an E2E/test database connection string.");

            await _dbContext.Database.EnsureDeletedAsync();
            Npgsql.NpgsqlConnection.ClearAllPools();
            await _dbContext.Database.EnsureCreatedAsync();
        }

        if (request.SeedAdmin || request.SeedStudents || request.SeedTeacher || request.SeedAssistant)
        {
            var adminRole = await _dbContext.Set<Role>().FirstOrDefaultAsync(r => r.Name == "Admin");
            if (adminRole == null)
            {
                adminRole = new Role { Id = Guid.NewGuid(), Name = "Admin", Type = RoleType.Admin };
                _dbContext.Set<Role>().Add(adminRole);
            }
            var studentRole = await _dbContext.Set<Role>().FirstOrDefaultAsync(r => r.Name == "Student");
            if (studentRole == null)
            {
                studentRole = new Role { Id = Guid.NewGuid(), Name = "Student", Type = RoleType.Student };
                _dbContext.Set<Role>().Add(studentRole);
            }
            var assistantRole = await _dbContext.Set<Role>().FirstOrDefaultAsync(r => r.Name == "Assistant");
            if (assistantRole == null)
            {
                assistantRole = new Role { Id = Guid.NewGuid(), Name = "Assistant", Type = RoleType.Assistant };
                _dbContext.Set<Role>().Add(assistantRole);
            }
            var teacherRole = await _dbContext.Set<Role>().FirstOrDefaultAsync(r => r.Name == "Teacher");
            if (teacherRole == null)
            {
                teacherRole = new Role
                {
                    Id = Guid.NewGuid(),
                    Name = "Teacher",
                    Type = RoleType.Teacher,
                    PermissionsJson = "[\"content.manage\",\"exams.manage\",\"comments.manage\"]"
                };
                _dbContext.Set<Role>().Add(teacherRole);
            }
            else
            {
                teacherRole.PermissionsJson = "[\"content.manage\",\"exams.manage\",\"comments.manage\"]";
            }
            await _dbContext.SaveChangesAsync();

            if (request.SeedAdmin)
            {
                var existingAdmin = await _dbContext.Set<User>().FirstOrDefaultAsync(u => u.PhoneNumber == "20000000000");
                if (existingAdmin == null)
                {
                    var adminUser = new User
                    {
                        Id = Guid.NewGuid(),
                        FullName = "E2E Admin",
                        PhoneNumber = "20000000000",
                        PasswordHash = HashPassword("password"),
                        IsActive = true,
                        IsProfileComplete = true
                    };
                    _dbContext.Set<User>().Add(adminUser);
                    _dbContext.Set<UserRole>().Add(new UserRole { UserId = adminUser.Id, RoleId = adminRole.Id });
                }
                else
                {
                    existingAdmin.PasswordHash = HashPassword("password");
                    existingAdmin.IsActive = true;
                    existingAdmin.IsProfileComplete = true;
                }
            }

            if (request.SeedAssistant)
            {
                var existingAssistant = await _dbContext.Set<User>().FirstOrDefaultAsync(u => u.PhoneNumber == "20000000003");
                if (existingAssistant == null)
                {
                    var assistantUser = new User
                    {
                        Id = Guid.NewGuid(),
                        FullName = "E2E Assistant",
                        PhoneNumber = "20000000003",
                        PasswordHash = HashPassword("password"),
                        IsActive = true,
                        IsProfileComplete = true
                    };
                    _dbContext.Set<User>().Add(assistantUser);
                    _dbContext.Set<UserRole>().Add(new UserRole { UserId = assistantUser.Id, RoleId = assistantRole.Id });
                }
                else
                {
                    existingAssistant.PasswordHash = HashPassword("password");
                    existingAssistant.IsActive = true;
                    existingAssistant.IsProfileComplete = true;
                }
            }

            if (request.SeedTeacher)
            {
                var existingTeacher = await _dbContext.Set<User>().FirstOrDefaultAsync(u => u.PhoneNumber == "20000000004");
                if (existingTeacher == null)
                {
                    var teacherUser = new User
                    {
                        Id = Guid.NewGuid(),
                        FullName = "E2E Teacher",
                        PhoneNumber = "20000000004",
                        PasswordHash = HashPassword("password"),
                        IsActive = true,
                        IsProfileComplete = true
                    };
                    _dbContext.Set<User>().Add(teacherUser);
                    _dbContext.Set<UserRole>().Add(new UserRole { UserId = teacherUser.Id, RoleId = teacherRole.Id });

                    var teacherProfile = new TeacherProfile
                    {
                        Id = Guid.NewGuid(),
                        UserId = teacherUser.Id,
                        CommissionRate = 0.20m,
                        Bio = "E2E Teacher Bio",
                        Specialization = "FirstSecondary,SecondSecondary,SecondaryGrade3"
                    };
                    _dbContext.Set<TeacherProfile>().Add(teacherProfile);
                }
                else
                {
                    existingTeacher.PasswordHash = HashPassword("password");
                    existingTeacher.IsActive = true;
                    existingTeacher.IsProfileComplete = true;
                }

                var existingTeacherB = await _dbContext.Set<User>().FirstOrDefaultAsync(u => u.PhoneNumber == "20000000005");
                if (existingTeacherB == null)
                {
                    var teacherUserB = new User
                    {
                        Id = Guid.NewGuid(),
                        FullName = "E2E Teacher B",
                        PhoneNumber = "20000000005",
                        PasswordHash = HashPassword("password"),
                        IsActive = true,
                        IsProfileComplete = true
                    };
                    _dbContext.Set<User>().Add(teacherUserB);
                    _dbContext.Set<UserRole>().Add(new UserRole { UserId = teacherUserB.Id, RoleId = teacherRole.Id });

                    var teacherProfileB = new TeacherProfile
                    {
                        Id = Guid.NewGuid(),
                        UserId = teacherUserB.Id,
                        CommissionRate = 0.20m,
                        Bio = "E2E Teacher B Bio",
                        Specialization = "FirstSecondary,SecondSecondary,SecondaryGrade3"
                    };
                    _dbContext.Set<TeacherProfile>().Add(teacherProfileB);
                }
                else
                {
                    existingTeacherB.PasswordHash = HashPassword("password");
                    existingTeacherB.IsActive = true;
                    existingTeacherB.IsProfileComplete = true;
                }
            }

            if (request.SeedStudents)
            {
                var existingStudent1 = await _dbContext.Set<User>().FirstOrDefaultAsync(u => u.PhoneNumber == "20000000001");
                if (existingStudent1 == null)
                {
                    var student1 = new User
                    {
                        Id = Guid.NewGuid(),
                        FullName = "E2E Student 1",
                        PhoneNumber = "20000000001",
                        PasswordHash = HashPassword("password"),
                        IsActive = true,
                        IsProfileComplete = true
                    };
                    _dbContext.Set<User>().Add(student1);
                    _dbContext.Set<UserRole>().Add(new UserRole { UserId = student1.Id, RoleId = studentRole.Id });
                }
                else
                {
                    existingStudent1.PasswordHash = HashPassword("password");
                    existingStudent1.IsActive = true;
                    existingStudent1.IsProfileComplete = true;
                }

                var existingStudent2 = await _dbContext.Set<User>().FirstOrDefaultAsync(u => u.PhoneNumber == "20000000002");
                if (existingStudent2 == null)
                {
                    var student2 = new User
                    {
                        Id = Guid.NewGuid(),
                        FullName = "E2E Student MaxDevices",
                        PhoneNumber = "20000000002",
                        PasswordHash = HashPassword("password"),
                        IsActive = true,
                        IsProfileComplete = true
                    };
                    _dbContext.Set<User>().Add(student2);
                    _dbContext.Set<UserRole>().Add(new UserRole { UserId = student2.Id, RoleId = studentRole.Id });

                    // Pre-register 2 devices for student2 to test device limits
                    _dbContext.Set<Device>().Add(new Device { UserId = student2.Id, DeviceFingerprint = "e2e-dev1", DeviceName = "Dev1", IpAddress = "127.0.0.1", IsActive = true, LastUsedAt = DateTime.UtcNow });
                    _dbContext.Set<Device>().Add(new Device { UserId = student2.Id, DeviceFingerprint = "e2e-dev2", DeviceName = "Dev2", IpAddress = "127.0.0.1", IsActive = true, LastUsedAt = DateTime.UtcNow });
                }
                else
                {
                    existingStudent2.PasswordHash = HashPassword("password");
                    existingStudent2.IsActive = true;
                    existingStudent2.IsProfileComplete = true;
                }
            }

            await _dbContext.SaveChangesAsync();
        }

        var users = await _dbContext.Set<User>()
            .Select(u => new { u.PhoneNumber, u.FullName, Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList() })
            .ToListAsync();

        return Ok(new { message = "E2E Database successfully seeded.", users = users });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    [HttpPost("setup-mock-package")]
    public async Task<IActionResult> SetupMockPackage()
    {
        // Create a Program first (Package requires ProgramId)
        var programId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var lesson2Id = Guid.NewGuid();
        var lesson3Id = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        var examId = Guid.NewGuid();
        var essayExamId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var essayQuestionId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var homeworkId = Guid.NewGuid();
        var hwQuestionId = Guid.NewGuid();
        var hwMcqQuestion1Id = Guid.NewGuid();
        var hwMcqQuestion2Id = Guid.NewGuid();
        var hwFtmQuestionId = Guid.NewGuid();

        var teacher = await _dbContext.Set<TeacherProfile>()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.User.PhoneNumber == "20000000004");
        if (teacher == null)
        {
            teacher = await _dbContext.Set<TeacherProfile>().FirstOrDefaultAsync();
        }
        if (teacher == null)
        {
            var teacherRole = await _dbContext.Set<Role>().FirstOrDefaultAsync(r => r.Name == "Teacher");
            if (teacherRole == null)
            {
                teacherRole = new Role { Id = Guid.NewGuid(), Name = "Teacher", Type = RoleType.Teacher };
                _dbContext.Set<Role>().Add(teacherRole);
            }
            var teacherUser = new User
            {
                Id = Guid.NewGuid(),
                FullName = "Fallback E2E Teacher",
                PhoneNumber = "20000000009",
                PasswordHash = HashPassword("password"),
                IsActive = true,
                IsProfileComplete = true
            };
            _dbContext.Set<User>().Add(teacherUser);
            _dbContext.Set<UserRole>().Add(new UserRole { UserId = teacherUser.Id, RoleId = teacherRole.Id });

            teacher = new TeacherProfile
            {
                Id = Guid.NewGuid(),
                UserId = teacherUser.Id,
                CommissionRate = 0.20m,
                Bio = "Fallback teacher",
                Specialization = "FirstSecondary,SecondSecondary,SecondaryGrade3"
            };
            _dbContext.Set<TeacherProfile>().Add(teacher);
            await _dbContext.SaveChangesAsync();
        }

        var subject = await _dbContext.Set<Subject>().FirstOrDefaultAsync();
        if (subject == null)
        {
            subject = new Subject
            {
                Id = Guid.NewGuid(),
                Name = "E2E Physics",
                NormalizedName = "E2E PHYSICS",
                Description = "E2E Physics Subject"
            };
            _dbContext.Set<Subject>().Add(subject);
            await _dbContext.SaveChangesAsync();
        }

        // Add TeacherSubject connections for all teachers if they don't exist
        var allTeachers = await _dbContext.Set<TeacherProfile>().ToListAsync();
        foreach (var t in allTeachers)
        {
            var exists = await _dbContext.Set<TeacherSubject>()
                .AnyAsync(ts => ts.TeacherId == t.Id && ts.SubjectId == subject.Id);
            if (!exists)
            {
                _dbContext.Set<TeacherSubject>().Add(new TeacherSubject
                {
                    TeacherId = t.Id,
                    SubjectId = subject.Id
                });
            }
        }
        await _dbContext.SaveChangesAsync();

        var termId = Guid.NewGuid();
        var package = new Package { Id = packageId, Name = "E2E Student Package", Description = "Test", Price = 100, SubjectId = subject.Id, TargetGrade = "1st Secondary", TeacherId = teacher.Id };
        var term = new Term { Id = termId, PackageId = packageId, Title = "E2E Term" };
        var section = new ContentSection { Id = sectionId, TermId = termId, Title = "E2E Section", Order = 0 };
        var lesson = new Lesson { Id = lessonId, ContentSectionId = sectionId, Title = "E2E Lesson", Summary = "Consume me", Order = 0 };
        var lesson2 = new Lesson { Id = lesson2Id, ContentSectionId = sectionId, Title = "E2E Lesson 2", Summary = "Essay lesson", Order = 1 };
        var lesson3 = new Lesson { Id = lesson3Id, ContentSectionId = sectionId, Title = "E2E Lesson 3", Summary = "Exam lesson", Order = 2, ExamId = examId };
        var video = new LessonVideo { Id = videoId, LessonId = lessonId, Title = "E2E Video", Provider = "youtube", ProviderVideoId = "dQw4w9WgXcQ", MaxWatchCount = 2, Order = 0 };
        var exam = new Exam { Id = examId, Title = "E2E Exam", Description = "Pass me", TotalScore = 10, PassingScore = 5, CreatedByTeacherId = teacher.Id, IsMandatory = false };
        var essayExam = new Exam { Id = essayExamId, Title = "E2E Essay Exam", Description = "Write something", TotalScore = 10, PassingScore = 5, CreatedByTeacherId = teacher.Id, IsMandatory = false };

        // Link exam to lesson
        lesson2.ExamId = essayExamId;

        var homework = new NaderGorge.Domain.Entities.Homework.Homework
        {
            Id = homeworkId,
            Title = "E2E Homework",
            Description = "Upload your solution",
            LessonId = lessonId,
            PassingScoreThreshold = 5,
            TotalScore = 40,
            IsMandatory = false
        };


        // MCQ Question 1: "What is 1+1?" → correct answer "2"
        var hwMcqQuestion1 = new NaderGorge.Domain.Entities.Homework.HomeworkQuestion
        {
            Id = hwMcqQuestion1Id,
            HomeworkId = homeworkId,
            BodyText = "ما ناتج 1+1؟",
            QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.MCQ,
            PointsActive = 10,
            Order = 1,
            PossibleAnswers = new[] { "1", "2", "3", "4" },
            CorrectAnswerKey = "2"
        };

        // MCQ Question 2: "What gas do plants produce?" → correct answer "الأكسجين"
        var hwMcqQuestion2 = new NaderGorge.Domain.Entities.Homework.HomeworkQuestion
        {
            Id = hwMcqQuestion2Id,
            HomeworkId = homeworkId,
            BodyText = "ما الغاز الذي تنتجه النباتات؟",
            QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.MCQ,
            PointsActive = 10,
            Order = 2,
            PossibleAnswers = new[] { "النيتروجين", "الأكسجين", "ثاني أكسيد الكربون", "الهيدروجين" },
            CorrectAnswerKey = "الأكسجين"
        };

        // FindTheMistake Question: base text has a mistake at indices 4-10
        var hwFtmQuestion = new NaderGorge.Domain.Entities.Homework.HomeworkQuestion
        {
            Id = hwFtmQuestionId,
            HomeworkId = homeworkId,
            BodyText = "أوجد الخطأ في النص التالي:",
            QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.FindTheMistake,
            PointsActive = 10,
            Order = 3,
            BaseText = "الشمس كوكبة تدور حولها الأرض",
            MistakeStartIndex = 6,
            MistakeEndIndex = 11
        };

        // Essay Question (existing)
        var hwQuestion = new NaderGorge.Domain.Entities.Homework.HomeworkQuestion
        {
            Id = hwQuestionId,
            HomeworkId = homeworkId,
            BodyText = "Write an essay about AI",
            QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.Essay,
            PointsActive = 10,
            Order = 4
        };

        var questionItem = new QuestionBankItem { Id = questionId, Text = "1+1=?", DefaultPoints = 10, Tags = "Inline", SubjectId = subject.Id, CreatedByTeacherId = teacher.Id };
        var correctOption = new QuestionOption { Id = optionId, QuestionBankItemId = questionId, Text = "2", IsCorrect = true };
        var wrongOption = new QuestionOption { Id = Guid.NewGuid(), QuestionBankItemId = questionId, Text = "3", IsCorrect = false };
        var examQuestion = new ExamQuestion { ExamId = examId, QuestionBankItemId = questionId, Order = 0, Points = 10 };

        var essayQuestion = new EssayQuestion { Id = essayQuestionId, Text = "Explain photosynthesis", Type = QuestionType.Essay, DefaultPoints = 10, Tags = "Inline", WrittenCorrection = "Plants use sunlight", SubjectId = subject.Id, CreatedByTeacherId = teacher.Id };
        var essayExamQuestion = new ExamQuestion { ExamId = essayExamId, QuestionBankItemId = essayQuestionId, Order = 0, Points = 10 };

        _dbContext.Set<Package>().Add(package);
        _dbContext.Set<Term>().Add(term);
        _dbContext.Set<ContentSection>().Add(section);
        _dbContext.Set<Exam>().AddRange(exam, essayExam);
        _dbContext.Set<Lesson>().AddRange(lesson, lesson2, lesson3);
        _dbContext.Set<LessonVideo>().Add(video);
        _dbContext.Set<QuestionBankItem>().AddRange(questionItem, essayQuestion);
        _dbContext.Set<QuestionOption>().AddRange(correctOption, wrongOption);
        _dbContext.Set<ExamQuestion>().AddRange(examQuestion, essayExamQuestion);

        _dbContext.Set<NaderGorge.Domain.Entities.Homework.Homework>().Add(homework);
        _dbContext.Set<NaderGorge.Domain.Entities.Homework.HomeworkQuestion>().AddRange(hwMcqQuestion1, hwMcqQuestion2, hwFtmQuestion, hwQuestion);

        await _dbContext.SaveChangesAsync();

        return Ok(new { PackageId = packageId, TermId = termId, LessonId = lessonId, Lesson2Id = lesson2Id, ExamId = examId, EssayExamId = essayExamId, HomeworkId = homeworkId, TeacherId = teacher.Id, TeacherPhone = teacher.User?.PhoneNumber });
    }

    [HttpPost("grant-package")]
    public async Task<IActionResult> GrantPackage([FromBody] GrantPackageRequest request)
    {
        // Find the student by phone number if no userId provided
        var student = await _dbContext.Set<User>().FirstOrDefaultAsync(u => u.PhoneNumber == "20000000001");
        if (student == null) return BadRequest("Student not found. Did you seed first?");

        var userId = request.UserId ?? student.Id;

        var existingGrant = await _dbContext.Set<StudentAccessGrant>()
            .FirstOrDefaultAsync(g => g.UserId == userId && g.PackageId == request.PackageId);

        if (existingGrant == null)
        {
            var package = await _dbContext.Set<Package>().FindAsync(request.PackageId);
            if (package == null) return BadRequest("Package not found.");

            // Create a dummy access code for the grant
            var codeGroup = new CodeGroup
            {
                Id = Guid.NewGuid(),
                Name = "E2E Grant",
                TotalCodes = 1,
                PackageId = request.PackageId,
                CreatedByUserId = userId,
                TeacherId = package.TeacherId
            };
            var uniqueId = Guid.NewGuid().ToString("N")[..8];
            var accessCode = new AccessCode
            {
                Id = Guid.NewGuid(),
                CodeHash = $"e2e-grant-hash-{uniqueId}",
                CodePlaintext = $"E2E-GRANT-{uniqueId}",
                CodeGroupId = codeGroup.Id,
                IsConsumed = true,
                ConsumedByUserId = userId,
                ConsumedAt = DateTime.UtcNow
            };

            _dbContext.Set<CodeGroup>().Add(codeGroup);
            _dbContext.Set<AccessCode>().Add(accessCode);

            _dbContext.Set<StudentAccessGrant>().Add(new StudentAccessGrant
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PackageId = request.PackageId,
                AccessCodeId = accessCode.Id,
                GrantedAt = DateTime.UtcNow,
                IsActive = true
            });
            await _dbContext.SaveChangesAsync();
        }

        return Ok(new { message = "Package granted successfully" });
    }

    [HttpPost("clear-devices")]
    public async Task<IActionResult> ClearDevices([FromBody] ClearDevicesRequest request)
    {
        var user = await _dbContext.Set<User>().FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
        if (user == null) return BadRequest("User not found.");

        var devices = _dbContext.Set<Device>().Where(d => d.UserId == user.Id);
        _dbContext.Set<Device>().RemoveRange(devices);
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Devices cleared" });
    }

    [HttpPost("reset-gamification")]
    public async Task<IActionResult> ResetGamification([FromBody] ClearDevicesRequest request)
    {
        var user = await _dbContext.Set<User>().FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
        if (user == null) return BadRequest("User not found.");

        var gamification = await _dbContext.Set<NaderGorge.Domain.Entities.Gamification.StudentGamification>()
            .FirstOrDefaultAsync(g => g.StudentId == user.Id);

        if (gamification != null)
        {
            _dbContext.Set<NaderGorge.Domain.Entities.Gamification.StudentGamification>().Remove(gamification);

            var logs = _dbContext.Set<NaderGorge.Domain.Entities.Gamification.GamificationActionLog>().Where(l => l.StudentId == user.Id);
            _dbContext.Set<NaderGorge.Domain.Entities.Gamification.GamificationActionLog>().RemoveRange(logs);

            await _dbContext.SaveChangesAsync();
        }

        return Ok(new { message = "Gamification reset" });
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications()
    {
        var notifications = await _dbContext.Set<Domain.Entities.Notifications.NotificationEvent>()
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new { n.Id, n.UserId, n.ChannelType, n.Title, n.Body, n.Status, n.CreatedAt })
            .ToListAsync();
        return Ok(notifications);
    }

    [HttpPost("set-role-permissions")]
    public async Task<IActionResult> SetRolePermissions([FromBody] SetRolePermissionsRequest request)
    {
        var role = await _dbContext.Set<Role>().FirstOrDefaultAsync(r => r.Name == request.RoleName);
        if (role == null) return BadRequest($"Role '{request.RoleName}' not found.");

        role.PermissionsJson = System.Text.Json.JsonSerializer.Serialize(request.Permissions);
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = $"Permissions for role '{request.RoleName}' updated successfully.", permissions = request.Permissions });
    }

    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _dbContext.Set<User>().Select(u => new { u.Id, u.PhoneNumber, u.FullName }).ToListAsync();
        return Ok(users);
    }

    [HttpGet("grants")]
    public async Task<IActionResult> GetGrants()
    {
        var grants = await _dbContext.Set<StudentAccessGrant>().ToListAsync();
        return Ok(grants);
    }

    private bool UsesE2eDatabase()
    {
        var connectionString = _dbContext.Database.GetConnectionString() ?? string.Empty;
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? string.Empty;
        return env.Equals("E2e", StringComparison.OrdinalIgnoreCase) ||
               connectionString.Contains("e2e", StringComparison.OrdinalIgnoreCase) ||
               connectionString.Contains("test", StringComparison.OrdinalIgnoreCase);
    }
}

public class GrantPackageRequest
{
    public Guid PackageId { get; set; }
    public Guid? UserId { get; set; }
}

public class SeedRequest
{
    public bool ClearDatabase { get; set; }
    public bool SeedAdmin { get; set; }
    public bool SeedStudents { get; set; }
    public bool SeedAssistant { get; set; }
    public bool SeedTeacher { get; set; }
}

public class ClearDevicesRequest
{
    public string PhoneNumber { get; set; } = "";
}

public class SetRolePermissionsRequest
{
    public string RoleName { get; set; } = "";
    public List<string> Permissions { get; set; } = new();
}
