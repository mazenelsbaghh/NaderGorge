using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using NaderGorge.API.Hubs;
using NaderGorge.Domain.Interfaces;
using Xunit;

namespace NaderGorge.Application.Tests;

public class PlatformHubTests
{
    private class FakeGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string GroupName)> AddedGroups { get; } = new();
        public List<(string ConnectionId, string GroupName)> RemovedGroups { get; } = new();

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            AddedGroups.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            RemovedGroups.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    private class FakeAccessCheckService : IAccessCheckService
    {
        public bool PackageAccessAllowed { get; set; } = true;
        public bool LessonAccessAllowed { get; set; } = true;

        public Task<bool> HasAccessToPackageAsync(Guid userId, Guid packageId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PackageAccessAllowed);
        }

        public Task<bool> HasAccessToLessonAsync(Guid userId, Guid lessonId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LessonAccessAllowed);
        }
    }

    private class FakeHubCallerContext : HubCallerContext
    {
        private readonly ClaimsPrincipal _user;
        public override string ConnectionId => "test-conn-id";
        public override ClaimsPrincipal? User => _user;
        public override string? UserIdentifier => _user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        public override IDictionary<object, object?> Items => new Dictionary<object, object?>();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override IFeatureCollection Features => new FeatureCollection();

        public bool IsAborted { get; private set; }

        public FakeHubCallerContext(ClaimsPrincipal user)
        {
            _user = user;
        }

        public override void Abort()
        {
            IsAborted = true;
        }
    }

    [Fact]
    public async Task OnConnectedAsync_ValidUser_JoinsUserAndRoleGroups()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, "Student")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var accessCheck = new FakeAccessCheckService();
        var hub = new PlatformHub(accessCheck);
        
        var groups = new FakeGroupManager();
        var context = new FakeHubCallerContext(principal);

        hub.Groups = groups;
        hub.Context = context;

        // Act
        await hub.OnConnectedAsync();

        // Assert
        Assert.False(context.IsAborted);
        Assert.Contains(groups.AddedGroups, g => g.ConnectionId == "test-conn-id" && g.GroupName == $"User_{userId}");
        Assert.Contains(groups.AddedGroups, g => g.ConnectionId == "test-conn-id" && g.GroupName == "Role_Student");
    }

    [Fact]
    public async Task OnConnectedAsync_InvalidUser_AbortsConnection()
    {
        // Arrange
        var claims = new List<Claim>(); // No claims
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        var accessCheck = new FakeAccessCheckService();
        var hub = new PlatformHub(accessCheck);
        
        var groups = new FakeGroupManager();
        var context = new FakeHubCallerContext(principal);

        hub.Groups = groups;
        hub.Context = context;

        // Act
        await hub.OnConnectedAsync();

        // Assert
        Assert.True(context.IsAborted);
        Assert.Empty(groups.AddedGroups);
    }

    [Fact]
    public async Task JoinPackage_StudentWithAccess_JoinsGroup()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, "Student")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var accessCheck = new FakeAccessCheckService { PackageAccessAllowed = true };
        var hub = new PlatformHub(accessCheck);
        
        var groups = new FakeGroupManager();
        var context = new FakeHubCallerContext(principal);

        hub.Groups = groups;
        hub.Context = context;

        // Act
        await hub.JoinPackage(packageId.ToString());

        // Assert
        Assert.Contains(groups.AddedGroups, g => g.ConnectionId == "test-conn-id" && g.GroupName == $"Package_{packageId}");
    }

    [Fact]
    public async Task JoinPackage_StudentWithoutAccess_DoesNotJoinGroup()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, "Student")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var accessCheck = new FakeAccessCheckService { PackageAccessAllowed = false };
        var hub = new PlatformHub(accessCheck);
        
        var groups = new FakeGroupManager();
        var context = new FakeHubCallerContext(principal);

        hub.Groups = groups;
        hub.Context = context;

        // Act
        await hub.JoinPackage(packageId.ToString());

        // Assert
        Assert.Empty(groups.AddedGroups);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Teacher")]
    [InlineData("Assistant")]
    [InlineData("AssistantReviewer")]
    [InlineData("AssistantAcademic")]
    [InlineData("Supervisor")]
    [InlineData("Staff")]
    public async Task JoinPackage_AdminOrStaff_JoinsGroupDirectly(string role)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var accessCheck = new FakeAccessCheckService { PackageAccessAllowed = false }; // Service says false
        var hub = new PlatformHub(accessCheck);
        
        var groups = new FakeGroupManager();
        var context = new FakeHubCallerContext(principal);

        hub.Groups = groups;
        hub.Context = context;

        // Act
        await hub.JoinPackage(packageId.ToString());

        // Assert
        // Should join because they are staff, bypassing access checks
        Assert.Contains(groups.AddedGroups, g => g.ConnectionId == "test-conn-id" && g.GroupName == $"Package_{packageId}");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Teacher")]
    [InlineData("Assistant")]
    [InlineData("AssistantReviewer")]
    [InlineData("AssistantAcademic")]
    [InlineData("Supervisor")]
    [InlineData("Staff")]
    public async Task OnConnectedAsync_StaffRole_JoinsSharedStaffGroup(string role)
    {
        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        ], "TestAuth"));
        var groups = new FakeGroupManager();
        var hub = new PlatformHub(new FakeAccessCheckService())
        {
            Groups = groups,
            Context = new FakeHubCallerContext(principal)
        };

        await hub.OnConnectedAsync();

        Assert.Contains(groups.AddedGroups, group => group.GroupName == "Role_Staff");
    }

    [Fact]
    public async Task JoinLesson_StudentWithAccess_JoinsGroup()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, "Student")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var accessCheck = new FakeAccessCheckService { LessonAccessAllowed = true };
        var hub = new PlatformHub(accessCheck);
        
        var groups = new FakeGroupManager();
        var context = new FakeHubCallerContext(principal);

        hub.Groups = groups;
        hub.Context = context;

        // Act
        await hub.JoinLesson(lessonId.ToString());

        // Assert
        Assert.Contains(groups.AddedGroups, g => g.ConnectionId == "test-conn-id" && g.GroupName == $"Lesson_{lessonId}");
    }

    [Fact]
    public async Task LeavePackage_RemovesFromGroup()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var claims = new List<Claim>();
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        var accessCheck = new FakeAccessCheckService();
        var hub = new PlatformHub(accessCheck);
        
        var groups = new FakeGroupManager();
        var context = new FakeHubCallerContext(principal);

        hub.Groups = groups;
        hub.Context = context;

        // Act
        await hub.LeavePackage(packageId.ToString());

        // Assert
        Assert.Contains(groups.RemovedGroups, g => g.ConnectionId == "test-conn-id" && g.GroupName == $"Package_{packageId}");
    }
}
