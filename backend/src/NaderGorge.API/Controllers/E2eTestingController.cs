using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using System.Security.Cryptography;
using NaderGorge.API.Configuration;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/e2e")]
public class E2eTestingController : ControllerBase
{
    private readonly DbContext _dbContext;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public E2eTestingController(IAppDbContext dbContext, IWebHostEnvironment env, IConfiguration configuration)
    {
        _dbContext = (DbContext)dbContext;
        _env = env;
        _configuration = configuration;
    }

    [HttpPost("seed")]
    public async Task<IActionResult> SeedDatabase([FromBody] SeedRequest request)
    {
        if (!IsAuthorizedE2eRequest(out var rejection)) return rejection;

        if (request.ClearDatabase)
        {
            if (!UsesE2eDatabase())
                return BadRequest("E2E destructive reset requires an E2E/test database connection string.");

            await _dbContext.Database.EnsureDeletedAsync();
            await _dbContext.Database.EnsureCreatedAsync();
        }

        if (request.SeedAdmin || request.SeedStudents)
        {
            var adminRole = new Role { Id = Guid.NewGuid(), Name = "Admin", Type = RoleType.Admin };
            var studentRole = new Role { Id = Guid.NewGuid(), Name = "Student", Type = RoleType.Student };
            var assistantRole = new Role { Id = Guid.NewGuid(), Name = "Assistant", Type = RoleType.Assistant };
            _dbContext.Set<Role>().AddRange(adminRole, studentRole, assistantRole);

            if (request.SeedAdmin)
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

            if (request.SeedAssistant)
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

            if (request.SeedStudents)
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
                var student2 = new User
                {
                    Id = Guid.NewGuid(),
                    FullName = "E2E Student MaxDevices",
                    PhoneNumber = "20000000002",
                    PasswordHash = HashPassword("password"),
                    IsActive = true,
                    IsProfileComplete = true
                };
                _dbContext.Set<User>().AddRange(student1, student2);
                _dbContext.Set<UserRole>().Add(new UserRole { UserId = student1.Id, RoleId = studentRole.Id });
                _dbContext.Set<UserRole>().Add(new UserRole { UserId = student2.Id, RoleId = studentRole.Id });

                // Pre-register 2 devices for student2 to test device limits
                _dbContext.Set<Device>().Add(new Device { UserId = student2.Id, DeviceFingerprint = "e2e-dev1", DeviceName = "Dev1", IpAddress = "127.0.0.1", IsActive = true, LastUsedAt = DateTime.UtcNow });
                _dbContext.Set<Device>().Add(new Device { UserId = student2.Id, DeviceFingerprint = "e2e-dev2", DeviceName = "Dev2", IpAddress = "127.0.0.1", IsActive = true, LastUsedAt = DateTime.UtcNow });
            }

            await _dbContext.SaveChangesAsync();
        }

        return Ok(new { message = "E2E Database successfully seeded." });
    }

    [HttpPost("setup-mock-package")]
    public async Task<IActionResult> SetupMockPackage()
    {
        if (!IsAuthorizedE2eRequest(out var rejection)) return rejection;

        // Create a Program first (Package requires ProgramId)
        var programId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var lesson2Id = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        var examId = Guid.NewGuid();
        var essayExamId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var essayQuestionId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var homeworkId = Guid.NewGuid();
        var hwQuestionId = Guid.NewGuid();

        var termId = Guid.NewGuid();
        var program = new Domain.Entities.Program { Id = programId, Name = "E2E Program", Description = "Test Program", TargetGrade = "1st Secondary" };
        var package = new Package { Id = packageId, Name = "E2E Student Package", Description = "Test", Price = 100, ProgramId = programId };
        var term = new Term { Id = termId, PackageId = packageId, Title = "E2E Term" };
        var section = new ContentSection { Id = sectionId, TermId = termId, Title = "E2E Section", Order = 0 };
        var lesson = new Lesson { Id = lessonId, ContentSectionId = sectionId, Title = "E2E Lesson", Summary = "Consume me", Order = 0 };
        var lesson2 = new Lesson { Id = lesson2Id, ContentSectionId = sectionId, Title = "E2E Lesson 2", Summary = "Essay lesson", Order = 1 };
        var video = new LessonVideo { Id = videoId, LessonId = lessonId, Title = "E2E Video", Provider = "vimeo", ProviderVideoId = "12345", MaxWatchCount = 2, Order = 0 };
        var exam = new Exam { Id = examId, Title = "E2E Exam", Description = "Pass me", TotalScore = 10, PassingScore = 5 };
        var essayExam = new Exam { Id = essayExamId, Title = "E2E Essay Exam", Description = "Write something", TotalScore = 10, PassingScore = 5 };

        // Link exam to lesson
        lesson.ExamId = examId;
        lesson2.ExamId = essayExamId;
        
        var homework = new NaderGorge.Domain.Entities.Homework.Homework
        {
            Id = homeworkId,
            Title = "E2E Homework",
            Description = "Upload your solution",
            LessonId = lessonId,
            PassingScoreThreshold = 5,
            IsMandatory = true
        };

        var hwQuestion = new NaderGorge.Domain.Entities.Homework.HomeworkQuestion
        {
            Id = hwQuestionId,
            HomeworkId = homeworkId,
            BodyText = "Write an essay about AI",
            QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.Essay,
            PointsActive = 10,
            Order = 1
        };

        var questionItem = new QuestionBankItem { Id = questionId, Text = "1+1=?", DefaultPoints = 10, Tags = "Inline" };
        var correctOption = new QuestionOption { Id = optionId, QuestionBankItemId = questionId, Text = "2", IsCorrect = true };
        var wrongOption = new QuestionOption { Id = Guid.NewGuid(), QuestionBankItemId = questionId, Text = "3", IsCorrect = false };
        var examQuestion = new ExamQuestion { ExamId = examId, QuestionBankItemId = questionId, Order = 0, Points = 10 };

        var essayQuestion = new EssayQuestion { Id = essayQuestionId, Text = "Explain photosynthesis", Type = QuestionType.Essay, DefaultPoints = 10, Tags = "Inline", WrittenCorrection = "Plants use sunlight" };
        var essayExamQuestion = new ExamQuestion { ExamId = essayExamId, QuestionBankItemId = essayQuestionId, Order = 0, Points = 10 };

        _dbContext.Set<Domain.Entities.Program>().Add(program);
        _dbContext.Set<Package>().Add(package);
        _dbContext.Set<Term>().Add(term);
        _dbContext.Set<ContentSection>().Add(section);
        _dbContext.Set<Exam>().AddRange(exam, essayExam);
        _dbContext.Set<Lesson>().AddRange(lesson, lesson2);
        _dbContext.Set<LessonVideo>().Add(video);
        _dbContext.Set<QuestionBankItem>().AddRange(questionItem, essayQuestion);
        _dbContext.Set<QuestionOption>().AddRange(correctOption, wrongOption);
        _dbContext.Set<ExamQuestion>().AddRange(examQuestion, essayExamQuestion);
        
        _dbContext.Set<NaderGorge.Domain.Entities.Homework.Homework>().Add(homework);
        _dbContext.Set<NaderGorge.Domain.Entities.Homework.HomeworkQuestion>().Add(hwQuestion);

        await _dbContext.SaveChangesAsync();

        return Ok(new { PackageId = packageId, LessonId = lessonId, Lesson2Id = lesson2Id, ExamId = examId, EssayExamId = essayExamId, HomeworkId = homeworkId });
    }

    [HttpPost("grant-package")]
    public async Task<IActionResult> GrantPackage([FromBody] GrantPackageRequest request)
    {
        if (!IsAuthorizedE2eRequest(out var rejection)) return rejection;

        // Find the student by phone number if no userId provided
        var student = await _dbContext.Set<User>().FirstOrDefaultAsync(u => u.PhoneNumber == "20000000001");
        if (student == null) return BadRequest("Student not found. Did you seed first?");

        var userId = request.UserId ?? student.Id;

        var existingGrant = await _dbContext.Set<StudentAccessGrant>()
            .FirstOrDefaultAsync(g => g.UserId == userId && g.PackageId == request.PackageId);

        if (existingGrant == null)
        {
            // Create a dummy access code for the grant
            var codeGroup = new CodeGroup
            {
                Id = Guid.NewGuid(),
                Name = "E2E Grant",
                TotalCodes = 1,
                PackageId = request.PackageId,
                CreatedByUserId = userId
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
        if (!IsAuthorizedE2eRequest(out var rejection)) return rejection;

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
        if (!IsAuthorizedE2eRequest(out var rejection)) return rejection;

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
        if (!IsAuthorizedE2eRequest(out var rejection)) return rejection;
        var notifications = await _dbContext.Set<Domain.Entities.Notifications.NotificationEvent>()
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new { n.Id, n.UserId, n.ChannelType, n.Title, n.Body, n.Status, n.CreatedAt })
            .ToListAsync();
        return Ok(notifications);
    }

    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    private bool IsAuthorizedE2eRequest(out IActionResult rejection)
    {
        if (_env.EnvironmentName != "E2e")
        {
            rejection = NotFound("E2E endpoints only available in E2E environment.");
            return false;
        }

        var configuredToken = _configuration["E2E_TEST_TOKEN"];
        var suppliedToken = Request.Headers["X-E2E-Token"].FirstOrDefault();
        if (!ServiceTokenValidator.IsValid(suppliedToken, configuredToken))
        {
            rejection = Unauthorized("Invalid E2E token.");
            return false;
        }

        rejection = Ok();
        return true;
    }

    private bool UsesE2eDatabase()
    {
        var connectionString = _dbContext.Database.GetConnectionString() ?? string.Empty;
        return connectionString.Contains("e2e", StringComparison.OrdinalIgnoreCase) ||
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
}

public class ClearDevicesRequest
{
    public string PhoneNumber { get; set; } = "";
}
