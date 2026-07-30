using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Internal.Commands;
using NaderGorge.Application.Features.Internal.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.Internal;

public class InternalChatSecurityTests
{
    [Fact]
    public async Task GetChatRoomMessages_ReturnsSuccess_WhenUserIsParticipant()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        // 1. Seed Users
        var participantUser = await TestAppDbContextFactory.SeedUserAsync(db, "Participant User", "01011111111");
        var adminUser = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01022222222");

        // 2. Create Room
        var room = new ChatRoom
        {
            Id = Guid.NewGuid(),
            Name = "Test Chat Room",
            Type = ChatRoomType.Group,
            IsArchived = false,
            CreatedByUserId = adminUser.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.ChatRooms.Add(room);

        // 3. Add User as Participant
        var participant = new ChatParticipant
        {
            ChatRoomId = room.Id,
            UserId = participantUser.Id,
            JoinedAt = DateTime.UtcNow
        };
        db.ChatParticipants.Add(participant);

        // 4. Add a message to the room
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatRoomId = room.Id,
            SenderUserId = adminUser.Id,
            Content = "Hello Team!",
            Type = ChatMessageType.Text,
            CreatedAt = DateTime.UtcNow
        };
        db.ChatMessages.Add(message);

        await db.SaveChangesAsync();

        // Act
        var query = new GetChatRoomMessagesQuery(room.Id, participantUser.Id, 1, 50);
        var handler = new GetChatRoomMessagesQueryHandler(db);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal("Hello Team!", result.Data[0].Content);
    }

    [Fact]
    public async Task GetChatRoomMessages_ReturnsFailure_WhenUserIsNotParticipant()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        // 1. Seed Users
        var nonParticipantUser = await TestAppDbContextFactory.SeedUserAsync(db, "Non Participant", "01033333333");
        var adminUser = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01044444444");

        // 2. Create Room
        var room = new ChatRoom
        {
            Id = Guid.NewGuid(),
            Name = "Secret Chat Room",
            Type = ChatRoomType.Group,
            IsArchived = false,
            CreatedByUserId = adminUser.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.ChatRooms.Add(room);

        await db.SaveChangesAsync();

        // Act
        var query = new GetChatRoomMessagesQuery(room.Id, nonParticipantUser.Id, 1, 50);
        var handler = new GetChatRoomMessagesQueryHandler(db);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("You are not authorized", result.Message);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task ArchiveChatRoom_Succeeds_ForAdminOrSupervisor()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        // 1. Seed Admin User
        var adminUser = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01055555555");
        var adminRole = new Role { Id = Guid.NewGuid(), Name = "Admin", Type = RoleType.Admin };
        db.Roles.Add(adminRole);
        db.UserRoles.Add(new UserRole { UserId = adminUser.Id, RoleId = adminRole.Id });

        // 2. Create Room
        var room = new ChatRoom
        {
            Id = Guid.NewGuid(),
            Name = "Room to Archive",
            Type = ChatRoomType.Group,
            IsArchived = false,
            CreatedByUserId = adminUser.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.ChatRooms.Add(room);

        await db.SaveChangesAsync();

        // Act
        var command = new ArchiveChatRoomCommand(room.Id, adminUser.Id, true);
        var handler = new ArchiveChatRoomCommandHandler(db);
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var updatedRoom = await db.ChatRooms.FindAsync(room.Id);
        Assert.NotNull(updatedRoom);
        Assert.True(updatedRoom!.IsArchived);
    }

    [Fact]
    public async Task ArchiveChatRoom_Fails_ForNonPrivilegedUsers()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        // 1. Seed Regular User (e.g. Student role or regular Teacher/Assistant without Admin/Supervisor RoleType)
        var assistantUser = await TestAppDbContextFactory.SeedUserAsync(db, "Assistant User", "01066666666");
        var assistantRole = new Role { Id = Guid.NewGuid(), Name = "Assistant", Type = RoleType.Assistant };
        db.Roles.Add(assistantRole);
        db.UserRoles.Add(new UserRole { UserId = assistantUser.Id, RoleId = assistantRole.Id });

        // 2. Create Room
        var room = new ChatRoom
        {
            Id = Guid.NewGuid(),
            Name = "Room to Archive 2",
            Type = ChatRoomType.Group,
            IsArchived = false,
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        db.ChatRooms.Add(room);

        await db.SaveChangesAsync();

        // Act
        var command = new ArchiveChatRoomCommand(room.Id, assistantUser.Id, true);
        var handler = new ArchiveChatRoomCommandHandler(db);
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Only administrators or supervisors", result.Message);
        
        var updatedRoom = await db.ChatRooms.FindAsync(room.Id);
        Assert.NotNull(updatedRoom);
        Assert.False(updatedRoom!.IsArchived);
    }
}
