using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.Parent.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NaderGorge.Application.Tests.Parent;

public class VerifyCodeTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _config;

    public VerifyCodeTests()
    {
        _db = TestAppDbContextFactory.Create();

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "SuperSecretKeyForParentVerificationTestingOnly!",
                ["JwtSettings:Issuer"] = "NaderGorge",
                ["JwtSettings:Audience"] = "NaderGorgeParents"
            })
            .Build();

        _tokenService = new TokenService(_config);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public void StudentProfile_Constructor_ShouldGenerateUnique6CharTrackingCode()
    {
        // Act
        var profile1 = new StudentProfile();
        var profile2 = new StudentProfile();

        // Assert
        Assert.NotNull(profile1.ParentTrackingCode);
        Assert.Equal(6, profile1.ParentTrackingCode.Length);
        Assert.Matches("^[A-Z0-9]{6}$", profile1.ParentTrackingCode);
        Assert.NotEqual(profile1.ParentTrackingCode, profile2.ParentTrackingCode);
    }

    [Fact]
    public async Task VerifyCode_ValidCode_ShouldReturnTokenAndStudentName()
    {
        // Arrange
        var user = new User { FullName = "تست طالب", PhoneNumber = "01000000000", PasswordHash = "hash" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var profile = new StudentProfile
        {
            UserId = user.Id,
            ParentTrackingCode = "ABC123"
        };
        _db.StudentProfiles.Add(profile);
        await _db.SaveChangesAsync();

        var handler = new VerifyParentCodeCommandHandler(_db, _tokenService);
        var command = new VerifyParentCodeCommand("ABC123", null, null);

        // Act
        var response = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("تست طالب", response.Data.StudentName);
        Assert.NotEmpty(response.Data.Token);

        // Validate the JWT Token
        var principal = _tokenService.ValidateToken(response.Data.Token);
        Assert.NotNull(principal);
        Assert.True(principal.IsInRole("Parent"));
        var studentIdClaim = principal.FindFirst("StudentId")?.Value;
        Assert.Equal(profile.Id.ToString(), studentIdClaim);
    }

    [Fact]
    public async Task VerifyCode_InvalidCode_ShouldReturnFailure()
    {
        // Arrange
        var handler = new VerifyParentCodeCommandHandler(_db, _tokenService);
        var command = new VerifyParentCodeCommand("XYZ999", null, null);

        // Act
        var response = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(response.Success);
        Assert.Equal("الرمز غير صالح، يرجى التحقق وإعادة المحاولة", response.Message);
    }

    [Fact]
    public async Task VerifyCode_WithDeviceToken_ShouldRegisterTokenOnce()
    {
        // Arrange
        var user = new User { FullName = "تست طالب", PhoneNumber = "01000000000", PasswordHash = "hash" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var profile = new StudentProfile
        {
            UserId = user.Id,
            ParentTrackingCode = "FCM789"
        };
        _db.StudentProfiles.Add(profile);
        await _db.SaveChangesAsync();

        var handler = new VerifyParentCodeCommandHandler(_db, _tokenService);
        var command = new VerifyParentCodeCommand("FCM789", "mock_fcm_token", "android");

        // Act - 1st call
        var response1 = await handler.Handle(command, CancellationToken.None);
        // Act - 2nd call
        var response2 = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(response1.Success);
        Assert.True(response2.Success);

        var registeredTokens = await _db.ParentDeviceTokens
            .Where(t => t.StudentId == profile.Id)
            .ToListAsync();

        Assert.Single(registeredTokens);
        Assert.Equal("mock_fcm_token", registeredTokens[0].DeviceToken);
        Assert.Equal("android", registeredTokens[0].Platform);
    }
}
