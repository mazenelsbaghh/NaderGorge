using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NaderGorge.Infrastructure.Data;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260726120000_HardenProductionBootstrapData")]
public sealed class HardenProductionBootstrapData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            -- Remove the historical fixed Admin identity. The block stays safe
            -- for an environment where another row still references it.
            DO $cleanup$
            BEGIN
                BEGIN
                    DELETE FROM user_roles
                    WHERE "UserId" = 'd36c2e35-512c-497b-b8c7-43df9ac3b123';
                    DELETE FROM users
                    WHERE "Id" = 'd36c2e35-512c-497b-b8c7-43df9ac3b123';
                EXCEPTION WHEN foreign_key_violation THEN
                    RAISE NOTICE 'Legacy Admin retained because it is referenced';
                END;

                DELETE FROM teacher_subjects
                WHERE "TeacherId" = 'b4b82937-293e-48a3-a002-decf9a1efab8'
                  AND "SubjectId" = 'd9b8a342-990a-4286-905e-fdebb2e3895e';

                BEGIN
                    DELETE FROM teacher_profiles
                    WHERE "Id" = 'b4b82937-293e-48a3-a002-decf9a1efab8';
                EXCEPTION WHEN foreign_key_violation THEN
                    RAISE NOTICE 'Legacy teacher profile retained because it is referenced';
                END;

                BEGIN
                    DELETE FROM subjects
                    WHERE "Id" = 'd9b8a342-990a-4286-905e-fdebb2e3895e';
                EXCEPTION WHEN foreign_key_violation THEN
                    RAISE NOTICE 'Legacy subject retained because it is referenced';
                END;

                BEGIN
                    DELETE FROM users
                    WHERE "Id" = 'c4b82937-293e-48a3-a002-decf9a1efab8';
                EXCEPTION WHEN foreign_key_violation THEN
                    RAISE NOTICE 'Legacy teacher user retained because it is referenced';
                END;
            END
            $cleanup$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Security cleanup is intentionally irreversible. Rollback must not
        // recreate fixed identities or credentials.
    }
}
