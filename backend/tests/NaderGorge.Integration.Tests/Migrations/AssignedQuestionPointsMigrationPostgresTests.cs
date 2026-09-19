using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NaderGorge.Infrastructure.Migrations;
using Npgsql;

namespace NaderGorge.Integration.Tests.Migrations;

public sealed class AssignedQuestionPointsMigrationPostgresTests
{
    [Fact]
    public async Task MigrationCorrectsSavedEvidenceIdempotentlyAndSurfacesNullSnapshots()
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException("PostgreSQL migration evidence requires ConnectionStrings__DefaultConnection.");
        await using var connection = new NpgsqlConnection(configured);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await Execute(connection, """
            CREATE TEMP TABLE student_exam_attempts (
                "Id" uuid PRIMARY KEY,
                "DefinitionSnapshotJson" jsonb,
                "ScoreAchieved" numeric NOT NULL,
                "IsPassed" boolean NOT NULL DEFAULT false,
                "IsTimeExpired" boolean NOT NULL,
                "UpdatedAt" timestamp without time zone);
            CREATE TEMP TABLE student_answers (
                "Id" uuid PRIMARY KEY,
                "StudentExamAttemptId" uuid NOT NULL,
                "PointsAwarded" numeric NOT NULL);
            """, transaction);

        var correctedId = Guid.NewGuid();
        var legacyWithEvidenceId = Guid.NewGuid();
        var unsupportedId = Guid.NewGuid();
        var snapshot = """{"SchemaVersion":1,"Kind":"exam","AssessmentId":"00000000-0000-0000-0000-000000000001","Title":"Exam","Description":"","TotalScore":20,"PassingScore":12,"DurationMinutes":null,"IsMandatory":true,"IsRandomized":true,"DisplayQuestionCount":2,"Questions":[{"Id":"00000000-0000-0000-0000-000000000011","BankQuestionId":"00000000-0000-0000-0000-000000000021","Order":1,"Type":0,"Text":"A","Points":10,"AudioUrl":null,"ImageUrl":null,"WrittenCorrection":null,"HintText":null,"BaseText":null,"MistakeStartIndex":null,"MistakeEndIndex":null,"Options":[],"CorrectAnswerKey":null},{"Id":"00000000-0000-0000-0000-000000000012","BankQuestionId":"00000000-0000-0000-0000-000000000022","Order":2,"Type":0,"Text":"B","Points":5,"AudioUrl":null,"ImageUrl":null,"WrittenCorrection":null,"HintText":null,"BaseText":null,"MistakeStartIndex":null,"MistakeEndIndex":null,"Options":[],"CorrectAnswerKey":null}],"ReserveQuestions":[{"Id":"00000000-0000-0000-0000-000000000013","BankQuestionId":"00000000-0000-0000-0000-000000000023","Order":3,"Type":0,"Text":"C","Points":5,"AudioUrl":null,"ImageUrl":null,"WrittenCorrection":null,"HintText":null,"BaseText":null,"MistakeStartIndex":null,"MistakeEndIndex":null,"Options":[],"CorrectAnswerKey":null}],"Revision":{"Answers":[],"MinimumScoreRatio":0.8},"UsesAssignedQuestionPoints":false}""";
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO student_exam_attempts ("Id", "DefinitionSnapshotJson", "ScoreAchieved", "IsTimeExpired")
            VALUES (@corrected, @snapshot::jsonb, 16, false), (@legacy, NULL, 4, false), (@unsupported, NULL, 0, false);
            INSERT INTO student_answers ("Id", "StudentExamAttemptId", "PointsAwarded")
            VALUES (gen_random_uuid(), @corrected, 10), (gen_random_uuid(), @legacy, 4);
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue("corrected", correctedId);
            insert.Parameters.AddWithValue("legacy", legacyWithEvidenceId);
            insert.Parameters.AddWithValue("unsupported", unsupportedId);
            insert.Parameters.AddWithValue("snapshot", snapshot);
            await insert.ExecuteNonQueryAsync();
        }

        var notices = new List<string>();
        connection.Notice += (_, notice) => notices.Add(notice.Notice.MessageText);
        var sql = MigrationSql();
        await Execute(connection, sql, transaction);
        await Execute(connection, sql, transaction);

        await using var query = new NpgsqlCommand("""
            SELECT ("DefinitionSnapshotJson"->>'TotalScore')::numeric,
                   ("DefinitionSnapshotJson"->>'PassingScore')::numeric,
                   ("DefinitionSnapshotJson"->>'UsesAssignedQuestionPoints')::boolean,
                   "ScoreAchieved"
            FROM student_exam_attempts WHERE "Id"=@id
            """, connection, transaction);
        query.Parameters.AddWithValue("id", correctedId);
        await using var reader = await query.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(15m, reader.GetDecimal(0));
        Assert.Equal(9m, reader.GetDecimal(1));
        Assert.True(reader.GetBoolean(2));
        Assert.Equal(12m, reader.GetDecimal(3));
        await reader.CloseAsync();

        Assert.Contains(notices, message => message.Contains("2 legacy exam attempts", StringComparison.Ordinal));
        Assert.Contains(notices, message => message.Contains("1 legacy exam attempts", StringComparison.Ordinal));

        var lateAttemptId = Guid.NewGuid();
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO student_exam_attempts
                ("Id", "DefinitionSnapshotJson", "ScoreAchieved", "IsPassed", "IsTimeExpired")
            VALUES (@id, @snapshot::jsonb, 0, true, false);
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue("id", lateAttemptId);
            insert.Parameters.AddWithValue("snapshot", snapshot);
            await insert.ExecuteNonQueryAsync();
        }

        var followupSql = FollowupMigrationSql();
        await Execute(connection, followupSql, transaction);
        await Execute(connection, followupSql, transaction);
        await using var followupQuery = new NpgsqlCommand("""
            SELECT ("DefinitionSnapshotJson"->>'TotalScore')::numeric,
                   ("DefinitionSnapshotJson"->>'PassingScore')::numeric,
                   ("DefinitionSnapshotJson"->>'UsesAssignedQuestionPoints')::boolean,
                   "ScoreAchieved", "IsPassed"
            FROM student_exam_attempts WHERE "Id"=@id
            """, connection, transaction);
        followupQuery.Parameters.AddWithValue("id", lateAttemptId);
        await using var followupReader = await followupQuery.ExecuteReaderAsync();
        Assert.True(await followupReader.ReadAsync());
        Assert.Equal(15m, followupReader.GetDecimal(0));
        Assert.Equal(9m, followupReader.GetDecimal(1));
        Assert.True(followupReader.GetBoolean(2));
        Assert.Equal(12m, followupReader.GetDecimal(3));
        Assert.True(followupReader.GetBoolean(4));
    }

    private static string MigrationSql()
    {
        var migration = new UseAssignedQuestionPointsForExamAttempts();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        typeof(UseAssignedQuestionPointsForExamAttempts)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return Assert.Single(builder.Operations.OfType<SqlOperation>()).Sql;
    }

    private static string FollowupMigrationSql()
    {
        var migration = new ReconcileRemainingExamAttemptScales();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        typeof(ReconcileRemainingExamAttemptScales)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return Assert.Single(builder.Operations.OfType<SqlOperation>()).Sql;
    }

    private static async Task Execute(NpgsqlConnection connection, string sql, NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }
}
