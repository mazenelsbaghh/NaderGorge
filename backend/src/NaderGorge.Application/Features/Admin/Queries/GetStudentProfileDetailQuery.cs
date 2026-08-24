using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public class GetStudentProfileDetailQuery : IRequest<StudentProfileExtendedDto>
{
    public Guid UserId { get; set; }

    public GetStudentProfileDetailQuery(Guid userId)
    {
        UserId = userId;
    }
}

public class GetStudentProfileDetailQueryHandler : IRequestHandler<GetStudentProfileDetailQuery, StudentProfileExtendedDto>
{
    private readonly IAppDbContext _context;

    public GetStudentProfileDetailQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<StudentProfileExtendedDto> Handle(GetStudentProfileDetailQuery request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .Include(u => u.StudentProfile)
            .Include(u => u.Devices)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            throw new KeyNotFoundException($"Student profile for user {request.UserId} not found.");
        }

        var gamification = await _context.StudentGamifications
            .FirstOrDefaultAsync(g => g.StudentId == request.UserId, cancellationToken);

        var rankPosition = gamification != null ? await _context.StudentGamifications
            .CountAsync(g => g.TotalPoints > gamification.TotalPoints, cancellationToken) + 1 : 0;

        // Load ALL grants (Package, Term, Month, Lesson) with proper names
        var allGrants = await _context.StudentAccessGrants
            .AsNoTracking()
            .Where(g => g.UserId == request.UserId)
            .Include(g => g.CancelledByUser)
            .Include(g => g.AccessCode)
                .ThenInclude(c => c!.CodeGroup)
                    .ThenInclude(cg => cg.Teacher)
                        .ThenInclude(t => t!.User)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(cancellationToken);

        var packageGrantIds = allGrants
            .Where(grant => grant.GrantType == NaderGorge.Domain.Enums.CodeType.Package && grant.PackageId.HasValue)
            .Select(grant => grant.PackageId!.Value)
            .Distinct()
            .ToList();
        var termGrantIds = allGrants
            .Where(grant => grant.GrantType == NaderGorge.Domain.Enums.CodeType.Term && grant.TermId.HasValue)
            .Select(grant => grant.TermId!.Value)
            .Distinct()
            .ToList();
        var sectionGrantIds = allGrants
            .Where(grant => grant.GrantType == NaderGorge.Domain.Enums.CodeType.Month && grant.ContentSectionId.HasValue)
            .Select(grant => grant.ContentSectionId!.Value)
            .Distinct()
            .ToList();
        var lessonGrantIds = allGrants
            .Where(grant => grant.GrantType == NaderGorge.Domain.Enums.CodeType.Lesson && grant.LessonId.HasValue)
            .Select(grant => grant.LessonId!.Value)
            .Distinct()
            .ToList();

        var grantedPackages = await _context.Packages
            .AsNoTracking()
            .Where(package => packageGrantIds.Contains(package.Id))
            .Select(package => new
            {
                package.Id,
                package.Name,
                package.Price,
                package.TeacherId,
                TeacherName = package.Teacher.User.FullName
            })
            .ToDictionaryAsync(package => package.Id, cancellationToken);
        var grantedTerms = await _context.Terms
            .AsNoTracking()
            .Where(term => termGrantIds.Contains(term.Id))
            .Select(term => new
            {
                term.Id,
                PackageName = term.Package.Name,
                term.Title,
                term.Price,
                term.Package.TeacherId,
                TeacherName = term.Package.Teacher.User.FullName
            })
            .ToDictionaryAsync(term => term.Id, cancellationToken);
        var grantedSections = await _context.ContentSections
            .AsNoTracking()
            .Where(section => sectionGrantIds.Contains(section.Id))
            .Select(section => new
            {
                section.Id,
                PackageName = section.Term.Package.Name,
                section.Title,
                section.Price,
                section.Term.Package.TeacherId,
                TeacherName = section.Term.Package.Teacher.User.FullName
            })
            .ToDictionaryAsync(section => section.Id, cancellationToken);
        var grantedLessons = await _context.Lessons
            .AsNoTracking()
            .Where(lesson => lessonGrantIds.Contains(lesson.Id))
            .Select(lesson => new
            {
                lesson.Id,
                PackageName = lesson.ContentSection.Term.Package.Name,
                lesson.Title,
                lesson.Price,
                lesson.ContentSection.Term.Package.TeacherId,
                TeacherName = lesson.ContentSection.Term.Package.Teacher.User.FullName
            })
            .ToDictionaryAsync(lesson => lesson.Id, cancellationToken);

        var purchaseEffects = await _context.SalesFinancialEffects
            .AsNoTracking()
            .Where(effect => effect.StudentId == request.UserId)
            .Select(effect => new
            {
                effect.PurchaseOperationId,
                effect.TargetId,
                effect.TeacherId,
                TeacherName = effect.Teacher != null ? effect.Teacher.User.FullName : null,
                effect.PaidAmount,
                effect.PlatformShareImpact,
                effect.TeacherShareImpact,
                effect.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var packages = new List<StudentPackageDto>();
        foreach (var grant in allGrants)
        {
            string name = "غير معروف";
            decimal price = 0m;
            Guid contentId = Guid.Empty;
            Guid? contentTeacherId = null;
            string? contentTeacherName = null;
            var codeTeacherId = grant.AccessCode?.CodeGroup?.TeacherId;
            var codeTeacherName = grant.AccessCode?.CodeGroup?.Teacher?.User?.FullName;

            switch (grant.GrantType)
            {
                case NaderGorge.Domain.Enums.CodeType.Package:
                    if (grant.PackageId.HasValue && grantedPackages.TryGetValue(grant.PackageId.Value, out var package))
                    {
                        name = package.Name;
                        price = package.Price;
                        contentId = package.Id;
                        contentTeacherId = package.TeacherId;
                        contentTeacherName = package.TeacherName;
                    }
                    else
                    {
                        name = string.IsNullOrWhiteSpace(codeTeacherName)
                            ? "باكدج عام للمنصة"
                            : $"باكدج عام لمدرس {codeTeacherName}";
                    }
                    break;
                case NaderGorge.Domain.Enums.CodeType.Term:
                    if (grant.TermId.HasValue && grantedTerms.TryGetValue(grant.TermId.Value, out var term))
                    {
                        name = $"{term.PackageName} — {term.Title}";
                        price = term.Price;
                        contentId = term.Id;
                        contentTeacherId = term.TeacherId;
                        contentTeacherName = term.TeacherName;
                    }
                    break;
                case NaderGorge.Domain.Enums.CodeType.Month:
                    if (grant.ContentSectionId.HasValue && grantedSections.TryGetValue(grant.ContentSectionId.Value, out var section))
                    {
                        name = $"{section.PackageName} — {section.Title}";
                        price = section.Price;
                        contentId = section.Id;
                        contentTeacherId = section.TeacherId;
                        contentTeacherName = section.TeacherName;
                    }
                    break;
                case NaderGorge.Domain.Enums.CodeType.Lesson:
                    if (grant.LessonId.HasValue && grantedLessons.TryGetValue(grant.LessonId.Value, out var lesson))
                    {
                        name = $"{lesson.PackageName} — {lesson.Title}";
                        price = lesson.Price;
                        contentId = lesson.Id;
                        contentTeacherId = lesson.TeacherId;
                        contentTeacherName = lesson.TeacherName;
                    }
                    break;
            }

            var purchaseEffect = purchaseEffects
                .Where(effect => effect.TargetId == contentId)
                .OrderBy(effect => Math.Abs((effect.CreatedAt - grant.CreatedAt).Ticks))
                .FirstOrDefault();

            var resolvedTeacherId = contentTeacherId;
            var resolvedTeacherName = contentTeacherName;
            if (codeTeacherId.HasValue)
            {
                resolvedTeacherId = codeTeacherId;
                resolvedTeacherName = codeTeacherName;
            }
            if (purchaseEffect?.TeacherId is not null)
            {
                resolvedTeacherId = purchaseEffect.TeacherId;
                resolvedTeacherName = purchaseEffect.TeacherName;
            }

            packages.Add(new StudentPackageDto
            {
                Id = contentId,
                AccessGrantId = grant.Id,
                Name = name,
                EnrolledAt = grant.CreatedAt,
                ExpiresAt = grant.ExpiresAt,
                Progress = 0,
                IsActive = grant.IsActive,
                PurchaseMethod = grant.AccessCodeId.HasValue ? "Code" : "Balance",
                Price = price,
                PurchaseOperationId = purchaseEffect?.PurchaseOperationId,
                TeacherId = resolvedTeacherId,
                TeacherName = resolvedTeacherName,
                PaidAmount = purchaseEffect?.PaidAmount ?? 0m,
                PlatformShareAmount = purchaseEffect?.PlatformShareImpact ?? 0m,
                TeacherShareAmount = purchaseEffect?.TeacherShareImpact ?? 0m,
                GrantType = grant.GrantType.ToString(),
                CancelledByName = grant.CancelledByUser?.FullName,
                CancelledAt = grant.CancelledAt,
                CancellationReason = grant.CancellationReason
            });
        }

        var overrides = await _context.VideoOverrides
            .Include(o => o.LessonVideo)
            .Include(o => o.PerformedByUser)
            .Where(o => o.UserId == request.UserId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new VideoOverrideDto
            {
                Id = o.Id,
                VideoId = o.LessonVideoId,
                VideoTitle = o.LessonVideo.Title,
                OriginalLimit = o.OriginalLimit,
                NewLimit = o.NewLimit,
                AddedViews = o.AddedViews,
                Reason = o.Reason,
                OverrideBy = o.PerformedByUser.FullName,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // Map devices
        var devices = user.Devices.Select(d => new StudentDeviceDto
        {
            Id = d.Id,
            DeviceName = d.DeviceName ?? d.DeviceFingerprint,
            IpAddress = d.IpAddress,
            OsName = d.OsName,
            BrowserName = d.BrowserName,
            DeviceType = d.DeviceType,
            LastActiveAt = d.LastUsedAt,
            IsActive = d.IsActive
        }).OrderByDescending(d => d.LastActiveAt).ToList();

        var watchActivities = await _context.VideoWatchEvents
            .Where(v => v.UserId == request.UserId)
            .Include(v => v.LessonVideo)
                .ThenInclude(video => video.Lesson)
                    .ThenInclude(lesson => lesson.ContentSection)
                        .ThenInclude(section => section.Term)
                            .ThenInclude(term => term.Package)
            .OrderByDescending(v => v.UpdatedAt)
            .Select(v => new StudentVideoWatchActivityDto
            {
                LessonVideoId = v.LessonVideoId,
                VideoTitle = v.LessonVideo.Title,
                LessonId = v.LessonVideo.LessonId,
                LessonTitle = v.LessonVideo.Lesson.Title,
                PackageName = v.LessonVideo.Lesson.ContentSection.Term.Package.Name,
                TermTitle = v.LessonVideo.Lesson.ContentSection.Term.Title,
                WatchCount = v.WatchCount,
                MaxWatchCount = v.CustomMaxWatchCount ?? v.LessonVideo.MaxWatchCount,
                WatchedSeconds = Math.Max(0, v.TimeWatchedInSeconds),
                ActualWatchedSeconds = Math.Max(0m, v.ActualWatchedSeconds),
                LastPlaybackRate = v.LastPlaybackRate,
                PlaybackRateBreakdownJson = v.PlaybackRateBreakdownJson,
                IsLocked = v.IsLocked && v.WatchCount >= (v.CustomMaxWatchCount ?? v.LessonVideo.MaxWatchCount),
                LastWatchedAt = v.UpdatedAt ?? v.CreatedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var activity in watchActivities)
        {
            activity.PlaybackRateSeconds = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(activity.PlaybackRateBreakdownJson) ?? new();
            activity.AveragePlaybackRate = activity.ActualWatchedSeconds > 0
                ? decimal.Round(activity.WatchedSeconds / (decimal)activity.ActualWatchedSeconds, 2)
                : activity.LastPlaybackRate;
        }

        var auditLogs = await _context.AuditLogs
            .Include(a => a.PerformedByUser)
            .Where(a => a.EntityType == "User" && a.EntityId == request.UserId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                Action = a.Action,
                AdminName = a.PerformedByUser != null ? a.PerformedByUser.FullName : "System",
                Date = a.CreatedAt,
                Details = a.NewValues ?? string.Empty,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                OldValues = a.OldValues,
                NewValues = a.NewValues,
                IpAddress = a.IpAddress
            })
            .ToListAsync(cancellationToken);

        var balance = await _context.StudentBalances
            .FirstOrDefaultAsync(b => b.UserId == request.UserId, cancellationToken);

        var balanceTransactions = new List<StudentBalanceTransactionDto>();
        if (balance != null)
        {
            balanceTransactions = await _context.BalanceTransactions
                .Include(t => t.PerformedByUser)
                .Where(t => t.StudentBalanceId == balance.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new StudentBalanceTransactionDto
                {
                    Id = t.Id,
                    Amount = t.Amount,
                    BalanceAfter = t.BalanceAfter,
                    BalanceBefore = t.BalanceAfter - t.Amount,
                    BalanceScope = "الرصيد العام",
                    ContentName = t.TransactionType == "ContentPurchase" ? t.Description : null,
                    TransactionType = t.TransactionType,
                    Description = t.Description,
                    CreatedAt = t.CreatedAt,
                    AdminName = t.PerformedByUser != null ? t.PerformedByUser.FullName : (t.TransactionType == "AdminAdjustment" ? "مدير النظام" : "النظام")
                })
                .ToListAsync(cancellationToken);
        }

        var promotionalUsages = await _context.PromotionalBalanceUsages
            .AsNoTracking()
            .Where(x => x.Allocation.StudentId == request.UserId)
            .Select(x => new
            {
                x.Id,
                x.AllocationId,
                x.Allocation.OriginalAmount,
                x.Amount,
                x.ContentType,
                x.ContentId,
                x.CreatedAt,
                TeacherName = x.Allocation.Teacher != null ? x.Allocation.Teacher.User.FullName : "رصيد مخصص عام"
            })
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var consumedByAllocation = new Dictionary<Guid, decimal>();
        foreach (var usage in promotionalUsages)
        {
            var consumedBefore = consumedByAllocation.GetValueOrDefault(usage.AllocationId);
            var balanceBefore = Math.Max(0m, usage.OriginalAmount - consumedBefore);
            var contentName = usage.ContentType switch
            {
                NaderGorge.Domain.Enums.CodeType.Package when grantedPackages.TryGetValue(usage.ContentId, out var package) => package.Name,
                NaderGorge.Domain.Enums.CodeType.Term when grantedTerms.TryGetValue(usage.ContentId, out var term) => $"{term.PackageName} — {term.Title}",
                NaderGorge.Domain.Enums.CodeType.Month when grantedSections.TryGetValue(usage.ContentId, out var section) => $"{section.PackageName} — {section.Title}",
                NaderGorge.Domain.Enums.CodeType.Lesson when grantedLessons.TryGetValue(usage.ContentId, out var lesson) => $"{lesson.PackageName} — {lesson.Title}",
                _ => $"{usage.ContentType} ({usage.ContentId})"
            };

            balanceTransactions.Add(new StudentBalanceTransactionDto
            {
                Id = usage.Id,
                Amount = -usage.Amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = Math.Max(0m, balanceBefore - usage.Amount),
                BalanceScope = $"رصيد المدرس {usage.TeacherName}",
                ContentName = contentName,
                TransactionType = "ContentPurchase",
                Description = $"شراء {contentName} من رصيد المدرس",
                CreatedAt = usage.CreatedAt,
                AdminName = "النظام"
            });
            consumedByAllocation[usage.AllocationId] = consumedBefore + usage.Amount;
        }

        balanceTransactions = balanceTransactions
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

        var rechargeRequests = await _context.RechargeRequests
            .AsNoTracking()
            .Where(x => x.UserId == request.UserId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new StudentRechargeRequestDto
            {
                Id = x.Id,
                Amount = x.Amount,
                BalanceScope = x.Teacher != null ? $"رصيد المدرس {x.Teacher.User.FullName}" : "الرصيد العام",
                WalletLabel = x.Wallet.Label,
                WalletPhoneNumber = x.Wallet.PhoneNumber,
                SenderPhoneNumber = x.SenderPhoneNumber,
                Status = x.Status.ToString(),
                HasMatchedSms = x.MatchedSmsLogId.HasValue,
                CreatedAt = x.CreatedAt,
                ResolvedAt = x.ResolvedAt,
                RejectionReason = x.RejectionReason
            })
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var promotionalBalancesRaw = await _context.PromotionalBalanceAllocations
            .AsNoTracking()
            .Where(x => x.StudentId == request.UserId && x.AvailableAmount > 0 && (x.ExpiresAt == null || x.ExpiresAt > now))
            .Select(x => new
            {
                x.TeacherId,
                TeacherName = x.Teacher != null ? x.Teacher.User.FullName : "رصيد مخصص عام",
                x.AvailableAmount,
                x.OriginalAmount,
                x.ConsumedAmount,
                x.ExpiresAt
            })
            .ToListAsync(cancellationToken);

        var promotionalBalances = promotionalBalancesRaw
            .GroupBy(x => new { x.TeacherId, x.TeacherName })
            .Select(group => new StudentPromotionalBalanceDto
            {
                TeacherId = group.Key.TeacherId,
                TeacherName = group.Key.TeacherName,
                AvailableAmount = group.Sum(x => x.AvailableAmount),
                OriginalAmount = group.Sum(x => x.OriginalAmount),
                ConsumedAmount = group.Sum(x => x.ConsumedAmount),
                NearestExpiresAt = group
                    .Where(x => x.ExpiresAt.HasValue)
                    .OrderBy(x => x.ExpiresAt)
                    .Select(x => x.ExpiresAt)
                    .FirstOrDefault()
            })
            .OrderByDescending(x => x.AvailableAmount)
            .ThenBy(x => x.TeacherName)
            .ToList();

        return new StudentProfileExtendedDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Phone = user.PhoneNumber,
            ParentPhone = user.StudentProfile?.ParentPhone,
            SecondaryPhone = user.StudentProfile?.SecondaryPhone,
            SecondaryParentPhone = user.StudentProfile?.SecondaryParentPhone,
            District = user.StudentProfile?.District,
            Grade = user.StudentProfile?.GradeLevel.ToString(),
            SchoolName = user.StudentProfile?.SchoolName,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,

            // ── Personal fields ─────────────────────────────────────────
            DateOfBirth = user.StudentProfile?.DateOfBirth,
            Gender = user.StudentProfile?.Gender.ToString(),
            Governorate = user.StudentProfile?.Governorate,
            Address = user.StudentProfile?.Address,
            StudentCode = user.StudentProfile?.StudentCode,
            ParentTrackingCode = user.StudentProfile?.ParentTrackingCode,
            IsProfileComplete = user.IsProfileComplete,

            // ── Academic fields ──────────────────────────────────────────
            EducationStage = user.StudentProfile?.EducationStage.ToString(),
            StudyTrack = user.StudentProfile?.StudyTrack?.ToString(),

            // ── Student Profile V2 fields ─────────────────────────────────
            Nationality = user.StudentProfile?.Nationality,
            MotherPhone = user.StudentProfile?.MotherPhone,
            FatherDateOfBirth = user.StudentProfile?.FatherDateOfBirth,
            MotherDateOfBirth = user.StudentProfile?.MotherDateOfBirth,
            SchoolType = user.StudentProfile?.SchoolType?.ToString(),
            IsFatherAlive = user.StudentProfile?.IsFatherAlive ?? true,
            IsMotherAlive = user.StudentProfile?.IsMotherAlive ?? true,

            Gamification = gamification != null ? new GamificationSummaryDto
            {
                TotalPoints = gamification.TotalPoints,
                GlobalRank = rankPosition,
                Level = 0,
                Title = gamification.LevelName ?? string.Empty,
                RecentBadges = new List<string>()
            } : null,
            Packages = packages,
            Devices = devices,
            Overrides = overrides,
            WatchTracking = new WatchTrackingSummaryDto
            {
                TotalWatchedSeconds = watchActivities.Sum(activity => activity.WatchedSeconds),
                TotalActualWatchedSeconds = watchActivities.Sum(activity => activity.ActualWatchedSeconds),
                AveragePlaybackRate = CalculateAveragePlaybackRate(watchActivities),
                WatchedVideosCount = watchActivities.Count,
                Activities = watchActivities
            },
            CurrentBalance = balance?.CurrentBalance ?? 0m,
            PromotionalBalances = promotionalBalances,
            BalanceTransactions = balanceTransactions,
            RechargeRequests = rechargeRequests,
            AuditTrail = auditLogs,
            Notes = await _context.StudentNotes
                .Include(n => n.Admin)
                .Where(n => n.StudentId == request.UserId)
                .OrderByDescending(n => n.IsPinned)
                .ThenByDescending(n => n.CreatedAt)
                .Select(n => new StudentNoteDto
                {
                    Id = n.Id,
                    Content = n.Content,
                    AdminName = n.Admin.FullName,
                    IsPinned = n.IsPinned,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync(cancellationToken)
        };
    }

    private static decimal CalculateAveragePlaybackRate(IReadOnlyCollection<StudentVideoWatchActivityDto> activities)
    {
        var actualWatchedSeconds = activities.Sum(activity => activity.ActualWatchedSeconds);
        return actualWatchedSeconds > 0
            ? decimal.Round(activities.Sum(activity => activity.WatchedSeconds) / actualWatchedSeconds, 2)
            : 1m;
    }
}
