using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.API.Middleware;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Auth.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests.Auth;

public sealed class AuthSessionSafetyTests
{
    [Fact]
    public async Task DisabledUser_CannotRefresh()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Inactive Student", "154001");
        user.IsActive = false;
        var role = new Role { Id = Guid.NewGuid(), Name = "Student", Type = RoleType.Student };
        db.Roles.Add(role);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, Token = "refresh-disabled", ExpiresAt = DateTime.UtcNow.AddDays(1) });
        await db.SaveChangesAsync();

        var handler = new RefreshTokenCommandHandler(db, new TokenService(TestJwtConfig()), TestJwtConfig());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new RefreshTokenCommand("refresh-disabled"), CancellationToken.None));

        Assert.False(await db.RefreshTokens.AnyAsync(token => token.Token != "refresh-disabled"));
    }

    [Fact]
    public void GeneratedAccessToken_IncludesSecurityStampVersion()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Versioned User",
            PhoneNumber = "154002",
            SecurityStampVersion = 7,
            PasswordResetVersion = 3
        };
        var token = new TokenService(TestJwtConfig()).GenerateAccessToken(user, ["Student"], TimeSpan.FromMinutes(5));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("7", jwt.Claims.Single(claim => claim.Type == "securityStampVersion").Value);
        Assert.Equal("3", jwt.Claims.Single(claim => claim.Type == "passwordResetVersion").Value);
    }

    [Fact]
    public async Task ResetPassword_IncrementsPasswordAndSecurityVersionsAndRevokesRefreshTokens()
    {
        await using var db = TestAppDbContextFactory.Create();
        var config = TestJwtConfig();
        var tokenService = new TokenService(config);
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Reset User", "154005");
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, Token = "refresh-reset", ExpiresAt = DateTime.UtcNow.AddDays(1) });
        await db.SaveChangesAsync();
        var resetToken = tokenService.GenerateAccessToken(user, ["PasswordReset"], TimeSpan.FromMinutes(10));

        var result = await new ResetPasswordCommandHandler(db, tokenService)
            .Handle(new ResetPasswordCommand(resetToken, "NewPassword123!"), CancellationToken.None);

        Assert.True(result.Success);
        var updatedUser = await db.Users.SingleAsync(item => item.Id == user.Id);
        Assert.Equal(1, updatedUser.PasswordResetVersion);
        Assert.Equal(1, updatedUser.SecurityStampVersion);
        Assert.True(await db.RefreshTokens.Where(token => token.UserId == user.Id).AllAsync(token => token.IsRevoked));
    }

    [Fact]
    public async Task ExceptionMiddleware_MapsForbiddenExceptionTo403()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new ForbiddenException("Forbidden action."),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task RoleChange_IncrementsSecurityStampAndRevokesRefreshTokens()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Staff", "154003");
        var assistantRole = new Role { Id = Guid.NewGuid(), Name = "Assistant", Type = RoleType.Assistant };
        var staffRole = new Role { Id = Guid.NewGuid(), Name = "Staff", Type = RoleType.Staff };
        db.Roles.AddRange(assistantRole, staffRole);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = assistantRole.Id });
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, Token = "refresh-role", ExpiresAt = DateTime.UtcNow.AddDays(1) });
        await db.SaveChangesAsync();

        var result = await new UpdateUserRoleCommandHandler(db)
            .Handle(new UpdateUserRoleCommand(user.Id, ["Staff"], Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.Success);
        var updatedUser = await db.Users.SingleAsync(item => item.Id == user.Id);
        Assert.Equal(1, updatedUser.SecurityStampVersion);
        Assert.True(await db.RefreshTokens.Where(token => token.UserId == user.Id).AllAsync(token => token.IsRevoked));
        var authorizationEvent = await db.OutboxEvents.SingleAsync(item => item.TargetUserId == user.Id.ToString());
        Assert.Equal("StaffDataChanged", authorizationEvent.Type);
    }

    [Fact]
    public async Task StatusChange_IncrementsSecurityStampAndTargetsTheChangedUser()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Status User", "154006");
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, Token = "refresh-status", ExpiresAt = DateTime.UtcNow.AddDays(1) });
        await db.SaveChangesAsync();

        var result = await new UpdateUserStatusCommandHandler(db)
            .Handle(new UpdateUserStatusCommand(user.Id, "Disabled", Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.Success);
        var updatedUser = await db.Users.SingleAsync(item => item.Id == user.Id);
        Assert.False(updatedUser.IsActive);
        Assert.Equal(1, updatedUser.SecurityStampVersion);
        Assert.True(await db.RefreshTokens.Where(token => token.UserId == user.Id).AllAsync(token => token.IsRevoked));
        Assert.True(await db.OutboxEvents.AnyAsync(item => item.TargetUserId == user.Id.ToString() && item.Type == "StaffDataChanged"));
    }

    [Fact]
    public async Task DeviceRevocation_RevokesMatchingRefreshTokens()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "154004");
        var device = new Device { UserId = user.Id, DeviceFingerprint = "device-a", IsActive = true };
        db.Devices.Add(device);
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, DeviceFingerprint = "device-a", Token = "match", ExpiresAt = DateTime.UtcNow.AddDays(1) });
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, DeviceFingerprint = "device-b", Token = "other", ExpiresAt = DateTime.UtcNow.AddDays(1) });
        await db.SaveChangesAsync();

        var result = await new RemoveDeviceCommandHandler(db)
            .Handle(new RemoveDeviceCommand(device.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(await db.RefreshTokens.Where(token => token.Token == "match").AllAsync(token => token.IsRevoked));
        Assert.False(await db.RefreshTokens.Where(token => token.Token == "other").Select(token => token.IsRevoked).SingleAsync());
    }

    private static IConfiguration TestJwtConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:Secret"] = "FakeJwtSecretForAuthSessionSafetyTestsOnly123!",
            ["JwtSettings:Issuer"] = "NaderGorgeAPI",
            ["JwtSettings:Audience"] = "NaderGorgeClients",
            ["JwtSettings:ExpirationMinutes"] = "60",
            ["JwtSettings:RefreshExpirationDays"] = "30"
        })
        .Build();
}
