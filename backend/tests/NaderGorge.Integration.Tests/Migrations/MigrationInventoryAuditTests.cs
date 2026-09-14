using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Integration.Tests.Migrations;

public sealed class MigrationInventoryAuditTests
{
    private static readonly string[] SupersededMigrationTypes =
    [
        "AddUserSecurityStampVersion",
        "AddVideoTypeCodeGrants",
        "EnforceSingleTeacherStaffMembership",
        "GrantStaffStudentManagementAndReports",
    ];

    static MigrationInventoryAuditTests() =>
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    [Fact]
    public void MigrationAssembly_RegistersEveryActiveMigration()
    {
        using var database = CreateDatabase();
        var migrationAssembly = database.GetService<IMigrationsAssembly>();
        var registeredTypes = migrationAssembly.Migrations.Values
            .Select(type => type.AsType())
            .ToHashSet();
        var unregisteredTypes = typeof(AppDbContext).Assembly.GetTypes()
            .Where(type =>
                !type.IsAbstract &&
                typeof(Migration).IsAssignableFrom(type) &&
                !registeredTypes.Contains(type))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(SupersededMigrationTypes, unregisteredTypes);
        Assert.Contains(
            "20260729220000_AddWebVitalsDimensions",
            migrationAssembly.Migrations.Keys);
    }

    [Fact]
    public void MigrationAssembly_GeneratesCompleteIdempotentUpgradeScript()
    {
        using var database = CreateDatabase();
        var migrationAssembly = database.GetService<IMigrationsAssembly>();
        var migrations = migrationAssembly.Migrations.Keys.ToArray();
        var script = database.GetService<IMigrator>().GenerateScript(
            options: MigrationsSqlGenerationOptions.Idempotent);

        Assert.Equal(
            migrations.Order(StringComparer.Ordinal),
            migrations);
        Assert.Equal(migrations.Length, migrations.Distinct().Count());
        Assert.All(
            migrations,
            migration => Assert.Contains(migration, script, StringComparison.Ordinal));
    }

    [Fact]
    public void ModelSnapshot_MatchesCurrentDesignModel()
    {
        using var database = CreateDatabase();
        var migrationAssembly = database.GetService<IMigrationsAssembly>();
        var snapshot = Assert.IsAssignableFrom<ModelSnapshot>(
            migrationAssembly.ModelSnapshot);
        var snapshotModel = database.GetService<IModelRuntimeInitializer>()
            .Initialize(
                snapshot.Model,
                designTime: true,
                validationLogger: null);
        var currentModel = database.GetService<IDesignTimeModel>().Model;
        var differences = database.GetService<IMigrationsModelDiffer>()
            .GetDifferences(
                snapshotModel.GetRelationalModel(),
                currentModel.GetRelationalModel());

        Assert.Empty(differences);
    }

    private static AppDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=127.0.0.1;Port=1;Database=migration_audit;" +
                "Username=audit;Password=unused")
            .Options;
        return new AppDbContext(options);
    }
}
