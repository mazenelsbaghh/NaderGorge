using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Admin bootstrap blocked: database connection reference is missing");
    return 3;
}

var phone = (Console.ReadLine() ?? string.Empty).Trim();
var password = Console.ReadLine() ?? string.Empty;
var fullName = (Console.ReadLine() ?? "Production Owner").Trim();

if (phone.Length is < 10 or > 20 || !phone.All(char.IsDigit))
{
    Console.Error.WriteLine("Admin bootstrap blocked: phone format is invalid");
    return 2;
}
if (password.Length < 10)
{
    Console.Error.WriteLine("Admin bootstrap blocked: password policy failed");
    return 2;
}

try
{
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(connectionString)
        .EnableSensitiveDataLogging(false)
        .Options;
    await using var database = new AppDbContext(options);
    await using var transaction = await database.Database.BeginTransactionAsync();

    if (await database.Users.AnyAsync(user => user.PhoneNumber == phone))
    {
        Console.Error.WriteLine("Admin bootstrap blocked: phone already exists");
        return 5;
    }

    var adminRole = await database.Roles.SingleOrDefaultAsync(role => role.Name == "Admin");
    if (adminRole is null)
    {
        Console.Error.WriteLine("Admin bootstrap blocked: Admin role is missing");
        return 6;
    }

    var user = new User
    {
        Id = Guid.NewGuid(),
        FullName = fullName,
        PhoneNumber = phone,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
        IsActive = true,
        IsProfileComplete = true,
        CreatedAt = DateTime.UtcNow,
    };
    database.Users.Add(user);
    database.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });
    database.AuditLogs.Add(new AuditLog
    {
        Id = Guid.NewGuid(),
        Action = "ProductionAdminBootstrap",
        EntityType = nameof(User),
        EntityId = user.Id,
        PerformedByUserId = user.Id,
        ActorType = "System",
        ActorSnapshot = """{"source":"protected-bootstrap"}""",
        Reason = "Initial production owner identity",
        CreatedAt = DateTime.UtcNow,
    });
    await database.SaveChangesAsync();
    await transaction.CommitAsync();
    Console.WriteLine($"Admin bootstrap complete: userId={user.Id:N}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Admin bootstrap failed: {exception.GetType().Name}");
    return 6;
}
finally
{
    password = string.Empty;
}
