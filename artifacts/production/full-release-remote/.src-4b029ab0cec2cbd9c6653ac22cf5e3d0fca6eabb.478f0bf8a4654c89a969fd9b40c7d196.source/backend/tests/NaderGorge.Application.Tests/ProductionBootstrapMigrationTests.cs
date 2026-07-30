using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NaderGorge.Infrastructure.Migrations;

namespace NaderGorge.Application.Tests;

public sealed class ProductionBootstrapMigrationTests
{
    private static IReadOnlyList<MigrationOperation> Up(Migration migration)
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        migration.GetType()
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations;
    }

    [Fact]
    public void HistoricalAdminMigration_DoesNotCreateAnIdentity()
    {
        Assert.Empty(Up(new AddIbrahimAdmin()));
    }

    [Fact]
    public void TeacherCompatibilityIdentity_IsInactiveAndNonAuthenticatable()
    {
        var sql = string.Join(
            "\n",
            Up(new AddMultiTeacherSubjectArchitecture())
                .OfType<SqlOperation>()
                .Select(operation => operation.Sql));

        Assert.Contains("'!'", sql, StringComparison.Ordinal);
        Assert.Contains("false, false", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("__legacy_teacher__", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("01111111111", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ForwardHardeningMigration_DeletesOnlyKnownBootstrapRows()
    {
        var operations = Up(new HardenProductionBootstrapData());
        var sql = Assert.Single(operations.OfType<SqlOperation>()).Sql;

        Assert.Contains("WHERE \"Id\" =", sql, StringComparison.Ordinal);
        Assert.Contains("foreign_key_violation", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("TRUNCATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM users;", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SystemRoleMigration_CreatesRolesWithoutUsersOrCredentials()
    {
        var sql = string.Join(
            "\n",
            Up(new EnsureSystemRoles())
                .OfType<SqlOperation>()
                .Select(operation => operation.Sql));

        foreach (var role in new[] { "Admin", "Teacher", "Assistant", "Student" })
        {
            Assert.Contains($"'{role}'", sql, StringComparison.Ordinal);
        }
        Assert.Contains("ON CONFLICT (\"Name\") DO NOTHING", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO users", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PasswordHash", sql, StringComparison.OrdinalIgnoreCase);
    }
}
