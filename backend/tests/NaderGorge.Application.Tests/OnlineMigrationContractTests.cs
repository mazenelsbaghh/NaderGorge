using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NaderGorge.Infrastructure.Migrations;

namespace NaderGorge.Application.Tests;

public sealed class OnlineMigrationContractTests
{
    [Theory]
    [InlineData(typeof(AddOutboxClaims))]
    [InlineData(typeof(RepairVideoTypeCodeGrantSchema))]
    [InlineData(typeof(AddWebVitalsDimensions))]
    public void HighWriteIndexes_AreConcurrentAndTransactionSuppressed(
        Type migrationType)
    {
        var sqlOperations = UpOperations(migrationType).OfType<SqlOperation>();
        var indexOperations = sqlOperations
            .Where(operation =>
                operation.Sql.Contains(
                    "CREATE",
                    StringComparison.OrdinalIgnoreCase) &&
                operation.Sql.Contains(
                    "INDEX CONCURRENTLY",
                    StringComparison.OrdinalIgnoreCase));

        Assert.NotEmpty(indexOperations);
        Assert.All(indexOperations, operation =>
        {
            Assert.True(operation.SuppressTransaction);
            Assert.Contains(
                "INDEX CONCURRENTLY",
                operation.Sql,
                StringComparison.OrdinalIgnoreCase);
        });
        Assert.Contains(sqlOperations, operation =>
            operation.SuppressTransaction &&
            operation.Sql.Contains(
                "lock_timeout",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GrantShapeConstraint_IsAddedNotValidBeforeValidation()
    {
        var migrationSql = string.Join(
            '\n',
            UpOperations(typeof(RepairVideoTypeCodeGrantSchema))
                .OfType<SqlOperation>()
                .Select(operation => operation.Sql));

        var notValid = migrationSql.IndexOf(
            ") NOT VALID",
            StringComparison.OrdinalIgnoreCase);
        var validate = migrationSql.IndexOf(
            "VALIDATE CONSTRAINT",
            StringComparison.OrdinalIgnoreCase);
        Assert.True(notValid >= 0);
        Assert.True(validate > notValid);
    }

    [Fact]
    public void AdditiveSchemaChanges_UseBoundedLockWaits()
    {
        foreach (var migrationType in new[]
                 {
                     typeof(AddOutboxClaims),
                     typeof(RepairVideoTypeCodeGrantSchema),
                     typeof(AddWebVitalsDimensions)
                 })
        {
            var migrationSql = string.Join(
                '\n',
                UpOperations(migrationType)
                    .OfType<SqlOperation>()
                    .Select(operation => operation.Sql));
            Assert.Contains(
                "SET LOCAL lock_timeout = '5s'",
                migrationSql,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyList<MigrationOperation> UpOperations(
        Type migrationType)
    {
        var migration = (Migration)Activator.CreateInstance(migrationType)!;
        var builder = new MigrationBuilder(
            "Npgsql.EntityFrameworkCore.PostgreSQL");
        migrationType
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations;
    }
}
